using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class PegasusMetadataParser
    {
        private static readonly Regex RxAssetKey = new Regex(@"^assets?\.(.+)$", RegexOptions.Compiled);
        private static readonly Regex RxCountRange = new Regex(@"^(\d+)(-(\d+))?$", RegexOptions.Compiled);
        private static readonly Regex RxPercent = new Regex(@"^\d+%$", RegexOptions.Compiled);
        private static readonly Regex RxFloat = new Regex(@"^\d+(\.\d+)?$", RegexOptions.Compiled);
        private static readonly Regex RxDate = new Regex(@"^(\d{4})(-(\d{1,2}))?(-(\d{1,2}))?$", RegexOptions.Compiled);
        private static readonly Regex RxUnescapedNewline = new Regex(@"(?<!\\)\\n", RegexOptions.Compiled);
        private static readonly Regex RxUri = new Regex(@"^[a-zA-Z][a-zA-Z0-9+\-.]+:.+", RegexOptions.Compiled);

        private static readonly HashSet<string> CollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "shortname", "launch", "command", "workdir", "cwd",
            "directory", "directories", "extension", "extensions",
            "file", "files", "regex",
            "ignore-extension", "ignore-extensions", "ignore-file", "ignore-files", "ignore-regex",
            "summary", "description", "sortby", "sort_by", "sort-by"
        };

        private static readonly HashSet<string> GameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file", "files", "launch", "command", "workdir", "cwd",
            "developer", "developers", "publisher", "publishers",
            "genre", "genres", "tag", "tags", "players",
            "summary", "description", "release", "rating",
            "sorttitle", "sortname", "sort_title", "sort_name", "sort-title", "sort-name",
            "sortby", "sort_by", "sort-by"
        };

        public static List<GameMetadata> Parse(string filePath)
        {
            var fullMetadata = ParseFull(filePath);
            return ConvertToGameMetadata(fullMetadata, Path.GetDirectoryName(filePath));
        }

        public static PegasusMetadataFile ParseFull(string filePath)
        {
            var result = new PegasusMetadataFile();
            var platformPath = Path.GetDirectoryName(filePath);

            var lines = File.ReadAllLines(filePath);

            PegasusCollectionMetadata currentCollection = null;
            PegasusGameMetadata currentGame = null;

            string lastKey = null;
            var currentValues = new List<string>();

            void FlushEntry()
            {
                if (lastKey != null && currentValues.Count > 0)
                {
                    if (currentGame != null)
                        ApplyGameEntry(currentGame, lastKey, currentValues, platformPath);
                    else if (currentCollection != null)
                        ApplyCollectionEntry(currentCollection, lastKey, currentValues, platformPath);

                    currentValues.Clear();
                }
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var colonIndex = line.IndexOf(':');

                if (colonIndex > 0)
                {
                    var key = line.Substring(0, colonIndex).Trim().ToLower();
                    var value = line.Substring(colonIndex + 1).Trim();

                    if (key == "collection" || key == "game" || CollectionKeys.Contains(key) || GameKeys.Contains(key) || key.StartsWith("x-") || RxAssetKey.IsMatch(key))
                        FlushEntry();

                    if (key == "collection")
                    {
                        currentCollection = new PegasusCollectionMetadata { Name = value };
                        currentGame = null;
                        result.Collections.Add(currentCollection);
                        lastKey = null;
                        continue;
                    }

                    if (key == "game")
                    {
                        currentGame = new PegasusGameMetadata { Title = value };
                        result.Games.Add(currentGame);
                        lastKey = null;
                        continue;
                    }

                    lastKey = key;
                    if (!string.IsNullOrEmpty(value))
                        currentValues.Add(value);
                }
                else if (!string.IsNullOrWhiteSpace(line) && lastKey != null)
                {
                    currentValues.Add(line);
                }
            }

            FlushEntry();
            return result;
        }

        private static List<GameMetadata> ConvertToGameMetadata(PegasusMetadataFile pegasus, string basePath)
        {
            var result = new List<GameMetadata>();

            foreach (var pg in pegasus.Games)
            {
                if (pg.Files.Count > 0)
                {
                    foreach (var file in pg.Files)
                    {
                        var game = new GameMetadata
                        {
                            Title = pg.Title,
                            RomFile = Path.GetFileName(file),
                            Developer = pg.Developers.FirstOrDefault() ?? string.Join(", ", pg.Developers),
                            Genre = pg.Genres.FirstOrDefault() ?? string.Join(", ", pg.Genres),
                            Description = pg.Description ?? pg.Summary
                        };

                        if (pg.Assets.ContainsKey(AssetType.BoxFront))
                            game.CoverImagePath = pg.Assets[AssetType.BoxFront].FirstOrDefault();

                        if (pg.Assets.ContainsKey(AssetType.Video))
                            game.VideoPath = pg.Assets[AssetType.Video].FirstOrDefault();

                        if (pg.Assets.ContainsKey(AssetType.Logo))
                            game.LogoImagePath = pg.Assets[AssetType.Logo].FirstOrDefault();

                        game.SetBasePath(basePath);
                        result.Add(game);
                    }
                }
                else
                {
                    var game = new GameMetadata
                    {
                        Title = pg.Title,
                        Developer = pg.Developers.FirstOrDefault() ?? string.Join(", ", pg.Developers),
                        Genre = pg.Genres.FirstOrDefault() ?? string.Join(", ", pg.Genres),
                        Description = pg.Description ?? pg.Summary
                    };

                    if (pg.Assets.ContainsKey(AssetType.BoxFront))
                        game.CoverImagePath = pg.Assets[AssetType.BoxFront].FirstOrDefault();

                    if (pg.Assets.ContainsKey(AssetType.Video))
                        game.VideoPath = pg.Assets[AssetType.Video].FirstOrDefault();

                    if (pg.Assets.ContainsKey(AssetType.Logo))
                        game.LogoImagePath = pg.Assets[AssetType.Logo].FirstOrDefault();

                    game.SetBasePath(basePath);
                    result.Add(game);
                }
            }

            var ignoreFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var collection in pegasus.Collections)
            {
                foreach (var ignoreFile in collection.IgnoreFiles)
                    ignoreFiles.Add(Path.GetFileName(ignoreFile));
            }

            foreach(var ignoreFile in ignoreFiles)
            {
                result.Add(new GameMetadata
                {
                    RomFile = ignoreFile,
                    Ignore = true
                });
            }

            return result;
        }

        private static void ApplyCollectionEntry(PegasusCollectionMetadata coll, string key, List<string> values, string basePath)
        {
            if (values.Count == 0) return;

            var firstLine = values[0];
            var allLines = string.Join("\n", values);

            switch (key.ToLower())
            {
                case "shortname":
                    coll.ShortName = firstLine;
                    break;

                case "launch":
                case "command":
                    coll.LaunchCmd = allLines;
                    break;

                case "workdir":
                case "cwd":
                    coll.LaunchWorkdir = firstLine;
                    break;

                case "directory":
                case "directories":
                    foreach (var line in values)
                    {
                        var path = NormalizePath(line, basePath);
                        if (!string.IsNullOrEmpty(path))
                            coll.Directories.Add(path);
                    }
                    break;

                case "extension":
                case "extensions":
                    var exts = firstLine.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim().ToLower()).ToList();
                    coll.Extensions.AddRange(exts);
                    break;

                case "file":
                case "files":
                    coll.Files.AddRange(values);
                    break;

                case "ignore-extension":
                case "ignore-extensions":
                    var ignoreExts = firstLine.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim().ToLower()).ToList();
                    coll.IgnoreExtensions.AddRange(ignoreExts);
                    break;

                case "ignore-file":
                case "ignore-files":
                    coll.IgnoreFiles.AddRange(values);
                    break;

                case "regex":
                    coll.Regex = firstLine;
                    break;

                case "ignore-regex":
                    coll.IgnoreRegex = firstLine;
                    break;

                case "summary":
                    coll.Summary = ReplaceNewlines(allLines);
                    break;

                case "description":
                    coll.Description = ReplaceNewlines(allLines);
                    break;

                case "sortby":
                case "sort_by":
                case "sort-by":
                    coll.SortBy = firstLine;
                    break;

                default:
                    if (key.StartsWith("x-"))
                    {
                        var extraKey = key.Substring(2);
                        coll.ExtraFields[extraKey] = new List<string>(values);
                    }
                    else if (TryParseAsset(key, values, basePath, out var assetType, out var assetPaths))
                    {
                        if (!coll.Assets.ContainsKey(assetType))
                            coll.Assets[assetType] = new List<string>();
                        coll.Assets[assetType].AddRange(assetPaths);
                    }
                    break;
            }
        }

        private static void ApplyGameEntry(PegasusGameMetadata game, string key, List<string> values, string basePath)
        {
            if (values.Count == 0) return;

            var firstLine = values[0];
            var allLines = string.Join("\n", values);

            switch (key.ToLower())
            {
                case "file":
                case "files":
                    foreach (var line in values)
                    {
                        var path = NormalizePath(line, basePath);
                        if (!string.IsNullOrEmpty(path))
                            game.Files.Add(path);
                    }
                    break;

                case "developer":
                case "developers":
                    game.Developers.AddRange(values);
                    break;

                case "publisher":
                case "publishers":
                    game.Publishers.AddRange(values);
                    break;

                case "genre":
                case "genres":
                    game.Genres.AddRange(values);
                    break;

                case "tag":
                case "tags":
                    game.Tags.AddRange(values);
                    break;

                case "players":
                    var match = RxCountRange.Match(firstLine);
                    if (match.Success)
                    {
                        var a = int.Parse(match.Groups[1].Value);
                        var b = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : a;
                        game.PlayerCount = Math.Max(a, b);
                    }
                    break;

                case "summary":
                    game.Summary = ReplaceNewlines(allLines);
                    break;

                case "description":
                    game.Description = ReplaceNewlines(allLines);
                    break;

                case "release":
                    var dateMatch = RxDate.Match(firstLine);
                    if (dateMatch.Success)
                    {
                        var year = int.Parse(dateMatch.Groups[1].Value);
                        var month = dateMatch.Groups[3].Success ? int.Parse(dateMatch.Groups[3].Value) : 1;
                        var day = dateMatch.Groups[5].Success ? int.Parse(dateMatch.Groups[5].Value) : 1;

                        try
                        {
                            game.ReleaseDate = new DateTime(year, month, day);
                        }
                        catch { }
                    }
                    break;

                case "rating":
                    if (RxPercent.IsMatch(firstLine))
                    {
                        var percentStr = firstLine.TrimEnd('%');
                        if (float.TryParse(percentStr, out var percent))
                            game.Rating = percent / 100f;
                    }
                    else if (RxFloat.IsMatch(firstLine))
                    {
                        if (float.TryParse(firstLine, out var rating))
                            game.Rating = rating;
                    }
                    break;

                case "launch":
                case "command":
                    game.LaunchCmd = allLines;
                    break;

                case "workdir":
                case "cwd":
                    game.LaunchWorkdir = firstLine;
                    break;

                case "sorttitle":
                case "sortname":
                case "sort_title":
                case "sort_name":
                case "sort-title":
                case "sort-name":
                case "sortby":
                case "sort_by":
                case "sort-by":
                    game.SortBy = firstLine;
                    break;

                default:
                    if (key.StartsWith("x-"))
                    {
                        var extraKey = key.Substring(2);
                        game.ExtraFields[extraKey] = new List<string>(values);
                    }
                    else if (TryParseAsset(key, values, basePath, out var assetType, out var assetPaths))
                    {
                        if (!game.Assets.ContainsKey(assetType))
                            game.Assets[assetType] = new List<string>();
                        game.Assets[assetType].AddRange(assetPaths);
                    }
                    break;
            }
        }

        private static bool TryParseAsset(string key, List<string> values, string basePath,
            out AssetType assetType, out List<string> paths)
        {
            assetType = AssetType.Unknown;
            paths = new List<string>();

            var match = RxAssetKey.Match(key);
            if (!match.Success)
                return false;

            var assetKey = match.Groups[1].Value.ToLower();
            assetType = ParseAssetType(assetKey);

            if (assetType == AssetType.Unknown)
                return false;

            foreach (var value in values)
            {
                var path = NormalizeAssetPath(value, basePath);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            return true;
        }

        private static AssetType ParseAssetType(string str)
        {
            var exactMatches = new Dictionary<string, AssetType>(StringComparer.OrdinalIgnoreCase)
            {
                ["boxfront"] = AssetType.BoxFront,
                ["boxFront"] = AssetType.BoxFront,
                ["box_front"] = AssetType.BoxFront,
                ["boxart2D"] = AssetType.BoxFront,
                ["boxart2d"] = AssetType.BoxFront,
                ["cover"] = AssetType.BoxFront,

                ["boxback"] = AssetType.BoxBack,
                ["boxBack"] = AssetType.BoxBack,
                ["box_back"] = AssetType.BoxBack,

                ["boxspine"] = AssetType.BoxSpine,
                ["boxSpine"] = AssetType.BoxSpine,
                ["box_spine"] = AssetType.BoxSpine,
                ["boxside"] = AssetType.BoxSpine,
                ["boxSide"] = AssetType.BoxSpine,
                ["box_side"] = AssetType.BoxSpine,

                ["boxfull"] = AssetType.BoxFull,
                ["boxFull"] = AssetType.BoxFull,
                ["box_full"] = AssetType.BoxFull,
                ["box"] = AssetType.BoxFull,

                ["cartridge"] = AssetType.Cartridge,
                ["disc"] = AssetType.Cartridge,
                ["cart"] = AssetType.Cartridge,

                ["logo"] = AssetType.Logo,
                ["wheel"] = AssetType.Logo,

                ["marquee"] = AssetType.ArcadeMarquee,

                ["bezel"] = AssetType.ArcadeBezel,
                ["screenmarquee"] = AssetType.ArcadeBezel,
                ["border"] = AssetType.ArcadeBezel,

                ["panel"] = AssetType.ArcadePanel,

                ["cabinetleft"] = AssetType.ArcadeCabinetLeft,
                ["cabinetLeft"] = AssetType.ArcadeCabinetLeft,
                ["cabinet_left"] = AssetType.ArcadeCabinetLeft,

                ["cabinetright"] = AssetType.ArcadeCabinetRight,
                ["cabinetRight"] = AssetType.ArcadeCabinetRight,
                ["cabinet_right"] = AssetType.ArcadeCabinetRight,

                ["tile"] = AssetType.UiTile,
                ["banner"] = AssetType.UiBanner,

                ["steam"] = AssetType.UiSteamGrid,
                ["steamgrid"] = AssetType.UiSteamGrid,
                ["grid"] = AssetType.UiSteamGrid,

                ["poster"] = AssetType.Poster,
                ["flyer"] = AssetType.Poster,

                ["background"] = AssetType.Background,
                ["music"] = AssetType.Music,

                ["screenshot"] = AssetType.Screenshot,
                ["screenshots"] = AssetType.Screenshot,

                ["video"] = AssetType.Video,
                ["videos"] = AssetType.Video,

                ["titlescreen"] = AssetType.TitleScreen,
            };

            if (exactMatches.TryGetValue(str, out var type))
                return type;

            foreach (var kvp in exactMatches)
            {
                if (str.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return AssetType.Unknown;
        }

        private static string NormalizePath(string path, string basePath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith("./"))
                path = path.Substring(2);
            else if (path.StartsWith(".\\"))
                path = path.Substring(2);

            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(basePath, path);
        }

        private static string NormalizeAssetPath(string path, string basePath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return path;

            return NormalizePath(path, basePath);
        }

        private static string ReplaceNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = RxUnescapedNewline.Replace(text, "\n");
            text = text.Replace("\\\\n", "\\n");

            return text;
        }
    }
}