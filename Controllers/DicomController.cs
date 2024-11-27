using Microsoft.AspNetCore.Mvc;
using DicomProcessor.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FellowOakDicom;
using System.Threading;
using System.Linq;

namespace DicomProcessor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DicomController : ControllerBase
    {
        private static readonly SemaphoreSlim _processingSemaphore = new(Environment.ProcessorCount);

        [HttpGet("process")]
        public async Task<IActionResult> ProcessDicomFiles([FromQuery] string sharePath)
        {
            if (string.IsNullOrEmpty(sharePath) || !Directory.Exists(sharePath))
            {
                return BadRequest("共享目录路径不能为空或不存在");
            }

            var dicomFiles = Directory.EnumerateFiles(sharePath, "*.dcm", SearchOption.AllDirectories).ToList();
            int totalFiles = dicomFiles.Count;
            Console.WriteLine($"找到 {totalFiles} 个 DICOM 文件");

            var results = new ConcurrentBag<DicomFileResult>();
            int processedCount = 0;
            var stopwatch = Stopwatch.StartNew();

            await Parallel.ForEachAsync(dicomFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (file, token) =>
            {
                await _processingSemaphore.WaitAsync(token);
                try
                {
                    var result = await ProcessDicomFileAsync(file).ConfigureAwait(false);
                    if (result != null)
                    {
                        results.Add(result);
                    }

                    Interlocked.Increment(ref processedCount);
                    UpdateProgress(processedCount, totalFiles);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理文件 {file} 时出错: {ex.Message}");
                }
                finally
                {
                    _processingSemaphore.Release();
                }
            }).ConfigureAwait(false);

            Console.Write($"\r进度: 100% ({processedCount}/{totalFiles})");
            stopwatch.Stop();
            Console.WriteLine($"\n处理完成! 成功处理: {results.Count}/{totalFiles}个文件");
            Console.WriteLine($"总用时: {stopwatch.ElapsedMilliseconds / 1000.0:F1}秒");

            return Ok(results);
        }

        private async Task<DicomFileResult?> ProcessDicomFileAsync(string filePath)
        {
            try
            {
                var result = new DicomFileResult { FilePath = filePath };
                var tags = new ConcurrentDictionary<string, string>();

                var dicomFile = await DicomFile.OpenAsync(filePath, FileReadOption.Default).ConfigureAwait(false);
                var dataset = dicomFile.Dataset;

                dataset.Remove(DicomTag.PixelData);
                await ExtractTagsAsync(dataset, tags).ConfigureAwait(false);

                result.Tags = tags;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理文件 {filePath} 时出错: {ex.Message}");
                return null;
            }
        }

        private async Task ExtractTagsAsync(DicomDataset dataset, ConcurrentDictionary<string, string> tags)
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

            var tasks = tagList.Select(tag => ExtractTagAsync(dataset, tag.Item1, tag.Item2, tags));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ExtractTagAsync(DicomDataset dataset, DicomTag tag, string tagName, ConcurrentDictionary<string, string> tags)
        {
            try
            {
                var value = dataset.Contains(tag) ? dataset.GetSingleValueOrDefault(tag, string.Empty) : string.Empty;
                tags.TryAdd(tagName, value?.Trim() ?? string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取标签 {tagName} 时出错: {ex.Message}");
                tags.TryAdd(tagName, string.Empty);
            }
        }

        private void UpdateProgress(int processed, int totalFiles)
        {
            if (processed % 100 == 0)
            {
                Console.Write($"\r进度: {processed * 100 / totalFiles}% ({processed}/{totalFiles})");
            }
        }
    }
}
