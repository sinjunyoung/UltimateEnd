using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UltimateEnd.SaveFile
{
    public class GoogleDriveFileList
    {
        [JsonPropertyName("files")]
        public List<GoogleDriveFile> Files { get; set; }
    }
}