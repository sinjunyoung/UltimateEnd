using System;
using System.Diagnostics;
using System.IO;
namespace UltimateEnd.Extractor
{
    public class NCCHHeader
    {
        public byte[] Signature = new byte[0x100];
        public uint Magic;
        public uint ContentSize;
        public byte[] PartitionId = new byte[8];
        public ushort MakerCode;
        public ushort Version;
        public ulong ProgramId;
        public byte SecondaryKeySlot;
        public NCCHFlags Flags = new();

        public ulong ProgramIdHigh => ProgramId >> 32;

        public static NCCHHeader Read(BinaryReader reader)
        {
            var header = new NCCHHeader();
            long startPos = reader.BaseStream.Position;

            header.Signature = reader.ReadBytes(0x100);
            header.Magic = reader.ReadUInt32();
            header.ContentSize = reader.ReadUInt32();
            reader.Read(header.PartitionId, 0, 8);
            header.MakerCode = reader.ReadUInt16();
            header.Version = reader.ReadUInt16();

            reader.BaseStream.Seek(4, SeekOrigin.Current);
            header.ProgramId = reader.ReadUInt64();

            reader.BaseStream.Seek(startPos + 0x18B, SeekOrigin.Begin);
            header.SecondaryKeySlot = reader.ReadByte();

            reader.BaseStream.Seek(startPos + 0x18F, SeekOrigin.Begin);
            byte flagByte = reader.ReadByte();
            header.Flags.NoCrypto = (flagByte & 0x04) != 0;
            header.Flags.FixedKey = (flagByte & 0x01) != 0;
            header.Flags.SeedCrypto = (flagByte & 0x20) != 0;

            return header;
        }
    }
}