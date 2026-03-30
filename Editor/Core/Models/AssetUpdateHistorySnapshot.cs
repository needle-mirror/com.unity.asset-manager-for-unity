using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Holds a snapshot of an asset's metadata.
    /// </summary>
    class AssetUpdateHistorySnapshot
    {
        public int SequenceNumber { get; }
        public DateTime Updated { get; }
        public string UpdatedBy { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        public AssetUpdateHistorySnapshot(int sequenceNumber, DateTime updated, string updatedBy, string name, string description,
            IReadOnlyList<string> tags = null, IReadOnlyDictionary<string, string> metadata = null)
        {
            SequenceNumber = sequenceNumber;
            Updated = updated;
            UpdatedBy = updatedBy ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            Metadata = metadata ?? new Dictionary<string, string>();
        }
    }
}
