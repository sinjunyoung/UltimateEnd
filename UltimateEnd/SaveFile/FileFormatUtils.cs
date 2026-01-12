using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile
{
    public static class FileFormatUtils
    {
        public static string? ReadGameIdFromStart(string filePath, int length = 6)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                byte[] gameIdBytes = new byte[length];
                stream.ReadExactly(gameIdBytes, 0, length);

                return Encoding.ASCII.GetString(gameIdBytes);
            }
            catch
            {
                return null;
            }
        }

        public static byte[]? DecompressZlib(byte[] compressedData, int outputSize)
        {
            try
            {
                using var memStream = new MemoryStream(compressedData);
                memStream.ReadByte();
                memStream.ReadByte();

                using var deflateStream = new DeflateStream(memStream, CompressionMode.Decompress);
                byte[] decompressed = new byte[outputSize];
                int bytesRead = deflateStream.Read(decompressed, 0, outputSize);

                return bytesRead > 0 ? decompressed : null;
            }
            catch
            {
                return null;
            }
        }

        public static byte[]? ReadSection(Stream stream, long offset, int size)
        {
            try
            {
                stream.Seek(offset, SeekOrigin.Begin);
                byte[] data = new byte[size];

                return stream.Read(data, 0, size) == size ? data : null;
            }
            catch
            {
                return null;
            }
        }

        public static int FindMagicBytes(byte[] data, byte[] magic, int startOffset = 0)
        {
            for (int i = startOffset; i < data.Length - magic.Length; i++)
            {
                bool found = true;

                for (int j = 0; j < magic.Length; j++)
                {
                    if (data[i + j] != magic[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found) return i;
            }
            return -1;
        }

        public static string ReadNullTerminatedString(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length) return string.Empty;

            int endIndex = offset;

            while (endIndex < data.Length && data[endIndex] != 0) endIndex++;

            int length = endIndex - offset;

            return length == 0 ? string.Empty : Encoding.UTF8.GetString(data, offset, length);
        }
    }
}