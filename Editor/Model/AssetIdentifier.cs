using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class AssetIdentifier : IEquatable<AssetIdentifier>
    {
        public string sourceId;
        public string version;
        public string organizationId;
        public string projectId;

        public override string ToString() => $"{sourceId}-{version}";

        public bool IsValid() => !string.IsNullOrEmpty(sourceId);

        public bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return sourceId == other.sourceId && version == other.version;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AssetIdentifier)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(sourceId, version);
        }
    }
}