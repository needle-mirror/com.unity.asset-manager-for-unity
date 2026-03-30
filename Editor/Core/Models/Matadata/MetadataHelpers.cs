using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.AssetManager.Core.Editor
{
    enum MetadataSharingType
    {
        None,
        Partial,
        All
    }

    static class MetadataHelpers
    {
        public static IMetadata CreateMetadataFromFieldDefinition(IMetadataFieldDefinition def)
        {
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            return def.Type switch
            {
                MetadataFieldType.Text => new TextMetadata(def.Key, def.DisplayName, string.Empty),
                MetadataFieldType.Number => new NumberMetadata(def.Key, def.DisplayName, 0),
                MetadataFieldType.Boolean => new BooleanMetadata(def.Key, def.DisplayName, false),
                MetadataFieldType.Url => new UrlMetadata(def.Key, def.DisplayName, new UriEntry(null, string.Empty)),
                MetadataFieldType.Timestamp => new TimestampMetadata(def.Key, def.DisplayName, new DateTimeEntry(DateTime.Now)),
                MetadataFieldType.User => new UserMetadata(def.Key, def.DisplayName, string.Empty),
                MetadataFieldType.SingleSelection => new SingleSelectionMetadata(def.Key, def.DisplayName, string.Empty),
                MetadataFieldType.MultiSelection => new MultiSelectionMetadata(def.Key, def.DisplayName, new List<string>()),
                _ => throw new InvalidOperationException("Unexpected field definition type was encountered.")
            };
        }

        public static MetadataSharingType GetMetadataSharingType(IReadOnlyCollection<IMetadataContainer> metadata, string fieldKey)
        {
            if (MetadataIsInAllAssets(metadata, fieldKey))
                return MetadataSharingType.All;

            return MetadataIsInAtLeastOneAsset(metadata, fieldKey)
                ? MetadataSharingType.Partial
                : MetadataSharingType.None;
        }

        public static bool HasSameMetadataFieldKeys(IReadOnlyCollection<IMetadataContainer> metadata)
        {
            if (metadata == null || metadata.Count <= 1)
                return true;

            var referenceMetadata = metadata.First();
            var count = referenceMetadata.Count();

            if (metadata.Any(m => m.Count() != count))
                return false;

            foreach (var fieldKey in referenceMetadata.Select(x => x.FieldKey))
            {
                if (!MetadataIsInAllAssets(metadata, fieldKey))
                    return false;
            }

            return true;
        }

        static bool MetadataIsInAllAssets(IEnumerable<IMetadataContainer> metadata, string fieldKey)
        {
            if (metadata == null)
                return false;

            return metadata.All(m => m?.FirstOrDefault(x => x.FieldKey == fieldKey) != null);
        }

        static bool MetadataIsInAtLeastOneAsset(IEnumerable<IMetadataContainer> metadata, string fieldKey)
        {
            if (metadata == null)
                return false;

            return metadata.Any(m => m?.FirstOrDefault(x => x.FieldKey == fieldKey) != null);
        }
    }
}
