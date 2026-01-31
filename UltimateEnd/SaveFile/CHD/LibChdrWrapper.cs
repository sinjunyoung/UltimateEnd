using System;
using System.Runtime.InteropServices;
using System.Text;

namespace UltimateEnd.SaveFile.CHD
{
    public class LibChdrWrapper : IDisposable
    {
        private IntPtr _chdHandle = IntPtr.Zero;
        private bool _disposed = false;

        private byte[]? _reusableHunkBuffer = null;
        private GCHandle _pinnedHunkHandle;
        private bool _hunkBufferPinned = false;

        public ChdrHeader? Header { get; private set; }

        public bool IsOpen => _chdHandle != IntPtr.Zero;

        public ChdrError Open(string filename, ChdrOpenFlags flags = ChdrOpenFlags.CHDOPEN_READ)
        {
            Close();

            var result = LibChdr.chd_open(filename, (int)flags, IntPtr.Zero, out _chdHandle);

            if (result == ChdrError.CHDERR_NONE)
            {
                IntPtr headerPtr = LibChdr.chd_get_header(_chdHandle);

                if (headerPtr != IntPtr.Zero)
                {
                    Header = Marshal.PtrToStructure<ChdrHeader>(headerPtr);

                    if (Header.HasValue)
                    {
                        _reusableHunkBuffer = new byte[Header.Value.hunkbytes];
                        _pinnedHunkHandle = GCHandle.Alloc(_reusableHunkBuffer, GCHandleType.Pinned);
                        _hunkBufferPinned = true;
                    }
                }
            }
            return result;
        }

        public void Close()
        {
            if (_hunkBufferPinned)
            {
                _pinnedHunkHandle.Free();
                _hunkBufferPinned = false;
            }

            if (_chdHandle != IntPtr.Zero)
            {
                LibChdr.chd_close(_chdHandle);
                _chdHandle = IntPtr.Zero;
                Header = null;
            }

            _reusableHunkBuffer = null;
        }

        public byte[]? ReadHunk(uint hunkIndex)
        {
            if (!Header.HasValue || _reusableHunkBuffer == null) throw new InvalidOperationException("CHD not opened");
            
            var result = LibChdr.chd_read(_chdHandle, hunkIndex, _pinnedHunkHandle.AddrOfPinnedObject());

            if (result != ChdrError.CHDERR_NONE) return null;

            byte[] copy = new byte[Header.Value.hunkbytes];
            Buffer.BlockCopy(_reusableHunkBuffer, 0, copy, 0, (int)Header.Value.hunkbytes);

            return copy;
        }
        
        public bool ReadHunkInto(uint hunkIndex, byte[] buffer)
        {
            if (!Header.HasValue) return false;

            if (buffer.Length < Header.Value.hunkbytes) return false;

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                var result = LibChdr.chd_read(_chdHandle, hunkIndex, handle.AddrOfPinnedObject());
                return result == ChdrError.CHDERR_NONE;
            }
            finally
            {
                handle.Free();
            }
        }

        public byte[]? ReadBytes(ulong offset, uint length)
        {
            if (!IsOpen) throw new InvalidOperationException("CHD not opened");

            byte[] buffer = new byte[length];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                var result = LibChdr.chd_read_bytes(_chdHandle, offset, handle.AddrOfPinnedObject(), length);

                if (result != ChdrError.CHDERR_NONE) return null;

                return buffer;
            }
            finally
            {
                handle.Free();
            }
        }

        public string? GetMetadata(uint tag, uint index = 0)
        {
            if (!IsOpen) throw new InvalidOperationException("CHD not opened");

            const int bufferSize = 65536;
            byte[] buffer = new byte[bufferSize];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                var result = LibChdr.chd_get_metadata(_chdHandle, tag, index, handle.AddrOfPinnedObject(), bufferSize, out uint resultLen, out uint resultTag, out byte flags);

                if (result == ChdrError.CHDERR_NONE && resultLen > 0) return Encoding.ASCII.GetString(buffer, 0, (int)resultLen).TrimEnd('\0');

                return null;
            }
            finally
            {
                handle.Free();
            }
        }

        public string[] GetCompressionMethods()
        {
            if (!Header.HasValue) return [];

            var methods = new System.Collections.Generic.List<string>();
            var header = Header.Value;

            if (header.compression0 != 0) methods.Add(LibChdr.GetCodecName(header.compression0));
            if (header.compression1 != 0) methods.Add(LibChdr.GetCodecName(header.compression1));
            if (header.compression2 != 0) methods.Add(LibChdr.GetCodecName(header.compression2));
            if (header.compression3 != 0) methods.Add(LibChdr.GetCodecName(header.compression3));

            return [.. methods];
        }

        public static string GetCompressionName(uint compression)
        {
            return compression switch
            {
                0 => "None",
                LibChdr.CHD_CODEC_ZLIB => "ZLIB",
                LibChdr.CHD_CODEC_LZMA => "LZMA",
                LibChdr.CHD_CODEC_HUFFMAN => "Huffman",
                LibChdr.CHD_CODEC_FLAC => "FLAC",
                LibChdr.CHD_CODEC_CD_ZLIB => "CD ZLIB",
                LibChdr.CHD_CODEC_CD_LZMA => "CD LZMA",
                LibChdr.CHD_CODEC_CD_FLAC => "CD FLAC",
                LibChdr.CHD_CODEC_AVHUFF => "AV Huffman",
                _ => $"Unknown (0x{compression:X})"
            };
        }

        public static string GetErrorString(ChdrError error)
        {
            IntPtr ptr = LibChdr.chd_error_string(error);

            if (ptr == IntPtr.Zero) return error.ToString();

            return Marshal.PtrToStringAnsi(ptr) ?? error.ToString();
        }

        public static string ByteArrayToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes) sb.AppendFormat("{0:x2}", b);

            return sb.ToString();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        ~LibChdrWrapper() => Dispose();
    }
}