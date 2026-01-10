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

            try
            {
                // XML 표준 위반 파일이므로 XmlReader로 순차 읽기
                using var reader = XmlReader.Create(filePath, new XmlReaderSettings
                {
                    ConformanceLevel = ConformanceLevel.Fragment, // 다중 루트 허용
                    IgnoreWhitespace = true
                });

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "game")
                    {
                        var game = ParseGame(reader, basePath);
                        if (game != null)
                            result.Add(game);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        private static GameMetadata ParseGame(XmlReader reader, string basePath)
        {
            var game = new GameMetadata();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "game")
                    break;

                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                var elementName = reader.Name.ToLower();

                if (!reader.Read() || reader.NodeType != XmlNodeType.Text)
                    continue;

                var value = reader.Value.Trim();

                switch (elementName)
                {
                    case "path":
                        if (value.StartsWith("./"))
                            value = value.Substring(2);
                        else if (value.StartsWith(".\\"))
                            value = value.Substring(2);
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
                return game;
            }

            return null;
        }
    }
}