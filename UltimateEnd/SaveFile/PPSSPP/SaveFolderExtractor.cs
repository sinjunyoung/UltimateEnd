using System;
using System.IO;
using UltimateEnd.Models;
using UltimateEnd.SaveFile.CHD;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.SaveFile.PPSSPP
{
    public static class SaveFolderExtractor
    {
        public static string? ExtractSaveFolderId(string romPath)
        {
            if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath)) return null;

            var extension = Path.GetExtension(romPath).ToLower();

            return extension switch
            {
                ".iso" => ExtractFromIso(romPath),
                ".cso" => ExtractFromCso(romPath),
                ".chd" => ExtractFromChd(romPath),
                _ => null
            };
        }

        private static string? ExtractFromChd(string chdPath)
        {
            try
            {
                using var chd = new LibChdrWrapper();

                if (chd.Open(chdPath) != ChdrError.CHDERR_NONE || !chd.Header.HasValue) return null;

                var header = chd.Header.Value;
                uint unitbytes = header.unitbytes;
                ulong unitcount = header.unitcount;

                if (unitbytes == 0 || unitbytes > 10000)
                {
                    var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0)
                                ?? chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                    if (metadata != null && metadata.Contains("TYPE:MODE1"))
                        unitbytes = 2352;
                    else
                        unitbytes = 2048;
                }

                if (unitcount == 0) unitcount = header.logicalbytes / unitbytes;

                var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);
                var ebootData = ExtractParamSfoFromBlockDevice(lba => blockDevice.ReadBlock(lba));

                return ebootData != null ? DecryptAndSearchSavePath(ebootData) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                var ebootData = ExtractParamSfoFromBlockDevice(lba =>
                {
                    stream.Seek(lba * 2048, SeekOrigin.Begin);
                    byte[] sector = new byte[2048];

                    return stream.Read(sector, 0, 2048) == 2048 ? sector : null;
                });

                return ebootData != null ? DecryptAndSearchSavePath(ebootData) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromCso(string csoPath)
        {
            try
            {
                using var cso = new CsoStreamReader(csoPath);
                var ebootData = ExtractParamSfoFromBlockDevice(lba => cso.ReadSector(lba));

                return ebootData != null ? DecryptAndSearchSavePath(ebootData) : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ExtractParamSfoFromBlockDevice(Func<uint, byte[]?> readSector)
        {
            try
            {
                var pvd = Iso9660Utils.ReadPrimaryVolumeDescriptor(readSector);
                if (pvd == null) return null;

                uint rootLBA = Iso9660Utils.GetRootLBA(pvd);
                var rootSector = readSector(rootLBA);

                uint pspGameLBA = Iso9660Utils.FindDirectory(rootSector, "PSP_GAME");
                if (pspGameLBA == 0) return null;

                var pspGameSector = readSector(pspGameLBA);
                var sfoInfo = Iso9660Utils.FindFile(pspGameSector, "PARAM.SFO");

                if (sfoInfo == null) return null;

                return Iso9660Utils.ReadFileFromSectors(readSector, sfoInfo.Value.lba, sfoInfo.Value.size);
            }
            catch
            {
                return null;
            }
        }

        private static string? DecryptAndSearchSavePath(byte[] ebootData)
        {
            byte[]? decryptedData = PrxDecrypter.PartialDecrypt(ebootData);
            decryptedData ??= ebootData;

            return PrxDecrypter.ExtractSaveFolderId(decryptedData);
        }

        public static string? GetSaveFolderId(GameMetadata game) => ExtractSaveFolderId(game.GetRomFullPath());
    }
}