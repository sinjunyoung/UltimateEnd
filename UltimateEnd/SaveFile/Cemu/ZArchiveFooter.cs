namespace UltimateEnd.SaveFile.Cemu
{    
    public struct ZArchiveFooter
    {
        public uint magic;

        public uint version;

        public ulong totalSize;

        public SectionInfo sectionCompressedData;

        public SectionInfo sectionOffsetRecords;

        public SectionInfo sectionNames;

        public SectionInfo sectionFileTree;

        public SectionInfo sectionMetaDirectory;

        public SectionInfo sectionMetaData;
    }
}