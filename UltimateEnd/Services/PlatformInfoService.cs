using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public sealed class PlatformInfoService
    {
        private const string PlatformInfoFileName = "platform_info.json";

        private static readonly Lazy<PlatformInfoService> _instance = new(() => new PlatformInfoService(), LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly PlatformDatabase _database;
        private readonly Dictionary<string, PlatformInfo> _aliasMap;
        private readonly Dictionary<string, string> _shortestAliasCache;

        public static PlatformInfoService Instance => _instance.Value;

        private PlatformInfoService()
        {
            _database = LoadDatabaseFromFile();

            var aliasMap = new Dictionary<string, PlatformInfo>(StringComparer.OrdinalIgnoreCase);
            var shortestAliasCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            BuildMaps(_database, aliasMap, shortestAliasCache);

            _aliasMap = aliasMap;
            _shortestAliasCache = shortestAliasCache;
        }

        private static string GetConfigFilePath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();
            return provider != null ? Path.Combine(provider.GetAppBaseFolder(), PlatformInfoFileName) : Path.Combine(AppContext.BaseDirectory, PlatformInfoFileName);
        }

        private static PlatformDatabase LoadDatabaseFromFile()
        {
            var configPath = GetConfigFilePath();

            if (!File.Exists(configPath))
            {
                var db = CreateDefaultDatabase();
                SaveDatabaseToFile(db, configPath);
                return db;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var db = JsonSerializer.Deserialize<PlatformDatabase>(json, options);

                if (db?.Platforms == null || db.Platforms.Count == 0) return CreateDefaultDatabase();

                return db;
            }
            catch
            {
                return CreateDefaultDatabase();
            }
        }

        private static void BuildMaps(PlatformDatabase database, Dictionary<string, PlatformInfo> aliasMap, Dictionary<string, string> shortestAliasCache)
        {
            if (database?.Platforms == null) return;

            foreach (var platform in database.Platforms)
            {
                aliasMap[platform.Id] = platform;

                foreach (var alias in platform.Aliases) aliasMap[alias] = platform;

                var allNames = new List<string> { platform.Id };
                allNames.AddRange(platform.Aliases);
                var shortest = allNames.OrderBy(n => n.Length).ThenBy(n => n).First();

                shortestAliasCache[platform.Id] = shortest;

                foreach (var alias in platform.Aliases)  shortestAliasCache[alias] = shortest;
            }
        }

        private static void SaveDatabaseToFile(PlatformDatabase database, string path)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(database, options);
                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save platform database to {path}", ex);
            }
        }

        public PlatformDatabase GetDatabase() => _database;

        public static void SaveDatabase(PlatformDatabase database)
        {
            ArgumentNullException.ThrowIfNull(database);

            var configPath = GetConfigFilePath();
            SaveDatabaseToFile(database, configPath);
        }

        private static string NormalizeInput(string input) => input.Trim().Replace(" ", string.Empty).ToLowerInvariant();

        public string GetPlatformDisplayName(string platformIdOrAlias)
        {
            if (string.IsNullOrWhiteSpace(platformIdOrAlias)) return platformIdOrAlias;

            var normalized = NormalizeInput(platformIdOrAlias);

            return _aliasMap.TryGetValue(normalized, out var platform) ? platform.DisplayName : platformIdOrAlias.Trim();
        }

        public string NormalizePlatformId(string platformName)
        {
            if (string.IsNullOrWhiteSpace(platformName)) return platformName;

            var normalized = NormalizeInput(platformName);

            return _aliasMap.TryGetValue(normalized, out var platform) ? platform.Id : normalized;
        }

        public string ExtractPlatformIdFromFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return folderName;

            var inputNormalized = NormalizeInput(folderName);

            PlatformInfo? bestMatchPlatform = null;
            int maxMatchLength = 0;

            foreach (var (alias, platform) in _aliasMap)
            {
                if (inputNormalized.StartsWith(alias, StringComparison.OrdinalIgnoreCase) && alias.Length > maxMatchLength)
                {
                    maxMatchLength = alias.Length;
                    bestMatchPlatform = platform;
                }
            }

            return bestMatchPlatform?.Id ?? inputNormalized;
        }

        public HashSet<string> GetValidExtensions(string platformId)
        {
            var normalized = NormalizePlatformId(platformId);

            if (_aliasMap.TryGetValue(normalized, out var platform)) return new HashSet<string>(platform.Extensions, StringComparer.OrdinalIgnoreCase);

            return [".zip", ".iso", ".chd"];
        }

        public PlatformInfo? GetPlatformInfo(string platformIdOrAlias)
        {
            if (string.IsNullOrWhiteSpace(platformIdOrAlias)) return null;

            var normalized = NormalizeInput(platformIdOrAlias);

            return _aliasMap.TryGetValue(normalized, out var platform) ? platform : null;
        }

        public ScreenScraperSystemId GetScreenScraperSystemId(string platformIdOrAlias)
        {
            if (string.IsNullOrWhiteSpace(platformIdOrAlias)) return ScreenScraperSystemId.NotSupported;

            var normalized = NormalizeInput(platformIdOrAlias);

            return _aliasMap.TryGetValue(normalized, out var platform) ? platform.ScreenScraperSystemId : ScreenScraperSystemId.NotSupported;
        }

        public string GetShortestAlias(string platformIdOrAlias)
        {
            if (string.IsNullOrWhiteSpace(platformIdOrAlias)) return platformIdOrAlias;

            var normalized = NormalizeInput(platformIdOrAlias);

            return _shortestAliasCache.TryGetValue(normalized, out var shortest) ? shortest : platformIdOrAlias.Trim();
        }

        public IReadOnlyList<PlatformInfo> GetAllPlatforms() => _database.Platforms.AsReadOnly();

        public bool PlatformExists(string platformIdOrAlias)
        {
            if (string.IsNullOrWhiteSpace(platformIdOrAlias)) return false;

            var normalized = NormalizeInput(platformIdOrAlias);

            return _aliasMap.ContainsKey(normalized);
        }

        private static PlatformDatabase CreateDefaultDatabase()
        {
            var db = new PlatformDatabase { Platforms = [] };

            // Arcade
            db.Platforms.Add(new PlatformInfo { Id = "mame", DisplayName = "MAME", Extensions = [".zip"], ScreenScraperSystemId = ScreenScraperSystemId.MAME });
            db.Platforms.Add(new PlatformInfo { Id = "fbneo", DisplayName = "FBNeo", Extensions = [".zip"], ScreenScraperSystemId = ScreenScraperSystemId.MAME });
            db.Platforms.Add(new PlatformInfo { Id = "neogeo", DisplayName = "Neo Geo", Extensions = [".neo", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.NeoGeo });

            // Nintendo
            db.Platforms.Add(new PlatformInfo { Id = "familycomputer", DisplayName = "Family Computer", Aliases = ["nes", "fc", "famicom"], Extensions = [".3dsen", ".fds", ".nes", ".unf", ".unif", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.NES });
            db.Platforms.Add(new PlatformInfo { Id = "superfamicom", DisplayName = "Super Famicom", Aliases = ["snes", "sfc"], Extensions = [".bin", ".bml", ".bs", ".bsx", ".dx2", ".fig", ".gd3", ".gd7", ".mgd", ".sfc", ".smc", ".st", ".swc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SNES });
            db.Platforms.Add(new PlatformInfo { Id = "gameboy", DisplayName = "Game Boy", Aliases = ["gb"], Extensions = [".bs", ".cgb", ".dmg", ".gb", ".gbc", ".sgb", ".sfc", ".smc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.GameBoy });
            db.Platforms.Add(new PlatformInfo { Id = "gameboycolor", DisplayName = "Game Boy Color", Aliases = ["gbc"], Extensions = [".gbc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.GameBoyColor });
            db.Platforms.Add(new PlatformInfo { Id = "gameboyadvance", DisplayName = "Game Boy Advance", Aliases = ["gba"], Extensions = [".agb", ".bin", ".cgb", ".dmg", ".gb", ".gba", ".gbc", ".sgb", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.GameBoyAdvance });
            db.Platforms.Add(new PlatformInfo { Id = "nintendo64", DisplayName = "Nintendo 64", Aliases = ["n64"], Extensions = [".bin", ".d64", ".n64", ".ndd", ".u1", ".v64", ".z64", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Nintendo64 });
            db.Platforms.Add(new PlatformInfo { Id = "nintendods", DisplayName = "Nintendo DS", Aliases = ["nds"], Extensions = [".app", ".bin", ".nds", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.NintendoDS });
            db.Platforms.Add(new PlatformInfo { Id = "3ds", DisplayName = "Nintendo 3DS", Aliases = ["nintendo3ds"], Extensions = [".3ds", ".3dsx", ".app", ".axf", ".cci", ".cxi", ".elf", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Nintendo3DS });
            db.Platforms.Add(new PlatformInfo { Id = "nintendogamecube", DisplayName = "GameCube", Aliases = ["gc", "ngc", "gamecube"], Extensions = [".ciso", ".wia", ".gcm", ".gcz", ".iso", ".rvz", ".tgc", ".wad", ".wbfs", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.GameCube });
            db.Platforms.Add(new PlatformInfo { Id = "wii", DisplayName = "Wii", Extensions = [".ciso", ".iso", ".gcz", ".m3u", ".wia", ".rvz", ".tgc", ".wad", ".wbfs", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Wii });
            db.Platforms.Add(new PlatformInfo { Id = "wiiu", DisplayName = "Wii U", Extensions = [".wua", ".wud", ".wuhb", ".wux"], ScreenScraperSystemId = ScreenScraperSystemId.WiiU }); 
            db.Platforms.Add(new PlatformInfo { Id = "nintendoswitch", DisplayName = "Nintendo Switch", Aliases = ["nsw", "switch"], Extensions = [".nsp", ".xci"], ScreenScraperSystemId = ScreenScraperSystemId.Switch });

            // Sega
            db.Platforms.Add(new PlatformInfo { Id = "megadrive", DisplayName = "Mega Drive", Aliases = ["md", "genesis", "segagenesis"], Extensions = [".32x", ".68k", ".bin", ".bms", ".chd", ".cue", ".gen", ".gg", ".iso", ".m3u", ".md", ".mdx", ".sg", ".sgd", ".smd", ".sms", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SegaMegaDrive });
            db.Platforms.Add(new PlatformInfo { Id = "megadrive32x", DisplayName = "Mega Drive 32X", Aliases = ["md32x", "md-32x", "megadrive32x"], Extensions = [".32x", ".68k", ".bin", ".chd", ".cue", ".gen", ".iso", ".m3u", ".md", ".smd", ".sms", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Sega32X });
            db.Platforms.Add(new PlatformInfo { Id = "megadrivecd", DisplayName = "Mega Drive CD", Aliases = ["mdcd", "md-cd", "segacd"], Extensions = [".bin", ".cue", ".chd", ".iso"], ScreenScraperSystemId = ScreenScraperSystemId.SegaCD });
            db.Platforms.Add(new PlatformInfo { Id = "gamegear", DisplayName = "Game Gear", Aliases = ["gg"], Extensions = [".68k", ".bin", ".bms", ".chd", ".col", ".cue", ".gen", ".gg", ".iso", ".m3u", ".md", ".mdx", ".rom", ".sg", ".sgd", ".smd", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.GameGear });
            db.Platforms.Add(new PlatformInfo { Id = "segasaturn", DisplayName = "Sega Saturn", Aliases = ["ss", "saturn"], Extensions = [".bin", ".cue", ".chd", ".m3u", ".iso"], ScreenScraperSystemId = ScreenScraperSystemId.SegaSaturn });
            db.Platforms.Add(new PlatformInfo { Id = "dreamcast", DisplayName = "Dreamcast", Aliases = ["dc"], Extensions = [".cdi", ".chd", ".cue", ".dat", ".elf", ".gdi", ".iso", ".lst", ".m3u", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SegaDreamcast });
            db.Platforms.Add(new PlatformInfo { Id = "mastersystem", DisplayName = "Sega Master System", Extensions = [".68k", ".bin", ".chd", ".cue", ".iso", ".m3u", ".gg", ".gen", ".md", ".mdx", ".rom", ".sg", ".sgd", ".smd", ".sms", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SegaMasterSystem });
            db.Platforms.Add(new PlatformInfo { Id = "seganaomi", DisplayName = "NAOMI", Aliases = ["naomi"], Extensions = [".bin", ".dat", ".elf", ".lst", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SegaNaomi });
            db.Platforms.Add(new PlatformInfo { Id = "seganaomi2", DisplayName = "NAOMI 2", Aliases = ["naomi2"], Extensions = [".bin", ".dat", ".elf", ".lst", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.SegaNaomi2 });

            // Sony
            db.Platforms.Add(new PlatformInfo { Id = "playstation", DisplayName = "PlayStation", Aliases = ["ps", "ps1", "psx"], Extensions = [".bin", ".cue", ".ccd", ".m3u", ".img", ".mdf", ".iso", ".pbp", ".chd", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.PlayStation });
            db.Platforms.Add(new PlatformInfo { Id = "playstation2", DisplayName = "PlayStation 2", Aliases = ["ps2"], Extensions = [".bin", ".chd", ".ciso", ".cso", ".elf", ".gz", ".m3u", ".mdf", ".img", ".iso", ".isz", ".nrg"], ScreenScraperSystemId = ScreenScraperSystemId.PlayStation2 });
            db.Platforms.Add(new PlatformInfo { Id = "playstation3", DisplayName = "PlayStation 3", Aliases = ["ps3"], Extensions = [".iso", ".ps3"], ScreenScraperSystemId = ScreenScraperSystemId.PlayStation3 });
            db.Platforms.Add(new PlatformInfo { Id = "playstationportable", DisplayName = "PlayStation Portable", Aliases = ["psp"], Extensions = [".bin", ".cue", ".iso", ".cso", ".pbp", ".chd", ".elf", ".prx"], ScreenScraperSystemId = ScreenScraperSystemId.PSP });
            db.Platforms.Add(new PlatformInfo { Id = "playstationvita", DisplayName = "PlayStation Vita", Aliases = ["psv", "psvita", "vita"], Extensions = [".psvita", ".vpk"], ScreenScraperSystemId = ScreenScraperSystemId.PSVita });

            // NEC
            db.Platforms.Add(new PlatformInfo { Id = "pcengine", DisplayName = "PC Engine", Aliases = ["pce", "turbografx-16", "turbografx16"], Extensions = [".pce", ".zip", ".ccd", ".chd", ".cue", ".img", ".m3u", ".rom", ".sgx", ".toc"], ScreenScraperSystemId = ScreenScraperSystemId.PCEngine });
            db.Platforms.Add(new PlatformInfo { Id = "pcenginecd", DisplayName = "PC Engine CD", Aliases = ["pcecd", "pce-cd", "turbografx-cd", "turbografxcd"], Extensions = [".ccd", ".cue", ".chd", ".img", ".iso", ".m3u", ".pce", ".sgx", ".toc"], ScreenScraperSystemId = ScreenScraperSystemId.PCEngineCD });
            db.Platforms.Add(new PlatformInfo { Id = "pcfx", DisplayName = "PC-FX", Aliases = ["pc-fx"], Extensions = [".ccd", ".chd", ".cue", ".m3u", ".toc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.PCFX });
            db.Platforms.Add(new PlatformInfo { Id = "supergrafx", DisplayName = "PC Engine SuperGrafx", Extensions = [".ccd", ".chd", ".cue", ".pce", ".rom", ".sgx", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.PCEngineSuperGrafx });
            db.Platforms.Add(new PlatformInfo { Id = "pc98", DisplayName = "NEC PC-9801", Aliases = ["pc-98"], Extensions = [".2hd", ".88d", ".98d", ".d88", ".d98", ".cmd", ".dup", ".fdd", ".fdi", ".hdd", ".hdi", ".hdm", ".hdn", ".m3u", ".nhd", ".tfd", ".thd", ".xdf", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.PC9801 });

            // Microsoft
            db.Platforms.Add(new PlatformInfo { Id = "xbox", DisplayName = "Xbox", Extensions = [".iso", ".xiso",], ScreenScraperSystemId = ScreenScraperSystemId.Xbox });
            db.Platforms.Add(new PlatformInfo { Id = "xbox360", DisplayName = "Xbox 360", Extensions = [".iso", ".xex"], ScreenScraperSystemId = ScreenScraperSystemId.Xbox360 });
            db.Platforms.Add(new PlatformInfo { Id = "xboxone", DisplayName = "Xbox One", Extensions = [".xvd", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.XboxOne });

            // Other
            db.Platforms.Add(new PlatformInfo { Id = "atomiswave", DisplayName = "Atomiswave", Extensions = [".bin", ".dat", ".elf", ".lst", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Atomiswave });
            db.Platforms.Add(new PlatformInfo { Id = "wonderswan", DisplayName = "WonderSwan", Aliases = ["ws"], Extensions = [".pc2", ".ws", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.WonderSwan });
            db.Platforms.Add(new PlatformInfo { Id = "wonderswancolor", DisplayName = "WonderSwan Color", Aliases = ["wsc"], Extensions = [".pc2", ".wsc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.WonderSwanColor });
            db.Platforms.Add(new PlatformInfo { Id = "neogeopocket", DisplayName = "Neo Geo Pocket", Aliases = ["ngp"], Extensions = [".ngc", ".ngp", ".ngpc", ".npc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.NeoGeoPocket });
            db.Platforms.Add(new PlatformInfo { Id = "neogeopocketcolor", DisplayName = "Neo Geo Pocket Color", Aliases = ["ngpc"], Extensions = [".ngc", ".ngp", ".ngpc", ".npc", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.NeoGeoPocketColor });
            db.Platforms.Add(new PlatformInfo { Id = "3do", DisplayName = "3DO", Extensions = [".iso", ".cue", ".bin", ".chd", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.Panasonic3DO });
            db.Platforms.Add(new PlatformInfo { Id = "msx", DisplayName = "MSX", Extensions = [".rom", ".mx1", ".mx2", ".dsk", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.MSX });
            db.Platforms.Add(new PlatformInfo { Id = "msx2", DisplayName = "MSX2", Extensions = [".rom", ".mx2", ".dsk", ".zip"], ScreenScraperSystemId = ScreenScraperSystemId.MSX2 });

            db.Platforms.Add(new PlatformInfo { Id = "dos", DisplayName = "DOS", Extensions = [".zip", ".dosz"], ScreenScraperSystemId = ScreenScraperSystemId.PCDos });
            db.Platforms.Add(new PlatformInfo { Id = "windows", DisplayName = "Windows", Aliases = ["win"], Extensions = [".zip", ".dosz"], ScreenScraperSystemId = ScreenScraperSystemId.PCWin9x }); 
            db.Platforms.Add(new PlatformInfo { Id = "easyrpg", DisplayName = "EasyRPG", Extensions = [".zip", ".easyrpg"], ScreenScraperSystemId = ScreenScraperSystemId.EasyRPG });
            db.Platforms.Add(new PlatformInfo { Id = "rpgmakerxp", DisplayName = "RPG Maker XP", Extensions = [".xp"], ScreenScraperSystemId = ScreenScraperSystemId.EasyRPG });
            db.Platforms.Add(new PlatformInfo { Id = "pico8", DisplayName = "PICO-8 Fantasy Console", Aliases = ["pico8"], Extensions = [".p8", ".png"], ScreenScraperSystemId = ScreenScraperSystemId.Pico8 });

            db.Platforms.Add(new PlatformInfo { Id = "steam", DisplayName = "Steam", Extensions = [".steam"], ScreenScraperSystemId = ScreenScraperSystemId.NotSupported});

            return db;
        }
    }
}