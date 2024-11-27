using System.Collections.Concurrent;

namespace DicomProcessor.Models
{
    public class DicomFileResult
    {
        public string? FilePath { get; set; }
        public ConcurrentDictionary<string, string> Tags { get; set; } = new ConcurrentDictionary<string, string>();
    }
}
