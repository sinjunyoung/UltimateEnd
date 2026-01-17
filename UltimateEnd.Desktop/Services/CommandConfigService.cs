using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class CommandConfigService : BaseCommandConfigService
    {
        protected override string GetConfigDirectory()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "settings");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to access config directory", ex);
            }
        }

        protected override void LogInfo(string message) => Debug.WriteLine($"[INFO] {message}");

        protected override void LogWarn(string message) => Debug.WriteLine($"[WARN] {message}");

        protected override void LogError(string message) => Debug.WriteLine($"[ERROR] {message}");

        protected override IEmulatorCommand CreateRetroArchCommand(string coreName, string displayName, params string[] platforms)
        {
            return new Command
            {
                Id = $"retroarch_{coreName}",
                Name = $"RetroArch ({displayName})",
                IsRetroArch = true,
                LaunchCommand = @"Emulators\RetroArch-Win64\retroarch.exe -L {corePath} {romPath} -f",
                CoreName = coreName,
                SupportedPlatforms = [.. platforms]
            };
        }

        protected override ICommandConfig CreateDefaultConfig()
        {
            var config = new CommandConfig { EmulatorCommands = [] };

            RegisterCommonEmulators(config);

            config.AddEmulator(new Command
            {
                Id = "mGBA",
                Name = "mGBA",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\mGBA\mGBA.exe {romPath} --fullscreen",
                SupportedPlatforms = ["gb", "gbc", "gba"]
            });

            config.AddEmulator(new Command
            {
                Id = "visualboyadvance",
                Name = "VisualBoyAdvance",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\VisualBoyAdvance\VisualBoyAdvance.exe {romPath}",
                SupportedPlatforms = ["gb", "gbc", "gba"]
            });

            config.AddEmulator(new Command
            {
                Id = "bsnes",
                Name = "BSNES",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\bsnes\bsnes.exe {romPath} --fullscreen",
                SupportedPlatforms = ["gb", "gbc", "gba", "sfc", "fc", "md", "mastersystem", "gg", "pce", "ws", "wsc"]
            });

            config.AddEmulator(new Command
            {
                Id = "mesen",
                Name = "Mesen",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\Mesen\Mesen.exe {romPath} --fullscreen",
                SupportedPlatforms = ["fc", "sfc", "gb", "gbc", "gba", "pce", "gg", "ws", "wsc"]
            });

            config.AddEmulator(new Command
            {
                Id = "desmume",
                Name = "DeSmuME",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\desmume\DeSmuME_0.9.13_x64.exe {romPath} --windowed-fullscreen",
                SupportedPlatforms = ["nds"]
            });

            config.AddEmulator(new Command
            {
                Id = "azahar",
                Name = "Azahar",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\azahar\azahar.exe {romPath}",
                SupportedPlatforms = ["3ds"]
            });

            config.AddEmulator(new Command
            {
                Id = "azahar plus",
                Name = "Azahar Plus",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\azaharplus\azahar.exe {romPath}",
                SupportedPlatforms = ["3ds"]
            });

            config.AddEmulator(new Command
            {
                Id = "ppsspp",
                Name = "PPSSPP",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\PPSSPP\PPSSPPWindows64.exe {romPath} --fullscreen",
                SupportedPlatforms = ["psp"]
            });

            config.AddEmulator(new Command
            {
                Id = "pcsx2",
                Name = "PCSX2",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\pcsx2\pcsx2-qt.exe {romPath} -fullscreen",
                SupportedPlatforms = ["ps2"]
            });

            //config.AddEmulator(new Command
            //{
            //    Id = "rpcs3",
            //    Name = "RPCS3",
            //    IsRetroArch = false,
            //    LaunchCommand = @"Emulators\rpcs3\rpcs3_launcher.bat {romPath}",
            //    SupportedPlatforms = ["ps3"]
            //});

            config.AddEmulator(new Command
            {
                Id = "vita3k",
                Name = "Vita3K",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\Vita3K\Vita3K.exe -r {romName} --fullscreen",
                SupportedPlatforms = ["vita"]
            });

            config.AddEmulator(new Command
            {
                Id = "dolphin",
                Name = "Dolphin",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\Dolphin\Dolphin.exe {romPath} --config ""Dolphin.Display.Fullscreen=True""",
                SupportedPlatforms = ["gc", "wii"]
            });

            config.AddEmulator(new Command
            {
                Id = "cemu",
                Name = "CEMU",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\cemu\Cemu.exe -g {romPath} -f",
                SupportedPlatforms = ["wiiu"]
            });

            config.AddEmulator(new Command
            {
                Id = "sudachi",
                Name = "sudachi",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\sudachi\sudachi.exe -f -g {romPath}",
                SupportedPlatforms = ["switch"]
            });

            config.AddEmulator(new Command
            {
                Id = "yuzu",
                Name = "yuzu",
                IsRetroArch = false,
                LaunchCommand = @"Emulators\yuzu\yuzu.exe -f -g {romPath}",
                SupportedPlatforms = ["switch"]
            });

            config.AddEmulator(new Command
            {
                Id = "steam",
                Name = "STEAM",
                IsRetroArch = false,
                LaunchCommand = @"""C:\Program Files (x86)\Steam\steam.exe"" -applaunch {romName}",
                SupportedPlatforms = ["steam"]
            });

            SetCommonDefaultEmulators(config);

            config.DefaultEmulators["nintendods"] = "desmume";
            config.DefaultEmulators["3ds"] = "azahar plus";
            config.DefaultEmulators["nintendogamecube"] = "dolphin";
            config.DefaultEmulators["wii"] = "dolphin";

            return config;
        }

        protected override ICommandConfig ParseConfigFromData(Dictionary<string, Dictionary<string, string>> data)
        {
            var config = new CommandConfig();

            foreach (var section in data)
            {
                if (section.Key == "DefaultEmulators")
                {
                    config.DefaultEmulatorsMap = new Dictionary<string, string>(section.Value);
                    continue;
                }

                var props = section.Value;

                var command = new Command
                {
                    Id = section.Key,
                    Name = props.GetValueOrDefault("name") ?? section.Key,
                    LaunchCommand = props.GetValueOrDefault("launchCommand") ?? string.Empty,
                    WorkingDirectory = props.GetValueOrDefault("workingDirectory"),
                    IsRetroArch = props.GetValueOrDefault("isRetroArch") == "true",
                    CoreName = props.GetValueOrDefault("coreName"),
                    PrelaunchScript = props.GetValueOrDefault("prelaunchScript"),
                    PostlaunchScript = props.GetValueOrDefault("postlaunchScript"),
                    SupportedPlatforms = [.. (props.GetValueOrDefault("platforms") ?? string.Empty)
                        .Split(',')
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))]
                };

                config.EmulatorCommands[command.Id] = command;
            }

            return config;
        }

        protected override Dictionary<string, Dictionary<string, string>> SerializeConfigToData(ICommandConfig config)
        {
            var data = new Dictionary<string, Dictionary<string, string>>();

            foreach (var emulator in config.Emulators.Values)
            {
                if (emulator is not Command cmd)
                    continue;

                var props = new Dictionary<string, string>
                {
                    ["name"] = cmd.Name,
                    ["platforms"] = string.Join(",", cmd.SupportedPlatforms),
                    ["launchCommand"] = cmd.LaunchCommand
                };

                if (!string.IsNullOrEmpty(cmd.WorkingDirectory))
                    props["workingDirectory"] = cmd.WorkingDirectory;

                if (cmd.IsRetroArch)
                    props["isRetroArch"] = "true";

                if (!string.IsNullOrEmpty(cmd.CoreName))
                    props["coreName"] = cmd.CoreName;

                if (!string.IsNullOrEmpty(cmd.PrelaunchScript))
                    props["prelaunchScript"] = cmd.PrelaunchScript;

                if (!string.IsNullOrEmpty(cmd.PostlaunchScript))
                    props["postlaunchScript"] = cmd.PostlaunchScript;

                data[cmd.Id] = props;
            }

            if (config.DefaultEmulators.Count > 0)
                data["DefaultEmulators"] = new Dictionary<string, string>(config.DefaultEmulators);

            return data;
        }
    }
}