using System;

namespace UltimateEnd.SaveFile.CHD
{
    public class ChdBlockDevice(LibChdrWrapper chd, uint hunkbytes, uint unitbytes, uint numBlocks)
    {
        private readonly uint blocksPerHunk = hunkbytes / unitbytes;
        private byte[]? readBuffer = null;
        private int currentHunk = -1;

        private int sessionOffset = -1;

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

            if (sessionOffset == -1 || blockNumber == 16)
            {
                if (unitbytes == 2448 || unitbytes == 2352)
                {
                    if (offset + 25 < readBuffer.Length && readBuffer[offset + 25] == 0x43 && readBuffer[offset + 26] == 0x44)
                        sessionOffset = 24;
                    else if (offset + 17 < readBuffer.Length && readBuffer[offset + 17] == 0x43 && readBuffer[offset + 18] == 0x44)
                        sessionOffset = 16;
                    else if (readBuffer[offset + 1] == 0x43 && readBuffer[offset + 2] == 0x44)
                        sessionOffset = 0;

                    if (sessionOffset == -1 && blockNumber == 16) sessionOffset = 0;
                }
                else
                {
                    sessionOffset = 0;
                }
            }

            int finalOffset = (sessionOffset == -1) ? 0 : sessionOffset;

            if (offset + (uint)finalOffset + 2048 <= (uint)readBuffer.Length)
                Array.Copy(readBuffer, (int)(offset + (uint)finalOffset), outPtr, 0, 2048);
            else
                Array.Copy(readBuffer, (int)offset, outPtr, 0, (int)Math.Min(unitbytes, 2048u));

            return outPtr;
        }
    }
}