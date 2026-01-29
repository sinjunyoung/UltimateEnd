using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public interface IMetadataExtractor
    {
        Task<ExtractedMetadata> Extract(string filePath);
    }
}