using System;
using Unity.Cloud.Common;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class AssetIdentifier : IEquatable<AssetIdentifier>
    {
        public string assetId;
        public string version;
        public string organizationId;
        public string projectId;

        public AssetIdentifier()
        {
            
        }
        
        public AssetIdentifier(AssetDescriptor descriptor)
        {
            assetId = descriptor.AssetId.ToString();
            version = descriptor.AssetVersion.ToString();
            organizationId = descriptor.OrganizationGenesisId.ToString();
            projectId = descriptor.ProjectId.ToString();
        }

        public AssetIdentifier(string organizationId, string projectId, string assetId, string version)
        {
            this.assetId = assetId;
            this.version = version;
            this.organizationId = organizationId;
            this.projectId = projectId;
        }

        public bool IsValid() => !string.IsNullOrEmpty(assetId);

        public bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return assetId == other.assetId && version == other.version;
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
            return HashCode.Combine(assetId, version);
        }

        public override string ToString()
        {
            return $"[Org:{organizationId}, Proj:{projectId}, Id:{assetId}, Ver:{version}]";
        }

        public AssetDescriptor ToAssetDescriptor()
        {
            return new AssetDescriptor(new ProjectDescriptor(new OrganizationId(organizationId),
                new ProjectId(projectId)), new AssetId(assetId), new AssetVersion(version));
        }
    }
}