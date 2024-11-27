using System;
using System.IO;
using System.Xml;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using FellowOakDicom;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DicomProcessor
{
    class Program
    {
        private static readonly SemaphoreSlim _processingSemaphore = new(Environment.ProcessorCount);
        private static readonly object _consoleLock = new object(); // 控制台输出锁

        static async Task Main(string[] args)
        {
            string sharePath;
            int maxDegreeOfParallelism = Environment.ProcessorCount; // 默认使用 CPU 核心数量

            // 检查命令行参数数量
            if (args.Length != 1) 
            {
                Console.WriteLine("使用方法: DicomProcessor.exe <共享目录路径>");
                Console.WriteLine("示例: DicomProcessor.exe \\\\server\\share\\folder");
                return;
            }

            sharePath = args[0]; // 第一个参数为共享目录路径

            // 创建输出目录
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xml");
            Directory.CreateDirectory(outputDir);

            try
            {
                var stopwatch = Stopwatch.StartNew(); // 开始计时

                // 使用并行处理获取 DICOM 文件路径
                var dicomFiles = await Task.Run(() => Directory.EnumerateFiles(sharePath, "*.dcm", SearchOption.AllDirectories).ToList());
                int totalFiles = dicomFiles.Count(); // 计算总文件数
                Console.WriteLine($"找到 {totalFiles} 个DICOM文件");
                Console.WriteLine($"使用 {maxDegreeOfParallelism} 个线程并发处理");
                Console.WriteLine($"开始时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                var results = new ConcurrentBag<DicomFileResult>(); // 存储处理结果的线程安全集合
                var processedCount = 0; // 记录已处理文件数量

                var parallelOptions = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = maxDegreeOfParallelism // 设置并行处理的最大线程数
                };

                // 使用 Task.Run 进行异步处理
                await Task.Run(async () =>
                {
                    await Parallel.ForEachAsync(dicomFiles, parallelOptions, async (file, token) =>
                    {
                        await _processingSemaphore.WaitAsync(); // 等待信号量，控制并发数
                        try
                        {
                            var result = await ProcessDicomFileAsync(file); // 处理 DICOM 文件
                            if (result != null)
                            {
                                results.Add(result); // 将结果添加到集合中
                            }

                            // 更新进度
                            int processed = Interlocked.Increment(ref processedCount); // 原子性增加已处理计数
                            if (processed % 100 == 0) // 每处理100个文件更新一次进度
                            {
                                Console.Write($"\r进度: {processed * 100 / totalFiles}% ({processed}/{totalFiles})");
                            }
                        }
                        finally
                        {
                            _processingSemaphore.Release(); // 释放信号量
                        }
                    });
                });

                Console.WriteLine("\n处理完成!");
                Console.WriteLine($"成功处理: {results.Count}/{totalFiles}个文件");

                Console.WriteLine("正在保存XML文件...");
                await SaveToXmlAsync(results, outputDir); // 保存结果到 XML 文件

                stopwatch.Stop(); // 停止计时
                var totalSeconds = stopwatch.ElapsedMilliseconds / 1000.0; // 计算总用时
                Console.WriteLine($"\n全部完成! 总用时: {totalSeconds:F1}秒");
                Console.WriteLine($"平均每个文件用时: {(stopwatch.ElapsedMilliseconds / (double)results.Count):F1}毫秒");
                Console.WriteLine($"处理速度: {(results.Count / totalSeconds):F1}文件/秒");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}"); // 捕获并输出异常信息
            }
        }

        // 异步处理 DICOM 文件
        static async Task<DicomFileResult?> ProcessDicomFileAsync(string filePath)
        {
            try
            {
                var result = new DicomFileResult { FilePath = filePath }; // 创建结果对象
                var tags = new ConcurrentDictionary<string, string>(); // 存储标签的线程安全字典

                // 使用流式读取 DICOM 文件
                var options = FileReadOption.Default;
                var dicomFile = await DicomFile.OpenAsync(filePath, options);
                var dataset = dicomFile.Dataset;

                // 手动忽略像素数据
                dataset.Remove(DicomTag.PixelData); // 直接移除像素数据

                // 提取标签
                ExtractTags(dataset, tags); // 合并提取标签的逻辑

                result.Tags = tags; // 将提取的标签赋值给结果
                return result; // 返回结果
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n处理文件 {filePath} 时出错: {ex.Message}"); // 捕获并输出异常信息
                return null; // 返回 null 表示处理失败
            }
        }

        // 合并提取标签的逻辑
        static void ExtractTags(DicomDataset dataset, ConcurrentDictionary<string, string> tags)
        {
            var tagList = new (DicomTag, string)[]
            {
                (DicomTag.PatientID, "PatientID"),
                (DicomTag.PatientName, "PatientName"),
                (DicomTag.PatientBirthDate, "PatientBirthDate"),
                (DicomTag.StudyInstanceUID, "StudyInstanceUID"),
                (DicomTag.StudyID, "StudyID"),
                (DicomTag.StudyDate, "StudyDate"),
                (DicomTag.StudyTime, "StudyTime"),
                (DicomTag.StudyDescription, "StudyDescription"),
                (DicomTag.SeriesInstanceUID, "SeriesInstanceUID"),
                (DicomTag.SeriesNumber, "SeriesNumber"),
                (DicomTag.Modality, "Modality"),
                (DicomTag.SeriesDescription, "SeriesDescription"),
                (DicomTag.SOPInstanceUID, "SOPInstanceUID"),
                (DicomTag.InstanceNumber, "InstanceNumber"),
                (DicomTag.SOPClassUID, "SOPClassUID")
            };

            Parallel.ForEach(tagList, tag => 
            {
                ExtractTag(dataset, tag.Item1, tag.Item2, tags);
            });
        }

        // 提取单个标签
        static void ExtractTag(DicomDataset dataset, DicomTag tag, string tagName, ConcurrentDictionary<string, string> tags)
        {
            try
            {
                if (dataset.Contains(tag)) // 检查标签是否存在
                {
                    var value = dataset.GetSingleValueOrDefault(tag, string.Empty); // 获取标签值
                    tags.TryAdd(tagName, value?.Trim() ?? string.Empty); // 添加到字典中
                }
                else
                {
                    tags.TryAdd(tagName, string.Empty); // 标签不存在时添加空值
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取标签 {tagName} 时出错: {ex.Message}"); // 记录具体的异常信息
                tags.TryAdd(tagName, string.Empty); // 捕获异常并添加空值
            }
        }

        // 异步保存结果到 XML 文件
        static async Task SaveToXmlAsync(ConcurrentBag<DicomFileResult> results, string outputDir)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"); // 获取当前时间戳
            var xmlPath = Path.Combine(outputDir, $"dicom_tags_{timestamp}.xml"); // XML 文件路径

            var settings = new XmlWriterSettings
            {
                Async = true, // 启用异步写入
                Indent = true, // 启用缩进
                Encoding = new UTF8Encoding(false) // 不使用 BOM
            };

            using var writer = XmlWriter.Create(xmlPath, settings); // 创建 XML 写入器
            await writer.WriteStartDocumentAsync(); // 写入文档开始
            await writer.WriteStartElementAsync(null, "DicomFiles", null); // 写入根元素

            // 顺序写入每个文件的标签
            foreach (var result in results)
            {
                await writer.WriteStartElementAsync(null, "File", null); // 写入文件元素
                await writer.WriteAttributeStringAsync(null, "path", null, XmlConvert.EncodeName(result.FilePath)); // 写入文件路径属性

                foreach (var tag in result.Tags) // 写入每个标签
                {
                    await writer.WriteStartElementAsync(null, "Tag", null); // 写入标签元素
                    await writer.WriteAttributeStringAsync(null, "name", null, XmlConvert.EncodeName(tag.Key)); // 写入标签名称属性
                    await writer.WriteStringAsync(tag.Value); // 写入标签值
                    await writer.WriteEndElementAsync(); // 结束标签元素
                }

                await writer.WriteEndElementAsync(); // 结束文件元素
            }

            await writer.WriteEndElementAsync(); // 结束根元素
            await writer.WriteEndDocumentAsync(); // 结束文档
        }
    }

    // DICOM 文件处理结果类
    class DicomFileResult
    {
        public string FilePath { get; set; } = string.Empty; // 文件路径
        public ConcurrentDictionary<string, string> Tags { get; set; } = new(); // 标签字典
    }
}
