using Avalonia.Platform;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public class SwitchMetadataExtractor : IMetadataExtractor
    {
        private readonly KeySet _keySet;
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();

        public SwitchMetadataExtractor(string prodKeysPath)
        {
            _keySet = KeySet.CreateDefaultKeySet();
            ExternalKeyReader.ReadKeyFile(_keySet, prodKeysPath, null, null, (IProgressReport)null);
        }

        public async Task<ExtractorMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = System.IO.Path.GetExtension(filePath).ToLower();
            var metadata = ext switch
            {
                ".nsp" => await ExtractFromNSP(filePath),
                ".xci" => await ExtractFromXCI(filePath),
                _ => null,
            };

            if (metadata != null) _cache[filePath] = metadata;

            return metadata;
        }

        private async Task<ExtractorMetadata> ExtractFromNSP(string nspPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var file = new LocalStorage(nspPath, FileAccess.Read);
                    var pfs = new PartitionFileSystem();
                    pfs.Initialize(file).ThrowIfFailure();

                    return ExtractMetadata(pfs);
                }
                catch
                {
                    return null;
                }
            });
        }

        private async Task<ExtractorMetadata> ExtractFromXCI(string xciPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var file = new LocalStorage(xciPath, FileAccess.Read);
                    var xci = new Xci(_keySet, file);
                    var securePartition = xci.OpenPartition(XciPartitionType.Secure);

                    return ExtractMetadata(securePartition);
                }
                catch
                {
                    return null;
                }
            });
        }

        private ExtractorMetadata ExtractMetadata(IFileSystem fs)
        {
            var metadata = new ExtractorMetadata();

            try
            {
                var entries = fs.EnumerateEntries("/", "*.nca");

                foreach (var entry in entries)
                {
                    using var ncaFile = new UniqueRef<IFile>();
                    fs.OpenFile(ref ncaFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    var nca = new Nca(_keySet, ncaFile.Release().AsStorage());

                    if (nca.Header.ContentType == NcaContentType.Control)
                    {
                        var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                        ExtractMetadataFromRomFs(romfs, metadata);
                        break;
                    }
                }
            }
            catch { }

            return metadata;
        }

        private static void ExtractMetadataFromRomFs(IFileSystem romfs, ExtractorMetadata metadata)
        {
            try
            {
                if (romfs.FileExists("/control.nacp"))
                {
                    using var nacpFile = new UniqueRef<IFile>();
                    romfs.OpenFile(ref nacpFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                    var control = new ApplicationControlProperty();
                    var nacpData = new byte[Marshal.SizeOf<ApplicationControlProperty>()];
                    nacpFile.Get.Read(out _, 0, nacpData).ThrowIfFailure();

                    GCHandle handle = GCHandle.Alloc(nacpData, GCHandleType.Pinned);

                    try
                    {
                        control = Marshal.PtrToStructure<ApplicationControlProperty>(handle.AddrOfPinnedObject());
                    }
                    finally
                    {
                        handle.Free();
                    }

                    metadata.HasKorean = ExtractSupportedLanguages(control.SupportedLanguageFlag).Contains("ko");

                    string foundTitle = null;
                    string foundPublisher = null;

                    var titleKo = control.Title[12].NameString.ToString().Trim('\0', ' ');
                    var titleEn = control.Title[0].NameString.ToString().Trim('\0', ' ');

                    var publisherKo = control.Title[12].PublisherString.ToString().Trim('\0', ' ');
                    var publisherEn = control.Title[0].PublisherString.ToString().Trim('\0', ' ');

                    if (!string.IsNullOrWhiteSpace(titleKo))
                    {
                        foundTitle = titleKo;
                        foundPublisher = publisherKo;
                    }
                    else if (!string.IsNullOrWhiteSpace(titleEn))
                    {
                        foundTitle = titleEn;
                        foundPublisher = publisherEn;
                    }
                    else
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            var t = control.Title[i].NameString.ToString().Trim('\0', ' ');

                            if (!string.IsNullOrWhiteSpace(t))
                            {
                                foundTitle = t;
                                foundPublisher = control.Title[i].PublisherString.ToString().Trim('\0', ' ');
                                break;
                            }
                        }
                    }

                    metadata.Title = foundTitle ?? string.Empty;
                    metadata.Developer = foundPublisher;
                }

                string[] iconPriorities = ["/icon_Korean.dat", "/icon_AmericanEnglish.dat", "/icon_English.dat"];
                string targetIconPath = null;

                foreach (var path in iconPriorities)
                {
                    if (romfs.FileExists(path))
                    {
                        targetIconPath = path;
                        break;
                    }
                }

                if (targetIconPath == null)
                {
                    var iconFiles = romfs.EnumerateEntries("/", "icon_*.dat").ToList();

                    if (iconFiles.Count != 0) targetIconPath = iconFiles.First().FullPath;
                }

                if (targetIconPath != null) metadata.Image = ReadFile(romfs, targetIconPath);
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(metadata.Title)) metadata.Title = "Extraction Failed";
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        private static List<string> ExtractSupportedLanguages(uint languageFlag)
        {
            System.Diagnostics.Debug.WriteLine($"Language Flag: 0x{languageFlag:X8} = {Convert.ToString(languageFlag, 2).PadLeft(32, '0')}");

            var languages = new List<string>();
            var flagValue = languageFlag;

            var languageMap = new Dictionary<uint, string>
            {
                { 1u << 0, "ja" },      // Japanese
                { 1u << 1, "en" },      // American English
                { 1u << 2, "fr" },      // French
                { 1u << 3, "de" },      // German
                { 1u << 4, "it" },      // Italian
                { 1u << 5, "es" },      // Spanish
                { 1u << 6, "zh-CN" },   // Chinese (Simplified)
                { 1u << 7, "ko" },      // Korean
                { 1u << 8, "nl" },      // Dutch
                { 1u << 9, "pt" },      // Portuguese
                { 1u << 10, "ru" },     // Russian
                { 1u << 11, "zh-TW" },  // Chinese (Traditional)
                { 1u << 12, "en-GB" },  // British English
                { 1u << 13, "fr-CA" },  // Canadian French
                { 1u << 14, "es-419" }, // Latin American Spanish
                { 1u << 15, "pt-BR" }   // Brazilian Portuguese
            };

            foreach (var kvp in languageMap)
            {
                if ((flagValue & kvp.Key) != 0)
                {
                    languages.Add(kvp.Value);
                }
            }

            return languages;
        }

        private static byte[] ReadFile(IFileSystem fs, string path)
        {
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref, path.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            file.Get.GetSize(out long size).ThrowIfFailure();
            var data = new byte[size];
            file.Get.Read(out _, 0, data).ThrowIfFailure();

            return data;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}