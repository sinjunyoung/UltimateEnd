using System;
using System.Text;
using UltimateEnd.Enums;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    public static class UrlBuilder
    {
        public static string BuildSearchUrl(string fileName, ScreenScraperSystemId systemId, string crc = null, long fileSize = 0)
        {
            var searchMethod = ScreenScraperConfig.Instance.PreferredSearchMethod;

            if (searchMethod == SearchMethod.ByCrc && string.IsNullOrEmpty(crc))
                searchMethod = SearchMethod.ByFileName;

            return searchMethod == SearchMethod.ByCrc
                ? BuildCrcSearchUrl(crc, fileName, fileSize, systemId)
                : BuildFileNameSearchUrl(fileName, systemId);
        }

        private static string BuildFileNameSearchUrl(string fileName, ScreenScraperSystemId systemId)
        {
            var sb = new StringBuilder(300);
            sb.Append(ScreenScraperConfig.Instance.ApiUrlBase);
            sb.Append("/jeuInfos.php?devid=");
            sb.Append(ScreenScraperConfig.Instance.ApiDevU);
            sb.Append("&devpassword=");
            sb.Append(ScreenScraperConfig.Instance.ApiDevP);
            sb.Append("&softname=");
            sb.Append(Uri.EscapeDataString(ScreenScraperConfig.Instance.ApiSoftName));
            sb.Append("&output=xml&romnom=");
            sb.Append(Uri.EscapeDataString(fileName));
            sb.Append("&langue=");
            sb.Append(ScreenScraperConfig.Instance.PreferredLanguage);

            if (systemId != ScreenScraperSystemId.NotSupported)
            {
                sb.Append("&systemeid=");
                sb.Append((int)systemId);
            }

            AppendCredentials(sb);
            return sb.ToString();
        }

        private static string BuildCrcSearchUrl(string crc, string fileName, long fileSize, ScreenScraperSystemId systemId)
        {
            var sb = new StringBuilder(300);
            sb.Append(ScreenScraperConfig.Instance.ApiUrlBase);
            sb.Append("/jeuInfos.php?devid=");
            sb.Append(ScreenScraperConfig.Instance.ApiDevU);
            sb.Append("&devpassword=");
            sb.Append(ScreenScraperConfig.Instance.ApiDevP);
            sb.Append("&softname=");
            sb.Append(Uri.EscapeDataString(ScreenScraperConfig.Instance.ApiSoftName));            
            sb.Append("&output=xml");
            sb.Append("&crc=");
            sb.Append(crc);
            sb.Append("&romtaille=");
            sb.Append(fileSize);
            sb.Append("&langue=");
            sb.Append(ScreenScraperConfig.Instance.PreferredLanguage);

            if (systemId != ScreenScraperSystemId.NotSupported)
            {
                sb.Append("&systemeid=");
                sb.Append((int)systemId);
            }

            AppendCredentials(sb);
            return sb.ToString();
        }

        private static void AppendCredentials(StringBuilder sb)
        {
            if (!string.IsNullOrEmpty(ScreenScraperConfig.Instance.Username) &&
                !string.IsNullOrEmpty(ScreenScraperConfig.Instance.Password))
            {
                sb.Append("&ssid=");
                sb.Append(Uri.EscapeDataString(ScreenScraperConfig.Instance.Username));
                sb.Append("&sspassword=");
                sb.Append(Uri.EscapeDataString(ScreenScraperConfig.Instance.Password));
            }
        }
    }
}