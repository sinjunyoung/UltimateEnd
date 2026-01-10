using System.Collections.Generic;
using System.Linq;

namespace UltimateEnd.SaveFile
{
    public static class RetroArchSaveConfig
    {
        private static readonly Dictionary<string, SaveFileInfo> CoreSaveInfo = new()
        {
            // Game Boy / Game Boy Color
            ["gambatte"] = new() { Extensions = [".srm"] },
            ["sameboy"] = new() { Extensions = [".sav"] },
            ["gearboy"] = new() { Extensions = [".sav"] },
            ["tgbdual"] = new() { Extensions = [".sav"] },
            ["DoubleCherryGB"] = new() { Extensions = [".sav"] },

            // Game Boy Advance
            ["mgba"] = new() { Extensions = [".srm", ".sav"] },
            ["vba_next"] = new() { Extensions = [".srm"] },
            ["vbam"] = new() { Extensions = [".srm"] },
            ["gpsp"] = new() { Extensions = [".sav"] },

            // NES / SNES
            ["fceumm"] = new() { Extensions = [".srm"] },
            ["mesen"] = new() { Extensions = [".sav"] },
            ["mesen-s"] = new() { Extensions = [".sav"] },
            ["snes9x"] = new() { Extensions = [".srm"] },
            ["bsnes"] = new() { Extensions = [".srm"] },

            // NDS
            ["melondsds"] = new() { Extensions = [".sav"], SubFolder = "Nintendo - Nintendo DS" },
            ["melonds"] = new() { Extensions = [".sav"] },
            ["desmume"] = new() { Extensions = [".dsv"] },
            ["desmume2015"] = new() { Extensions = [".dsv"] },
            ["noods"] = new() { Extensions = [".sav"] },

            // 3DS
            ["citra"] = new() { Extensions = [".sav"] },

            // GameCube / Wii
            ["dolphin"] = new() { Extensions = [".raw", ".gci"], SubFolder = "GC" },

            // Mega Drive / Master System
            ["genesis_plus_gx"] = new() { Extensions = [".srm"] },
            ["genesis_plus_gx_wide"] = new() { Extensions = [".srm"] },
            ["picodrive"] = new() { Extensions = [".srm"] },
            ["gearsystem"] = new() { Extensions = [".sav"] },

            // PS1
            ["mednafen_psx"] = new() { Extensions = [".srm", ".mcr"] },
            ["mednafen_psx_hw"] = new() { Extensions = [".srm", ".mcr"] },
            ["pcsx_rearmed"] = new() { Extensions = [".srm", ".mcr"] },
            ["swanstation"] = new() { Extensions = [".sav"] },

            // PSP
            ["ppsspp"] = new() { Extensions = [".sav"], SubFolder = "PSP/SAVEDATA" },

            // Saturn
            ["mednafen_saturn"] = new() { Extensions = [".bkr", ".smpc"] },
            ["yabasanshiro"] = new() { Extensions = [".srm"] },
            ["yabause"] = new() { Extensions = [".srm"] },

            // MAME / FBNeo
            ["mamearcade"] = new() { Extensions = [".nv"], SubFolder = "mame/nvram" },
            ["mame2010"] = new() { Extensions = [".nv"], SubFolder = "mame2010/nvram" },
            ["mame2003_plus"] = new() { Extensions = [".nv"], SubFolder = "mame2003-plus/nvram" },
            ["mame2003"] = new() { Extensions = [".nv"], SubFolder = "mame2003/nvram" },
            ["mame2000"] = new() { Extensions = [".nv"], SubFolder = "mame2000/nvram" },
            ["fbneo"] = new() { Extensions = [".fs"], SubFolder = "FinalBurn Neo/fbneo" },
            ["fbneo_crcskip"] = new() { Extensions = [".fs"] },
            ["fbalpha2012"] = new() { Extensions = [".fs"] },
            ["geolith"] = new() { Extensions = [".srm"] },
            ["flycast"] = new() { Extensions = [".bin"], SubFolder = "dc" },

            // MSX
            ["fmsx"] = new() { Extensions = [".sav"] },
            ["bluemsx"] = new() { Extensions = [".sav"] },

            // PC Engine
            ["mednafen_pce_fast"] = new() { Extensions = [".srm", ".bram"] },
            ["mednafen_supergrafx"] = new() { Extensions = [".srm"] },

            // WonderSwan
            ["mednafen_wswan"] = new() { Extensions = [".srm", ".eep"] },

            // Others
            ["opera"] = new() { Extensions = [ ".srm" ] },
            ["dosbox_pure"] = new() { Extensions = [ ".save" ] },
            ["easyrpg"] = new() { Extensions = [".lsd"] },
        };

        public static SaveFileInfo GetSaveInfo(string coreName)
        {
            if (CoreSaveInfo.TryGetValue(coreName, out var info))
                return info;

            return new SaveFileInfo { Extensions = [".srm"] };
        }

        public static string[] GetAllPossibleExtensions()
        {
            return [.. CoreSaveInfo.Values
                .SelectMany(info => info.Extensions)
                .Concat(CoreSaveInfo.Values
                    .Where(info => info.OptionalExtensions != null)
                    .SelectMany(info => info.OptionalExtensions))
                .Distinct()];
        }
    }
}