using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace UltimateEnd.Services
{
    public abstract class BaseCommandConfigService : ICommandConfigService
    {
        protected ICommandConfig? _config;
        protected readonly Lock _lock = new();

        protected abstract string GetConfigDirectory();

        protected abstract ICommandConfig CreateDefaultConfig();

        protected abstract void LogInfo(string message);

        protected abstract void LogWarn(string message);

        protected abstract void LogError(string message);

        protected abstract IEmulatorCommand CreateRetroArchCommand(string coreName, string displayName, params string[] platforms);

        protected abstract ICommandConfig ParseConfigFromData(Dictionary<string, Dictionary<string, string>> data);

        protected abstract Dictionary<string, Dictionary<string, string>> SerializeConfigToData(ICommandConfig config);

        protected void RegisterCommonEmulators(ICommandConfig config)
        {
            // Game Boy / Game Boy Color
            config.AddEmulator(CreateRetroArchCommand("gambatte", "Gambatte", "gb", "gbc"));
            config.AddEmulator(CreateRetroArchCommand("sameboy", "SameBoy", "gb", "gbc"));
            config.AddEmulator(CreateRetroArchCommand("gearboy", "Gearboy", "gb", "gbc"));
            config.AddEmulator(CreateRetroArchCommand("tgbdual", "TGB Dual", "gb", "gbc"));
            config.AddEmulator(CreateRetroArchCommand("DoubleCherryGB", "DoubleCherryGB", "gb", "gbc"));
            config.AddEmulator(CreateRetroArchCommand("mesen-s", "Mesen-S", "gb", "gbc", "sfc", "fc"));
            config.AddEmulator(CreateRetroArchCommand("bsnes", "bsnes", "gb", "gbc", "gba", "sfc", "fc", "md", "mastersystem", "gg", "pce", "ws", "wsc"));
            config.AddEmulator(CreateRetroArchCommand("vbam", "VBA-M", "gb", "gbc", "gba"));

            // Game Boy Advance
            config.AddEmulator(CreateRetroArchCommand("mgba", "mGBA", "gb", "gbc", "gba"));
            config.AddEmulator(CreateRetroArchCommand("vba_next", "Vba_Next", "gba"));
            config.AddEmulator(CreateRetroArchCommand("gpsp", "gpSP", "gba"));
            config.AddEmulator(CreateRetroArchCommand("noods", "NooDS", "gba", "nds"));

            // Famicom / Super Famicom
            config.AddEmulator(CreateRetroArchCommand("mesen", "Mesen", "fc", "sfc", "gb", "gbc", "gba", "pce", "gg", "ws", "wsc"));
            config.AddEmulator(CreateRetroArchCommand("fceumm", "Fceumm", "fc"));
            config.AddEmulator(CreateRetroArchCommand("snes9x", "Snes9x", "sfc"));

            // GameCube
            config.AddEmulator(CreateRetroArchCommand("dolphin", "Dolphin", "gc", "wii"));

            // NDS
            config.AddEmulator(CreateRetroArchCommand("melondsds", "melonDS DS", "nds"));
            config.AddEmulator(CreateRetroArchCommand("melonds", "melonDS", "nds"));
            config.AddEmulator(CreateRetroArchCommand("desmume", "DeSmuME", "nds"));
            config.AddEmulator(CreateRetroArchCommand("desmume2015", "DeSmuME 2015", "nds"));

            // N64
            config.AddEmulator(CreateRetroArchCommand("mupen64plus_next", "Mupen64Plus-Next", "n64"));
            config.AddEmulator(CreateRetroArchCommand("parallel_n64", "ParaLLEl N64", "n64"));

            // 3DS
            config.AddEmulator(CreateRetroArchCommand("citra", "Citra", "3ds"));

            // Mega Drive
            config.AddEmulator(CreateRetroArchCommand("genesis_plus_gx", "Genesis Plus GX", "md", "mastersystem"));
            config.AddEmulator(CreateRetroArchCommand("genesis_plus_gx_wide", "Genesis Plus GX Wide", "md", "mastersystem"));
            config.AddEmulator(CreateRetroArchCommand("picodrive", "PicoDrive", "md", "md32x", "mastersystem"));

            // MAME
            config.AddEmulator(CreateRetroArchCommand("mamearcade", "MAME - Current", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("mame2010", "MAME 2010", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("mame2003_plus", "MAME 2003-Plus", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("mame2003", "MAME 2003", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("mame2000", "MAME 2000", "neogeo", "mame", "fbneo"));

            // FBNeo
            config.AddEmulator(CreateRetroArchCommand("fbneo", "FBNeo", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("fbneo_crcskip", "FBNeo No CRC Check ", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("fbalpha2012", "FB Alpha 2012", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("geolith", "Geolith", "neogeo", "mame", "fbneo"));
            config.AddEmulator(CreateRetroArchCommand("flycast", "Flycast", "neogeo", "mame", "fbneo", "naomi", "naomi2", "dc", "atomiswave"));
            config.AddEmulator(CreateRetroArchCommand("dice", "DICE", "neogeo", "mame", "fbneo"));

            // PS1
            config.AddEmulator(CreateRetroArchCommand("mednafen_psx", "Beetle PSX", "ps1"));
            config.AddEmulator(CreateRetroArchCommand("mednafen_psx_hw", "Beetle PSX HW", "ps1"));
            config.AddEmulator(CreateRetroArchCommand("pcsx_rearmed", "PCSX-ReARMed", "ps1"));
            config.AddEmulator(CreateRetroArchCommand("swanstation", "SwanStation", "ps1"));

            // PSP
            config.AddEmulator(CreateRetroArchCommand("ppsspp", "PPSSPP", "psp"));

            // Sega Saturn
            config.AddEmulator(CreateRetroArchCommand("yabause", "Yabause", "saturn"));
            config.AddEmulator(CreateRetroArchCommand("yabasanshiro", "YabaSanshiro", "saturn"));
            config.AddEmulator(CreateRetroArchCommand("mednafen_saturn", "Beetle Saturn", "saturn"));

            config.AddEmulator(CreateRetroArchCommand("gearsystem", "Gearsystem", "gamegear", "mastersystem"));

            // msx
            config.AddEmulator(CreateRetroArchCommand("fmsx", "fMSX", "msx", "msx2"));
            config.AddEmulator(CreateRetroArchCommand("bluemsx", "blueMSX", "msx", "msx2"));

            // pce, pce cd
            config.AddEmulator(CreateRetroArchCommand("mednafen_pce_fast", "Beetle PCE FAST", "pce", "pcecd", "pcfx"));
            config.AddEmulator(CreateRetroArchCommand("mednafen_supergrafx", "Beetle SuperGrafx", "pce", "supergrafx"));

            // neogeo
            config.AddEmulator(CreateRetroArchCommand("mednafen_ngp", "Beetle NeoPop", "ngp", "ngpc"));
            config.AddEmulator(CreateRetroArchCommand("race", "RACE", "ngp", "ngpc"));

            // pc98
            config.AddEmulator(CreateRetroArchCommand("nekop2", "Neko Project II", "pc98"));
            config.AddEmulator(CreateRetroArchCommand("np2kai", "Neko Project II Kai", "pc98"));

            // ETC            
            config.AddEmulator(CreateRetroArchCommand("opera", "Opera", "3do"));
            config.AddEmulator(CreateRetroArchCommand("dosbox_pure", "DOSBox-pure", "dos", "windows"));
            config.AddEmulator(CreateRetroArchCommand("mednafen_wswan", "Beetle Cygne", "ws", "wsc"));
            config.AddEmulator(CreateRetroArchCommand("easyrpg", "EasyRPG", "easyrpg"));
            config.AddEmulator(CreateRetroArchCommand("retro8", "Retro8", "pico8"));
        }

        protected static void SetCommonDefaultEmulators(ICommandConfig config)
        {
            config.DefaultEmulators["gameboy"] = "retroarch_gambatte";
            config.DefaultEmulators["gameboycolor"] = "retroarch_gambatte";
            config.DefaultEmulators["gameboyadvance"] = "retroarch_mgba";
            config.DefaultEmulators["nintendogamecube"] = "dolphin";
            config.DefaultEmulators["familycomputer"] = "retroarch_fceumm";
            config.DefaultEmulators["superfamicom"] = "retroarch_snes9x";
            config.DefaultEmulators["mame"] = "retroarch_mame2003_plus";
            config.DefaultEmulators["fbneo"] = "retroarch_fbneo_crcskip";
            config.DefaultEmulators["neogeo"] = "retroarch_fbneo_crcskip";
            config.DefaultEmulators["playstation"] = "retroarch_pcsx_rearmed";
            config.DefaultEmulators["playstationportable"] = "ppsspp";
        }

        public ICommandConfig LoadConfig()
        {
            if (_config != null)
                return _config;

            lock (_lock)
            {
                if (_config != null)
                    return _config;

                var configPath = Path.Combine(GetConfigDirectory(), "commands.txt");

                if (!File.Exists(configPath))
                {
                    LogInfo("Config file not found, creating default config");
                    _config = CreateDefaultConfig();
                    SaveConfigInternal(_config);
                    return _config;
                }

                try
                {
                    var data = ConfigFileParser.Parse(configPath);
                    _config = ParseConfigFromData(data);

                    if (_config == null || _config.Emulators == null || _config.Emulators.Count == 0)
                    {
                        LogWarn("Parsed config is invalid, using default config");
                        _config = CreateDefaultConfig();
                        return _config;
                    }

                    return _config;
                }
                catch (Exception ex)
                {
                    LogError($"Config parsing error: {ex.Message}");
                    _config = CreateDefaultConfig();
                    BackupCorruptedConfig(configPath);
                    SaveConfigInternal(_config);
                    return _config;
                }
            }
        }

        public void SaveConfig(ICommandConfig config)
        {
            if (config == null)
            {
                LogWarn("Config is null, skipping save");
                return;
            }

            lock (_lock)
            {
                SaveConfigInternal(config);
            }
        }

        protected void SaveConfigInternal(ICommandConfig config)
        {
            try
            {
                var directory = GetConfigDirectory();

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    LogInfo($"Created directory: {directory}");
                }

                var configPath = Path.Combine(directory, "commands.txt");
                var tempPath = configPath + ".tmp";

                var data = SerializeConfigToData(config);
                ConfigFileParser.Write(tempPath, data);

                if (File.Exists(configPath))
                    File.Delete(configPath);

                File.Move(tempPath, configPath);

                _config = config;
                LogInfo($"Config saved successfully to {configPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Permission denied saving config: {ex.Message}");
                throw new InvalidOperationException("저장소 접근 권한이 없습니다.", ex);
            }
            catch (IOException ex)
            {
                LogError($"I/O error saving config: {ex.Message}");
                throw new InvalidOperationException("설정 파일 저장 중 오류가 발생했습니다.", ex);
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error saving config: {ex.Message}");
                throw;
            }
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _config = null;
                LogInfo("Config cache cleared");
            }
        }

        protected void BackupCorruptedConfig(string configPath)
        {
            try
            {
                var backupPath = $"{configPath}.corrupted.{DateTime.Now:yyyyMMddHHmmss}";

                if (File.Exists(configPath))
                {
                    File.Copy(configPath, backupPath, true);
                    LogInfo($"Corrupted config backed up to: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                LogWarn($"Failed to backup corrupted config: {ex.Message}");
            }
        }
    }
}