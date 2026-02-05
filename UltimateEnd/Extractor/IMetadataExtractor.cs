using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public interface IMetadataExtractor
    {
        Task<GameMetadata> Extract(string filePath);
    }
}