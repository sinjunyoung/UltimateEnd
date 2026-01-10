using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UltimateEnd.Scraper.Helpers;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper
{
    public static class ScreenScraperXmlParser
    {
        #region Public Methods

        public static GameResult ParseGameInfo(string xmlContent)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                var gameNode = doc.SelectSingleNode("//Data/jeu");

                return ParseGameNode(gameNode);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Private Parsing Methods

        private static GameResult ParseGameNode(XmlNode gameNode)
        {
            try
            {
                string[] preferredRegions = ScreenScraperConfig.Instance.GetPreferredRegions();
                string preferredLanguage = ScreenScraperConfig.Instance.PreferredLanguage.ToString();

                var idAttr = gameNode?.Attributes?["id"];

                if (idAttr == null || !int.TryParse(idAttr.Value, out int id))
                    return null;

                var result = new GameResult
                {
                    Id = id,
                    Title = GetLocalizedText(gameNode, "noms/nom", preferredLanguage) ?? "Unknown",
                    Description = GetLocalizedText(gameNode, "synopsis/synopsis", preferredLanguage) ?? string.Empty,
                    ReleaseDate = GetNodeText(gameNode, "dates/date") ?? string.Empty,
                    Developer = GetNodeText(gameNode, "developpeur") ?? string.Empty,
                    Publisher = GetNodeText(gameNode, "editeur") ?? string.Empty,
                    Genre = ParseGenre(gameNode),
                    Players = GetNodeText(gameNode, "joueurs") ?? string.Empty
                };

                var noteText = GetNodeText(gameNode, "note");

                if (!string.IsNullOrEmpty(noteText) &&
                    float.TryParse(noteText,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float note))
                    result.Rating = note / 20.0f;

                var mediaList = gameNode.SelectSingleNode("medias");

                if (mediaList != null)
                    ParseMediaUrlsWithRegion(mediaList, result.Media, preferredRegions);

                return result;
            }
            catch
            {
                return null;
            }
        }

        private static string ParseGenre(XmlNode gameNode)
        {
            var genreNodes = gameNode.SelectNodes("genres/genre");

            if (genreNodes == null || genreNodes.Count == 0)
                return string.Empty;

            var seenIds = new HashSet<int>();

            foreach (XmlNode genreNode in genreNodes)
            {
                var genreIdAttr = genreNode.Attributes?["id"];

                if (genreIdAttr != null && int.TryParse(genreIdAttr.Value, out int genreId))
                {
                    if (seenIds.Add(genreId))
                    {
                        var koreanGenre = ScreenScraperGenre.GetKoreanName(genreId);
                        if (koreanGenre != ScreenScraperGenre.UnknownGenreKorean)
                            return koreanGenre;
                    }
                }
            }

            return string.Empty;
        }

        private static void ParseMediaUrlsWithRegion(XmlNode mediaList, Dictionary<string, MediaInfo> media, string[] preferredRegions)
        {
            var mediaNodeCache = new Dictionary<string, List<XmlNode>>();

            var allMediaTypes = new[] { "wheel", "wheel-hd", "wheel-steel", "box-3D", "box-2D", "video-normalized", "video" };

            foreach (var mediaType in allMediaTypes)
            {
                var nodes = mediaList?.SelectNodes($"media[@type='{mediaType}']");
                if (nodes != null && nodes.Count > 0)
                    mediaNodeCache[mediaType] = [.. nodes.Cast<XmlNode>()];
            }

            string[] logoKey;
            if (ScreenScraperConfig.Instance.LogoImage == Enums.LogoImageType.Normal)
                logoKey = ["wheel", "wheel-hd", "wheel-steel"];
            else
                logoKey = ["wheel-steel", "wheel", "wheel-hd"];

            var logo = GetMediaInfoByRegionCached(mediaNodeCache, logoKey, preferredRegions);
            if (logo != null)
                media[MediaDownloader.MediaKeyLogo] = logo;

            string[] boxKey;
            if (ScreenScraperConfig.Instance.CoverImage == Enums.CoverImageType.Box3D)
                boxKey = ["box-3D", "box-2D"];
            else
                boxKey = ["box-2D", "box-3D"];

            var box = GetMediaInfoByRegionCached(mediaNodeCache, boxKey, preferredRegions);
            if (box != null)
                media[MediaDownloader.MediaKeyBoxFront] = box;

            var video = GetMediaInfoByRegionCached(mediaNodeCache, ["video-normalized", "video"], preferredRegions);
            if (video != null)
                media[MediaDownloader.MediaKeyVideo] = video;
        }

        #endregion

        #region Helper Methods

        private static string GetNodeText(XmlNode parent, string xpath) => parent?.SelectSingleNode(xpath)?.InnerText;

        private static string GetLocalizedText(XmlNode parentNode, string xpath, string preferredLanguage)
        {
            var nodes = parentNode?.SelectNodes(xpath);

            if (nodes == null || nodes.Count == 0)
                return null;

            if (xpath.Contains("noms/nom"))
            {
                foreach (XmlNode node in nodes)
                {
                    var regionAttr = node.Attributes?["region"]?.Value;
                    if (regionAttr == preferredLanguage)
                        return node.InnerText;
                }
            }
            else
            {
                foreach (XmlNode node in nodes)
                {
                    var langAttr = node.Attributes?["langue"]?.Value;
                    if (langAttr == preferredLanguage)
                        return node.InnerText;
                }
            }

            return nodes[0]?.InnerText;
        }

        private static MediaInfo GetMediaInfoByRegionCached(Dictionary<string, List<XmlNode>> mediaNodeCache, string[] mediaTypes, string[] preferredRegions)
        {
            foreach (var region in preferredRegions)
            {
                foreach (var mediaType in mediaTypes)
                {
                    if (!mediaNodeCache.TryGetValue(mediaType, out var nodes))
                        continue;

                    foreach (var node in nodes)
                    {
                        var nodeRegion = node.Attributes?["region"]?.Value;

                        if (!string.IsNullOrEmpty(nodeRegion) &&
                            nodeRegion.Equals(region, System.StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(node.InnerText))
                        {
                            return new MediaInfo
                            {
                                Url = node.InnerText.Replace(" ", "%20"),
                                Format = node.Attributes?["format"]?.Value ?? "png"
                            };
                        }
                    }
                }
            }

            foreach (var mediaType in mediaTypes)
            {
                if (mediaNodeCache.TryGetValue(mediaType, out var nodes) && nodes.Count > 0)
                {
                    var node = nodes[0];
                    if (!string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        return new MediaInfo
                        {
                            Url = node.InnerText.Replace(" ", "%20"),
                            Format = node.Attributes?["format"]?.Value ?? "png"
                        };
                    }
                }
            }

            return null;
        }

        #endregion
    }
}