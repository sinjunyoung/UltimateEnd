using System.Collections.Generic;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
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
                Variable = "{safUriRomPath}",
                Description = "SAF content:// URI 형식의 ROM 경로"
            },
            new TemplateVariable
            {
                Variable = "{fileUriRomPath}",
                Description = "file:// URI 형식의 ROM 경로 (am start용)"
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

        public static string ReplaceTokens(string template, TokenContext context)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string result = template;

            result = result.Replace("{romPath}", context.RomPath);
            result = result.Replace("{romDir}", context.RomDir);
            result = result.Replace("{romName}", context.RomName);

            if (!string.IsNullOrEmpty(context.CoreName))
                result = result.Replace("{coreName}", context.CoreName);

            if (!string.IsNullOrEmpty(context.CorePath))
                result = result.Replace("{corePath}", context.CorePath);

            if (!string.IsNullOrEmpty(context.SafUriRomPath))
                result = result.Replace("{safUriRomPath}", context.SafUriRomPath);

            if (!string.IsNullOrEmpty(context.FileUriRomPath))
                result = result.Replace("{fileUriRomPath}", context.FileUriRomPath);

            return result;
        }
    }
}