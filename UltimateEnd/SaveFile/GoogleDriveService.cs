using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UltimateEnd.SaveFile
{
    public class GoogleDriveService
    {
        private static readonly HttpClient _sharedHttpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private string _accessToken;

        public GoogleDriveService() { }

        public void SetAccessToken(string accessToken)
        {
            _accessToken = accessToken;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _sharedHttpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> UploadFileAsync(string fileName, byte[] fileData, string folderId = null)
        {
            var metadata = new
            {
                name = fileName,
                parents = folderId != null ? new[] { folderId } : null
            };

            var metadataJson = JsonSerializer.Serialize(metadata);
            var boundary = "----" + DateTime.Now.Ticks.ToString("x");
            var bodyBuilder = new StringBuilder();

            bodyBuilder.AppendLine($"--{boundary}");
            bodyBuilder.AppendLine("Content-Type: application/json; charset=UTF-8");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine(metadataJson);
            bodyBuilder.AppendLine($"--{boundary}");
            bodyBuilder.AppendLine("Content-Type: application/octet-stream");
            bodyBuilder.AppendLine();

            var headerBytes = Encoding.UTF8.GetBytes(bodyBuilder.ToString());
            var footerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--");

            var totalBytes = new byte[headerBytes.Length + fileData.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, totalBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(fileData, 0, totalBytes, headerBytes.Length, fileData.Length);
            Buffer.BlockCopy(footerBytes, 0, totalBytes, headerBytes.Length + fileData.Length, footerBytes.Length);

            var content = new ByteArrayContent(totalBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("multipart/related")
            {
                Parameters = { new NameValueHeaderValue("boundary", boundary) }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _sharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result);

                return fileInfo["id"].GetString();
            }

            return null;
        }

        public async Task<string> FindFileByNameAsync(string fileName, string folderId = null)
        {
            var escapedFileName = fileName.Replace("\\", "\\\\").Replace("'", "\\'");

            var query = $"name='{escapedFileName}' and trashed=false";

            if (folderId != null)
                query += $" and '{folderId}' in parents";

            var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _sharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GoogleDriveFileList>(json);

                return result?.Files?.FirstOrDefault()?.Id;
            }

            return null;
        }

        public async Task<bool> UpdateFileAsync(string fileId, byte[] fileData)
        {
            var boundary = "----" + DateTime.Now.Ticks.ToString("x");

            var metadata = new
            {
                modifiedTime = DateTime.UtcNow.ToString("o")
            };
            var metadataJson = JsonSerializer.Serialize(metadata);

            var bodyBuilder = new StringBuilder();

            bodyBuilder.AppendLine($"--{boundary}");
            bodyBuilder.AppendLine("Content-Type: application/json; charset=UTF-8");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine(metadataJson);
            bodyBuilder.AppendLine($"--{boundary}");
            bodyBuilder.AppendLine("Content-Type: application/octet-stream");
            bodyBuilder.AppendLine();

            var headerBytes = Encoding.UTF8.GetBytes(bodyBuilder.ToString());
            var footerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--");
            var totalBytes = new byte[headerBytes.Length + fileData.Length + footerBytes.Length];

            Buffer.BlockCopy(headerBytes, 0, totalBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(fileData, 0, totalBytes, headerBytes.Length, fileData.Length);
            Buffer.BlockCopy(footerBytes, 0, totalBytes, headerBytes.Length + fileData.Length, footerBytes.Length);

            var content = new ByteArrayContent(totalBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("multipart/related")
            {
                Parameters = { new NameValueHeaderValue("boundary", boundary) }
            };

            var request = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"https://www.googleapis.com/upload/drive/v3/files/{fileId}?uploadType=multipart")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var response = await _sharedHttpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
        }

        public async Task<byte[]> DownloadFileAsync(string fileId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var response = await _sharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();

            return null;
        }

        public async Task<string> CreateFolderAsync(string folderName, string parentFolderId = null)
        {
            var metadata = new
            {
                name = folderName,
                mimeType = "application/vnd.google-apps.folder",
                parents = parentFolderId != null ? new[] { parentFolderId } : null
            };

            var content = new StringContent(JsonSerializer.Serialize(metadata), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files")
            {
                Content = content
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _sharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                var folderInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result);

                return folderInfo["id"].GetString();
            }

            return null;
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/drive/v3/files/{fileId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _sharedHttpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<SaveBackupInfo>> FindFilesByPrefixAsync(string prefix, string folderId, int limit = 10)
        {
            var escapedPrefix = prefix.Replace("\\", "\\\\").Replace("'", "\\'");
            var query = $"name contains '{escapedPrefix}' and trashed=false and '{folderId}' in parents";
            var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime)&orderBy=modifiedTime desc&pageSize={limit}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _sharedHttpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GoogleDriveFileList>(json);

                return result?.Files?.Select(f => new SaveBackupInfo
                {
                    FileId = f.Id,
                    FileName = f.Name,
                    ModifiedTime = DateTime.Parse(f.ModifiedTime)
                }).ToList() ?? [];
            }

            return [];
        }
    }
}