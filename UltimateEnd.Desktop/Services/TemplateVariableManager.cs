using System.Collections.Generic;
using System.IO;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class TemplateVariableManager : ITemplateVariableManager
    {
        public List<TemplateVariable> Variables =>
        [
            new TemplateVariable
            {
                Variable = "{romPath}",
                Description = "ROM 파일의 전체 경로"
            },
            new TemplateVariable
            {
                Variable = "{romDir}/{romName}",
                Description = "롬이 파일이 아닌 폴더인 경우"
            },
            new TemplateVariable
            {
                Variable = "{romDir}",
                Description = "ROM 파일이 있는 디렉토리 경로"
            },
            new TemplateVariable
            {
                Variable = "{romName}",
                Description = "ROM 파일 이름 (확장자 제외)"
            },
            new TemplateVariable
            {
                Variable = "{coreName}",
                Description = "코어 이름 (RetroArch)"
            },
            new TemplateVariable
            {
                Variable = "{corePath}",
                Description = "코어 파일 경로 (RetroArch)"
            }
        ];

        private static string FormatPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (path.StartsWith('\"') && path.EndsWith('\"'))
                return path;

            if (path.Contains(' '))
                return $"\"{path}\"";

            return path;
        }

        public static string ReplaceTokens(string template, string romPath, string? coreName = null, string? corePath = null)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string romName = Path.GetFileNameWithoutExtension(romPath);
            string romDir = Path.GetDirectoryName(romPath) ?? string.Empty;

            string result = template;

            result = result.Replace("{romPath}", FormatPath(romPath));
            result = result.Replace("{romDir}", FormatPath(romDir));
            result = result.Replace("{romName}", romName);

            if (!string.IsNullOrEmpty(coreName))
                result = result.Replace("{coreName}", coreName);

            if (!string.IsNullOrEmpty(corePath))
                result = result.Replace("{corePath}", FormatPath(corePath));

            return result;
        }
    }
}