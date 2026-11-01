# DicomProcessor

基于 ASP.NET Core 的 DICOM 元数据提取 API：指定目录后递归扫描 `.dcm` 文件，并行提取常用标签并以 JSON 返回。

## 功能

- 递归扫描目录下全部 `.dcm` 文件
- 并行提取患者 / 检查 / 序列 / 实例相关元数据
- 读取时跳过 `PixelData`，降低内存占用
- 提供 RESTful API，便于与其他系统集成

## 技术栈

- .NET 8 / ASP.NET Core
- [fo-dicom](https://github.com/fo-dicom/fo-dicom) (FellowOakDicom) 5.1.5

## 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 快速开始

```bash
git clone https://github.com/fightroad/DicomProcessor.git
cd DicomProcessor
dotnet run
```

启动后：

| 地址 | 说明 |
|------|------|
| http://localhost:5168 | HTTP |
| https://localhost:7274 | HTTPS |

Gitee 镜像：`https://gitee.com/fightroad/DicomProcessor.git`

## API

### `GET /api/dicom/process`

| 参数 | 位置 | 必填 | 说明 |
|------|------|------|------|
| `sharePath` | Query | 是 | 含 DICOM 文件的目录路径，须存在 |

**请求示例**

```http
GET http://localhost:5168/api/dicom/process?sharePath=C:\path\to\dicom\files
```

路径不存在或为空时返回 `400`，正文：`共享目录路径不能为空或不存在`。

**成功响应**（`200`，直接返回数组）

```json
[
  {
    "filePath": "C:\\path\\to\\dicom\\files\\example.dcm",
    "tags": {
      "PatientID": "12345",
      "PatientName": "John Doe",
      "PatientBirthDate": "19800101",
      "StudyInstanceUID": "1.2.3.4.5",
      "StudyID": "1",
      "StudyDate": "20240101",
      "StudyTime": "120000",
      "StudyDescription": "CT Chest",
      "SeriesInstanceUID": "1.2.3.4.5.6",
      "SeriesNumber": "1",
      "Modality": "CT",
      "SeriesDescription": "Axial",
      "SOPInstanceUID": "1.2.3.4.5.6.7",
      "InstanceNumber": "1",
      "SOPClassUID": "1.2.840.10008.5.1.4.1.1.2"
    }
  }
]
```

### 提取的标签

| 标签 | 说明 |
|------|------|
| PatientID / PatientName / PatientBirthDate | 患者 |
| StudyInstanceUID / StudyID / StudyDate / StudyTime / StudyDescription | 检查 |
| SeriesInstanceUID / SeriesNumber / Modality / SeriesDescription | 序列 |
| SOPInstanceUID / InstanceNumber / SOPClassUID | 实例 |

单个文件解析失败时会跳过该文件并在控制台输出错误；接口仍返回其余成功结果。处理进度与耗时会打印到控制台。

## 注意事项

- 仅匹配扩展名为 `.dcm` 的文件
- 并行度默认为 CPU 核心数
- 已启用 CORS（允许任意来源），便于前端或其他服务调用
- 生产环境请按需收紧 CORS，并注意路径访问权限

## 许可证

[MIT](LICENSE)
