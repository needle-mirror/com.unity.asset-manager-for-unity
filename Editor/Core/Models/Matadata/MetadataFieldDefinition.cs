
using System;

namespace Unity.AssetManager.Core.Editor
{
    enum MetadataFieldType
    {
        Boolean,
        Selection,
        Number,
        Text,
        Timestamp,
        Url,
        User,
        Unknown
    }

    [Serializable]
    class MetadataFieldDefinition
    {
        public string Key { get; private set; }

        public string DisplayName { get; private set; }

        public MetadataFieldType Type { get; private set; }

        public MetadataFieldDefinition(string key, string displayName, MetadataFieldType fieldType)
        {
            Key = key;
            DisplayName = displayName;
            Type = fieldType;
        }
    }
}
