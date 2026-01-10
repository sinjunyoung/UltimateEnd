using System.Collections.Generic;

namespace UltimateEnd.Models
{
    public class PegasusMetadataFile
    {
        public List<PegasusCollectionMetadata> Collections { get; set; } = [];

        public List<PegasusGameMetadata> Games { get; set; } = [];
    }
}