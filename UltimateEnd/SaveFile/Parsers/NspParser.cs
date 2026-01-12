using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace UltimateEnd.SaveFile.Parsers
{
    public class NspParser(KeySet keySet) : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".nsp", StringComparison.CurrentCultureIgnoreCase);

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var storage = new LocalStorage(filePath, FileAccess.Read);
                var pfs = new PartitionFileSystem();
                pfs.Initialize(storage).ThrowIfFailure();

                var titleIds = new List<ulong>();

                foreach (var entry in pfs.EnumerateEntries("/", "*"))
                {
                    if (entry.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                    {
                        using var ncaStorage = new UniqueRef<IFile>();
                        pfs.OpenFile(ref ncaStorage.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        var nca = new Nca(keySet, ncaStorage.Get.AsStorage());
                        var titleId = nca.Header.TitleId;
                        titleIds.Add(titleId);
                    }
                }

                if (titleIds.Count == 0) return null;

                var baseTitleId = titleIds
                    .Where(id => (id & 0xFFF) == 0)
                    .OrderBy(id => id)
                    .FirstOrDefault();

                if (baseTitleId == 0)
                    baseTitleId = titleIds.Min();

                return baseTitleId.ToString("X16");
            }
            catch
            {
                return null;
            }
        }
    }
}