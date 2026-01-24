using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class EsDeMetadataParser
    {
        public static List<GameMetadata> Parse(string filePath)
        {
            var result = new List<GameMetadata>();
            var basePath = Path.GetDirectoryName(filePath);
            var mediaBasePath = GetMediaBasePath(filePath);

            try
            {
                using var reader = XmlReader.Create(filePath, new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Fragment,
                    IgnoreWhitespace = true
                });

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "game")
                    {
                        var game = ParseGame(reader, basePath, mediaBasePath);

                        if (game != null) result.Add(game);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        private static string GetMediaBasePath(string gamelistPath)
        {
            var gamelistDir = Path.GetDirectoryName(gamelistPath);
            var systemName = Path.GetFileName(gamelistDir);
            var esdeRoot = Path.GetDirectoryName(Path.GetDirectoryName(gamelistDir));

            return Path.Combine(esdeRoot, "downloaded_media", systemName);
        }

        private static GameMetadata ParseGame(XmlReader reader, string basePath, string mediaBasePath)
        {
            var game = new GameMetadata();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "game") break;
                if (reader.NodeType != XmlNodeType.Element) continue;

                var elementName = reader.Name.ToLower();

                if (!reader.Read() || reader.NodeType != XmlNodeType.Text) continue;

                var value = reader.Value.Trim();

                switch (elementName)
                {
                    case "path":
                        if (value.StartsWith("./"))
                            value = value[2..];
                        else if (value.StartsWith(".\\"))
                            value = value[2..];
                        game.RomFile = Path.GetFileName(value);
                        break;
                    case "name":
                        game.Title = value;
                        break;
                    case "desc":
                        game.Description = value;
                        break;
                    case "developer":
                        game.Developer = value;
                        break;
                    case "genre":
                        game.Genre = value;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(game.RomFile))
            {
                game.SetBasePath(basePath);
                SetDefaultMediaPaths(game, mediaBasePath);
                return game;
            }

            return null;
        }

        private static void SetDefaultMediaPaths(GameMetadata game, string mediaBasePath)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(game.RomFile);

            // CoverImagePath: covers 우선, 없으면 3dboxes
            var coverPng = Path.Combine(mediaBasePath, "covers", $"{fileNameWithoutExt}.png");
            //var coverJpg = Path.Combine(mediaBasePath, "covers", $"{fileNameWithoutExt}.jpg");
            //var box3dPng = Path.Combine(mediaBasePath, "3dboxes", $"{fileNameWithoutExt}.png");
            //var box3dJpg = Path.Combine(mediaBasePath, "3dboxes", $"{fileNameWithoutExt}.jpg");

            game.CoverImagePath = coverPng; // 기본값으로 covers/png 설정
                                            // 실제로는 covers/jpg, 3dboxes/png, 3dboxes/jpg 순으로 fallback 가능

            // LogoImagePath: marquees
            var marqueePng = Path.Combine(mediaBasePath, "marquees", $"{fileNameWithoutExt}.png");
            //var marqueeJpg = Path.Combine(mediaBasePath, "marquees", $"{fileNameWithoutExt}.jpg");
            game.LogoImagePath = marqueePng; // 기본값으로 png 설정

            // VideoPath: videos
            game.VideoPath = Path.Combine(mediaBasePath, "videos", $"{fileNameWithoutExt}.mp4");
        }
    }
}