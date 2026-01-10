using System;

namespace UltimateEnd.SaveFile.CHD
{
    public class ChdBlockDevice
    {
        private readonly LibChdrWrapper chd;
        private readonly uint hunkbytes;
        private readonly uint unitbytes;
        private readonly uint blocksPerHunk;
        private readonly uint numBlocks;

        private byte[]? readBuffer;
        private int currentHunk = -1;

        public ChdBlockDevice(LibChdrWrapper chd, uint hunkbytes, uint unitbytes, uint numBlocks)
        {
            this.chd = chd;
            this.hunkbytes = hunkbytes;
            this.unitbytes = unitbytes;
            this.numBlocks = numBlocks;
            this.blocksPerHunk = hunkbytes / unitbytes;
            this.readBuffer = null;
        }

        public byte[]? ReadBlock(uint blockNumber)
        {
            if (blockNumber >= numBlocks)
                return null;

            uint hunk = blockNumber / blocksPerHunk;
            uint blockInHunk = blockNumber % blocksPerHunk;

            // PPSSPP와 동일: 같은 hunk면 재사용
            if (currentHunk != (int)hunk)
            {
                readBuffer = chd.ReadHunk(hunk);
                currentHunk = (int)hunk;
            }

            if (readBuffer == null)
                return null;

            // PPSSPP와 동일: unitbytes 오프셋 계산
            uint offset = blockInHunk * unitbytes;

            byte[] outPtr = new byte[2048];

            // CD-ROM MODE1: 16바이트 건너뛰기
            if (unitbytes == 2352)
            {
                if (offset + 16 + 2048 > readBuffer.Length)
                    return null;
                Array.Copy(readBuffer, offset + 16, outPtr, 0, 2048);
            }
            else
            {
                if (offset + 2048 > readBuffer.Length)
                    return null;
                Array.Copy(readBuffer, offset, outPtr, 0, 2048);
            }

            return outPtr;
        }
    }
}