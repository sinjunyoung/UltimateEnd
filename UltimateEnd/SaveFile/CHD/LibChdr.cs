using System;
using System.Runtime.InteropServices;

namespace UltimateEnd.SaveFile.CHD
{
    public static class LibChdr
    {
        #region Dll Imports

        private const string DllName = "chdr";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_open([MarshalAs(UnmanagedType.LPStr)] string filename, int mode, nint parent, out nint chd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_open_file(nint file, int mode, nint parent, out nint chd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void chd_close(nint chd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint chd_get_header(nint chd);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_read(nint chd, uint hunknum, nint buffer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_read_bytes(nint chd, ulong offset, nint buffer, uint length);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_get_metadata(nint chd, uint searchtag, uint searchindex, nint output, uint outputlen, out uint resultlen, out uint resulttag, out byte resultflags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_set_metadata(nint chd, uint metatag, uint metaindex, nint metadata, uint metalength, byte flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_create([MarshalAs(UnmanagedType.LPStr)] string filename, ulong logicalbytes, uint hunkbytes, uint compression, nint parent);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_create_file(nint file, ulong logicalbytes, uint hunkbytes, uint compression, nint parent);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_codec_config(nint chd, int param, nint config);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint chd_get_codec_name(uint codec);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_write(nint chd, uint hunknum, nint buffer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_compress_file([MarshalAs(UnmanagedType.LPStr)] string inputfile, [MarshalAs(UnmanagedType.LPStr)] string outputfile, uint version, uint hunksize, [MarshalAs(UnmanagedType.LPArray)] uint[] codecs, uint numcodecs, nint parent);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint chd_error_string(ChdrError err);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ChdrError chd_verify(nint chd, nint progress);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint chd_get_compressed_size(nint chd);

        #endregion

        public const uint CHD_CODEC_NONE = 0;
        public const uint CHD_CODEC_ZLIB = 1;      // 'zlib'
        public const uint CHD_CODEC_LZMA = 2;      // 'lzma'
        public const uint CHD_CODEC_HUFFMAN = 3;   // 'huff'
        public const uint CHD_CODEC_FLAC = 4;      // 'flac'
        public const uint CHD_CODEC_CD_ZLIB = 5;   // 'cdzl'
        public const uint CHD_CODEC_CD_LZMA = 6;   // 'cdlz'
        public const uint CHD_CODEC_CD_FLAC = 7;   // 'cdfl'
        public const uint CHD_CODEC_AVHUFF = 8;    // 'avhu'

        public const uint CDROM_OLD_METADATA = 0x43484344;  // 'CHCD'
        public const uint CDROM_TRACK_METADATA = 0x43485452; // 'CHTR'
        public const uint CDROM_TRACK_METADATA2 = 0x43485432; // 'CHT2'
        public const uint GDROM_OLD_METADATA = 0x43484744;  // 'CHGD'
        public const uint GDROM_TRACK_METADATA = 0x43484754; // 'CHGT'
        public const uint HARD_DISK_METADATA = 0x48444944;  // 'HDID'
        public const uint HARD_DISK_IDENT_METADATA = 0x4449444E; // 'DIIN'
        public const uint PCMCIA_CIS_METADATA = 0x43495320; // 'CIS '
        public const uint AV_METADATA = 0x41564156;          // 'AVAV'
        public const uint AV_LD_METADATA = 0x41564C44;       // 'AVLD'

        public static uint GetCodecId(string codecName)
        {
            return codecName?.ToLowerInvariant() switch
            {
                "none" => CHD_CODEC_NONE,
                "zlib" => CHD_CODEC_ZLIB,
                "lzma" => CHD_CODEC_LZMA,
                "huff" => CHD_CODEC_HUFFMAN,
                "flac" => CHD_CODEC_FLAC,
                "cdzl" => CHD_CODEC_CD_ZLIB,
                "cdlz" => CHD_CODEC_CD_LZMA,
                "cdfl" => CHD_CODEC_CD_FLAC,
                "avhu" => CHD_CODEC_AVHUFF,
                _ => CHD_CODEC_NONE
            };
        }

        public static string GetCodecName(uint codecId)
        {
            nint ptr = chd_get_codec_name(codecId);
            if (ptr == nint.Zero)
                return "Unknown";
            return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
        }
    }
}