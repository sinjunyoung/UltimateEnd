using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public class RvzWiaParser : IFormatParser
    {
        private const uint WIA_MAGIC = 0x01414957;
        private const uint RVZ_MAGIC = 0x015A5652;

        public bool CanParse(string extension)
        {
            var ext = extension.ToLower();

            return ext == ".rvz" || ext == ".wia";
        }

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                byte[] header1 = new byte[0x48];

                if (stream.Read(header1, 0, 0x48) != 0x48) return null;

                uint magic = BitConverter.ToUInt32(header1, 0);

                if (magic != WIA_MAGIC && magic != RVZ_MAGIC) return null;

                uint header2Size = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(header1, 0x0C));

                byte[] header2 = new byte[header2Size];

                if (stream.Read(header2, 0, (int)header2Size) != (int)header2Size) return null;

                if (header2Size < 0x10 + 6) return null;

                return Encoding.ASCII.GetString(header2, 0x10, 6);
            }
            catch
            {
                return null;
            }
        }
    }
}