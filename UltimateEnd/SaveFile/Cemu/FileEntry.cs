namespace UltimateEnd.SaveFile.Cemu
{
    public struct FileEntry
    {
        public uint nameOffset;

        public ulong offsetOrNodeStart;

        public ulong sizeOrCount;

        public bool isFile;
    }
}