using Android.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.Android.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class CommandConfigService : BaseCommandConfigService
    {
        private const string TAG = "CommandConfigService";

        protected override string GetConfigDirectory()
        {
            try
            {
                var externalStorage = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;

                if (string.IsNullOrEmpty(externalStorage))
                {
                    LogWarn("ExternalStorageDirectory is null, using fallback path");
                    externalStorage = "/storage/emulated/0";
                }

                var path = Path.Combine(externalStorage, "UltimateEnd", "settings");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    LogInfo($"Created config directory: {path}");
                }

                return path;
            }
            catch (Exception ex)
            {
                LogError($"Error getting config directory: {ex.Message}");
                throw new InvalidOperationException("Failed to access config directory", ex);
            }
        }

        protected override void LogInfo(string message) => Log.Info(TAG, message);

        protected override void LogWarn(string message) => Log.Warn(TAG, message);

        protected override void LogError(string message) => Log.Error(TAG, message);

        protected override IEmulatorCommand CreateRetroArchCommand(string coreName, string displayName, params string[] platforms)
        {
            return new Command
            {
                Id = $"retroarch_{coreName}",
                Name = $"RetroArch ({displayName})",
                IsRetroArch = true,
                CoreName = coreName,
                SupportedPlatforms = [.. platforms],
                LaunchCommand =
                    "am start " +
                    "-n com.retroarch.aarch64/com.retroarch.browser.retroactivity.RetroActivityFuture " +
                    "-a android.intent.action.MAIN " +
                    "-e ROM \"{romPath}\" " +
                    $"-e LIBRETRO \"/data/data/com.retroarch.aarch64/cores/{coreName}_libretro_android.so\" " +
                    "-e CONFIGFILE \"/storage/emulated/0/Android/data/com.retroarch.aarch64/files/retroarch.cfg\" " +
                    "-e SDCARD \"/storage/emulated/0\" " +
                    "-e EXTERNAL \"/storage/emulated/0/Android/data/com.retroarch.aarch64/files\" " +
                    "-e QUITFOCUS \"true\" " +
                    "--activity-clear-task --activity-clear-top --activity-no-history"
            };
        }

        protected override ICommandConfig CreateDefaultConfig()
        {
            var config = new CommandConfig { EmulatorCommands = [] };

            RegisterCommonEmulators(config);

            config.AddEmulator(new Command
            { 
                Id = "dolphin", Name = "Dolphin", IsRetroArch = false, SupportedPlatforms = ["gc", "wii"],
                LaunchCommand = "am start -n org.dolphinemu.dolphinemu/.ui.main.MainActivity -a android.intent.action.VIEW -e AutoStartFile \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "dolphinmmjr2", Name = "Dolphin MMJR2", IsRetroArch = false, SupportedPlatforms = ["gc", "wii"], 
                LaunchCommand = "am start -n org.dolphinemu.mmjr/org.dolphinemu.dolphinemu.ui.main.MainActivity -a android.intent.action.VIEW -e AutoStartFile \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            { 
                Id = "cemu", Name = "Cemu", IsRetroArch = false, SupportedPlatforms = ["wiiu"],
                LaunchCommand = "am start -n info.cemu.cemu/.emulation.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            { 
                Id = "flycast", Name = "Flycast", IsRetroArch = false, SupportedPlatforms = ["naomi", "naomi2", "dc"],
                LaunchCommand = "am start -n com.flycast.emulator/.MainActivity -a android.intent.action.VIEW -d \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "drastic", Name = "DRASTIC", IsRetroArch = false, SupportedPlatforms = ["nds"],
                LaunchCommand = "am start -n com.dsemu.drastic/.DraSticActivity -a android.intent.action.VIEW -d \"{fileUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "melondsdual", Name = "MELONDS Dual", IsRetroArch = false, SupportedPlatforms = ["nds"],
                LaunchCommand = "am start -n me.magnum.melonds/me.magnum.melonds.ui.emulator.EmulatorActivity -a me.magnum.melonds.LAUNCH_ROM -d \"{safUriRomPath}\" --grant-read-uri-permission --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "noods", Name = "NOODS", IsRetroArch = false, SupportedPlatforms = ["nds"],
                LaunchCommand = "am start -n com.hydra.noods/.FileBrowser -a android.intent.action.MAIN -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "citra", Name = "Citra", IsRetroArch = false, SupportedPlatforms = ["3ds"],
                LaunchCommand = "am start -n org.citra.citra_emu/.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-top"
            });

            config.AddEmulator(new Command
            {
                Id = "citra mmj", Name = "Citra MMJ", IsRetroArch = false, SupportedPlatforms = ["3ds"],
                LaunchCommand = "am start -n com.antutu.ABenchMark/org.citra.emu.ui.EmulationActivity -a android.intent.action.VIEW -e GamePath \"{romPath}\" --activity-clear-top"
            });

            config.AddEmulator(new Command
            {
                Id = "azahar", Name = "AZAHAR", IsRetroArch = false, SupportedPlatforms = ["3ds"],
                LaunchCommand = "am start -n io.github.lime3ds.android/org.citra.citra_emu.activities.EmulationActivity -a android.intent.action.VIEW  -d \"{safUriRomPath}\" --activity-clear-top"
            });

            config.AddEmulator(new Command
            {
                Id = "azahar plus", Name = "AZAHAR Plus", IsRetroArch = false, SupportedPlatforms = ["3ds"],
                LaunchCommand = "am start -n io.github.azaharplus.android/org.citra.citra_emu.activities.EmulationActivity -a android.intent.action.VIEW  -d \"{safUriRomPath}\" --activity-clear-top"
            });

            config.AddEmulator(new Command
            {
                Id = "ppsspp", Name = "PPSSPP", IsRetroArch = false, SupportedPlatforms = ["psp"],
                LaunchCommand = "am start -n org.ppsspp.ppsspp/.PpssppActivity -a android.intent.action.VIEW -d \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "ppssppgold", Name = "PPSSPP(Gold)", IsRetroArch = false, SupportedPlatforms = ["psp"],
                LaunchCommand = "am start -n org.ppsspp.ppssppgold/org.ppsspp.ppsspp.PpssppActivity -a android.intent.action.VIEW -d \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command // 미 확인
            {
                Id = "vita3k", Name = "VITA3K", IsRetroArch = false, SupportedPlatforms = ["psvita"],
                LaunchCommand = "am start -n org.vita3k.emulator/.Emulator -a android.intent.action.VIEW --esa AppStartParameters -r,{romName} --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "aethersx2", Name = "AetherSX2", IsRetroArch = false, SupportedPlatforms = ["ps2"],
                LaunchCommand = "am start -n xyz.aethersx2.android/.EmulationActivity -a android.intent.action.MAIN -e bootPath \"{romPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "rpcsx", Name = "RPCSX", IsRetroArch = false, SupportedPlatforms = ["ps3"],
                LaunchCommand = "am start -n net.rpcsx/.MainActivity --es titleId {romName} --activity-clear-task --activity-clear-top --activity-no-history"
            });


            config.AddEmulator(new Command
            {
                Id = "yuzu", Name = "Yuzu", IsRetroArch = false, SupportedPlatforms = ["nsw"],
                LaunchCommand = "am start -n org.yuzu.yuzu_emu/.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "citron", Name = "Citron", IsRetroArch = false, SupportedPlatforms = ["nsw"],
                LaunchCommand = "am start -n org.citron.citron_emu/.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "edenlegacy", Name = "Eden Legacy", IsRetroArch = false, SupportedPlatforms = ["nsw"],
                LaunchCommand = "am start -n dev.legacy.eden_emulator/org.yuzu.yuzu_emu.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "edenoptimized", Name = "Eden Optimized", IsRetroArch = false, SupportedPlatforms = ["nsw"],
                LaunchCommand = "am start -n com.miHoYo.Yuanshen/org.yuzu.yuzu_emu.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "edenstandard", Name = "Eden Standard", IsRetroArch = false, SupportedPlatforms = ["nsw"],
                LaunchCommand = "am start -n dev.eden.eden_emulator/org.yuzu.yuzu_emu.activities.EmulationActivity -a android.intent.action.VIEW -d \"{safUriRomPath}\" --activity-clear-task --activity-clear-top --activity-no-history"
            });

            config.AddEmulator(new Command
            {
                Id = "rpgmakerxp", Name = "RPG Maker XP", IsRetroArch = false, SupportedPlatforms = ["rpgmakerxp"],
                LaunchCommand = @"am start -a cyou.joiplay.runtime.mkxp-z.run  -n cyou.joiplay.runtime.rpgmaker/cyou.joiplay.runtime.rpgmaker.PermissionActivity --es game '{""folder"":""{romDir}/{romName}"",""type"":""mkxp-z""}' --es settings '{""useRuby18"":{""boolean"":true},""gamepad"":{""xKeyCode"":{""int"":54},""yKeyCode"":{""int"":113},""zKeyCode"":{""int"":45},""aKeyCode"":{""int"":52},""bKeyCode"":{""int"":59},""cKeyCode"":{""int"":46},""lKeyCode"":{""int"":66},""rKeyCode"":{""int"":111},""clKeyCode"":{""int"":132},""crKeyCode"":{""int"":138}}}'"
            });

            SetCommonDefaultEmulators(config);

            config.DefaultEmulators["nintendods"] = "melondsdual";
            config.DefaultEmulators["3ds"] = "azahar";
            config.DefaultEmulators["dreamcast"] = "flycast";
            config.DefaultEmulators["seganaomi"] = "flycast";
            config.DefaultEmulators["seganaomi2"] = "flycast";
            config.DefaultEmulators["nintendogamecube"] = "dolphin";
            config.DefaultEmulators["wii"] = "dolphin";
            config.DefaultEmulators["nintendoswitch"] = "edenstandard";

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
                    LaunchCommand = props.GetValueOrDefault("launch") ?? string.Empty,
                    IsRetroArch = props.GetValueOrDefault("isRetroArch") == "true",
                    CoreName = props.GetValueOrDefault("coreName"),
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
                    ["launch"] = cmd.LaunchCommand
                };

                if (cmd.IsRetroArch)
                    props["isRetroArch"] = "true";

                if (!string.IsNullOrEmpty(cmd.CoreName))
                    props["coreName"] = cmd.CoreName;

                data[cmd.Id] = props;
            }

            if (config.DefaultEmulators.Count > 0)
                data["DefaultEmulators"] = new Dictionary<string, string>(config.DefaultEmulators);

            return data;
        }
    }
}