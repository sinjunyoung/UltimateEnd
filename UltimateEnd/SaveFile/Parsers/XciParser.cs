using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UltimateEnd.SaveFile.Parsers
{
    public class XciParser(KeySet keySet) : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".xci", StringComparison.CurrentCultureIgnoreCase);

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var storage = new LocalStorage(filePath, FileAccess.Read);
                var xci = new Xci(keySet, storage);

                if (xci.HasPartition(XciPartitionType.Secure))
                {
                    var secure = xci.OpenPartition(XciPartitionType.Secure);
                    var titleIds = new List<ulong>();

                    foreach (var entry in secure.EnumerateEntries("/", "*"))
                    {
                        if (entry.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                        {
                            using var ncaStorage = new UniqueRef<IFile>();
                            secure.OpenFile(ref ncaStorage.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

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

                    if (baseTitleId == 0) baseTitleId = titleIds.Min();

                    return baseTitleId.ToString("X16");
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}