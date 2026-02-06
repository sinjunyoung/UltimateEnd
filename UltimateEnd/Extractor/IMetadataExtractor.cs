using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public interface IMetadataExtractor
    {
        Task<ExtractorMetadata> Extract(string filePath);
    }
}