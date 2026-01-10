using System;
using System.IO;
using System.Xml;

namespace UltimateEnd.Scraper.Helpers
{
    public static class ApiErrorParser
    {
        const string ApiLimitExceededMessage = "API 호출 제한 초과. 잠시 후 다시 시도하세요";

        public static string? Check(string xmlContent)
        {
            if (string.IsNullOrEmpty(xmlContent))
                return "API 응답이 비어있습니다";

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersFromEntities = 1024
                };

                using var stringReader = new StringReader(xmlContent);
                using var xmlReader = XmlReader.Create(stringReader, settings);

                var doc = new XmlDocument();
                doc.Load(xmlReader);

                var errorNode = doc.SelectSingleNode("//Data/error");

                if (errorNode == null)
                    return null;

                var errorMsg = errorNode.InnerText;

                if (string.IsNullOrEmpty(errorMsg))
                    return "알 수 없는 API 오류";

                if (errorMsg.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                    errorMsg.Contains("exceeded", StringComparison.OrdinalIgnoreCase) ||
                    errorMsg.Contains("limit", StringComparison.OrdinalIgnoreCase))
                    return ApiLimitExceededMessage;

                return $"API 오류: {errorMsg}";
            }
            catch (XmlException)
            {
                return "잘못된 XML 응답";
            }
            catch (Exception ex)
            {
                return $"응답 파싱 오류: {ex.Message}";
            }
        }
    }
}