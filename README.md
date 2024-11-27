# DicomProcessor

## 项目简介
DicomProcessor 是一个用于处理 DICOM 文件的 API。它允许用户上传 DICOM 文件并提取相关的元数据。该项目旨在为医疗影像处理提供一个简单而高效的解决方案。

## 功能
- 支持批量处理 DICOM 文件。
- 提取 DICOM 文件中的重要元数据（如患者信息、研究信息等）。
- 提供 RESTful API 接口，方便与其他系统集成。

## 技术栈
- .NET 6
- ASP.NET Core
- FellowOakDicom

## 安装与运行
1. 克隆项目到本地：
   ```bash
   git clone https://github.com/yourusername/DicomProcessor.git
   cd DicomProcessor
   ```

2. 使用 Visual Studio 或命令行工具打开项目。

3. 确保安装了 .NET 6 SDK。

4. 运行项目：
   ```bash
   dotnet run
   ```

5. API 将在 `http://localhost:5000` 上运行。

## API 端点

### 处理 DICOM 文件
- **请求方法**: `GET`
- **路径**: `/api/dicom/process`
- **查询参数**:
  - `sharePath`: 共享目录的路径，必须包含 DICOM 文件。
- **示例请求**:
  ```http
  GET http://localhost:5000/api/dicom/process?sharePath=C:\path\to\dicom\files
  ```

### 返回值
- 返回一个 JSON 对象，包含处理结果和提取的标签信息。例如：
  ```json
  {
      "success": true,
      "results": [
          {
              "FilePath": "C:\\path\\to\\dicom\\files\\example.dcm",
              "Tags": {
                  "PatientID": "12345",
                  "PatientName": "John Doe",
                  "StudyInstanceUID": "1.2.3.4.5"
              }
          }
      ]
  }
  ```

## 注意事项
- 确保提供的路径存在且包含 DICOM 文件。
- 处理过程中可能会输出进度信息到控制台。
- 处理完成后，返回的结果将包含成功处理的文件数量和相关的元数据。

## 贡献
欢迎任何形式的贡献！请提交问题或拉取请求。

## 许可证
本项目采用 MIT 许可证，详细信息请查看 LICENSE 文件。
