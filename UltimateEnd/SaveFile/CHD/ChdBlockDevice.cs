using System;

namespace UltimateEnd.SaveFile.CHD
{
    public class ChdBlockDevice(LibChdrWrapper chd, uint hunkbytes, uint unitbytes, uint numBlocks)
    {
        private readonly uint blocksPerHunk = hunkbytes / unitbytes;

        private byte[]? readBuffer = null;
        private int currentHunk = -1;

        public byte[]? ReadBlock(uint blockNumber)
        {
            if (blockNumber >= numBlocks) return null;

            uint hunk = blockNumber / blocksPerHunk;
            uint blockInHunk = blockNumber % blocksPerHunk;

            if (currentHunk != (int)hunk)
            {
                readBuffer = chd.ReadHunk(hunk);
                currentHunk = (int)hunk;
            }

            if (readBuffer == null) return null;

            uint offset = blockInHunk * unitbytes;

            byte[] outPtr = new byte[2048];

            if (unitbytes == 2352)
            {
                if (offset + 16 + 2048 > readBuffer.Length) return null;

                Array.Copy(readBuffer, offset + 16, outPtr, 0, 2048);
            }
            else
            {
                if (offset + 2048 > readBuffer.Length) return null;

                Array.Copy(readBuffer, offset, outPtr, 0, 2048);
            }

            return outPtr;
        }
    }
}