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
                Id = "mGBA", Name = "mGBA", SupportedPlatforms = ["gb", "gbc", "gba"],
                LaunchCommand = @"Emulators\mGBA\mGBA.exe {romPath} --fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "visualboyadvance", Name = "VisualBoyAdvance", SupportedPlatforms = ["gb", "gbc", "gba"],
                LaunchCommand = @"Emulators\VisualBoyAdvance\VisualBoyAdvance.exe {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "bsnes", Name = "BSNES", SupportedPlatforms = ["gb", "gbc", "gba", "sfc", "fc", "md", "mastersystem", "gg", "pce", "ws", "wsc"],
                LaunchCommand = @"Emulators\bsnes\bsnes.exe {romPath} --fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "mesen", Name = "Mesen", SupportedPlatforms = ["fc", "sfc", "gb", "gbc", "gba", "pce", "gg", "ws", "wsc"],
                LaunchCommand = @"Emulators\Mesen\Mesen.exe {romPath} --fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "desmume", Name = "DeSmuME", SupportedPlatforms = ["nds"],
                LaunchCommand = @"Emulators\desmume\DeSmuME_0.9.13_x64.exe {romPath} --windowed-fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "melonds", Name = "melonDS", SupportedPlatforms = ["nds"],
                LaunchCommand = @"Emulators\melonds\melonDS.exe -f {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "duckstation", Name = "DuckStation", SupportedPlatforms = ["ps1"],
                LaunchCommand = @"Emulators\duckstation\duckstation-qt-x64-ReleaseLTCG.exe -fullscreen {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "flycast", Name = "Flycast", SupportedPlatforms = ["naomi", "naomi2", "dc"],
                LaunchCommand = @"Emulators\flycast\flycast.exe -config window:fullscreen=yes {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "citra", Name = "Citra", SupportedPlatforms = ["3ds"],
                LaunchCommand = @"Emulators\citra\citra-qt.exe -g {romPath} --fullscreen"
            });

            config.AddEmulator(new Command
            {
                Id = "azahar", Name = "Azahar", SupportedPlatforms = ["3ds"],
                LaunchCommand = @"Emulators\azahar\azahar.exe {romPath}"                
            });

            config.AddEmulator(new Command
            {
                Id = "azahar plus", Name = "Azahar Plus", SupportedPlatforms = ["3ds"],
                LaunchCommand = @"Emulators\azaharplus\azahar.exe {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "ppsspp", Name = "PPSSPP", SupportedPlatforms = ["psp"],
                LaunchCommand = @"Emulators\PPSSPP\PPSSPPWindows64.exe {romPath} --fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "pcsx2", Name = "PCSX2", SupportedPlatforms = ["ps2"],
                LaunchCommand = @"Emulators\pcsx2\pcsx2-qt.exe {romPath} -fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "rpcs3", Name = "RPCS3", SupportedPlatforms = ["ps3"],
                LaunchCommand = @"Emulators\rpcs3\rpcs3.exe {preScriptResult} --no-gui",                
                PrelaunchScript = @"powershell -Command ""$img = Mount-DiskImage -ImagePath '{romPath}' -PassThru; '{romPath}' | Out-File (Join-Path $env:TEMP 'rpcs3_iso.txt'); $drive = ($img | Get-Volume).DriveLetter + ':'; $eboot = (Get-ChildItem -Path $drive -Recurse -Filter 'EBOOT.BIN' | Select-Object -First 1).FullName; Write-Output $eboot""",
                PostlaunchScript = @"powershell -Command ""$f = Join-Path $env:TEMP 'rpcs3_iso.txt'; if (Test-Path $f) { Dismount-DiskImage -ImagePath (Get-Content $f); Remove-Item $f }"""
            });

            config.AddEmulator(new Command
            {
                Id = "vita3k", Name = "Vita3K", SupportedPlatforms = ["vita"],
                LaunchCommand = @"Emulators\Vita3K\Vita3K.exe -r {romName} --fullscreen"                
            });

            config.AddEmulator(new Command
            {
                Id = "dolphin", Name = "Dolphin", SupportedPlatforms = ["gc", "wii"],
                LaunchCommand = @"Emulators\Dolphin\Dolphin.exe {romPath} --config ""Dolphin.Display.Fullscreen=True"""
            });

            config.AddEmulator(new Command
            {
                Id = "cemu", Name = "CEMU", SupportedPlatforms = ["wiiu"],
                LaunchCommand = @"Emulators\cemu\Cemu.exe -g {romPath} -f"
            });

            config.AddEmulator(new Command
            {
                Id = "sudachi", Name = "sudachi", SupportedPlatforms = ["switch"],
                LaunchCommand = @"Emulators\sudachi\sudachi.exe -f -g {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "yuzu", Name = "yuzu", SupportedPlatforms = ["switch"],
                LaunchCommand = @"Emulators\yuzu\yuzu.exe -f -g {romPath}"
            });

            config.AddEmulator(new Command
            {
                Id = "xemu", Name = "xemu", SupportedPlatforms = ["xbox"],
                LaunchCommand = @"Emulators\xemu\xemu.exe -dvd_path {romPath} -full-screen"
            });

            config.AddEmulator(new Command
            {
                Id = "xenia", Name = "Xenia", SupportedPlatforms = ["xbox360"],
                LaunchCommand = @"Emulators\xenia\xenia_canary.exe {preScriptResult} --fullscreen=true",
                PrelaunchScript = @"powershell -Command ""$img = Mount-DiskImage -ImagePath '{romPath}' -PassThru; '{romPath}' | Out-File (Join-Path $env:TEMP 'xenia_iso.txt'); $drive = ($img | Get-Volume).DriveLetter + ':'; $xex = (Get-ChildItem -Path $drive -Recurse -Filter 'default.xex' | Select-Object -First 1).FullName; Write-Output $xex""",
                PostlaunchScript = @"powershell -Command ""$f = Join-Path $env:TEMP 'xenia_iso.txt'; if (Test-Path $f) { Dismount-DiskImage -ImagePath (Get-Content $f); Remove-Item $f }"""
            });

            config.AddEmulator(new Command
            {
                Id = "steam", Name = "STEAM", SupportedPlatforms = ["steam"],
                LaunchCommand = @"""C:\Program Files (x86)\Steam\steam.exe"" -applaunch {romName}"                
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