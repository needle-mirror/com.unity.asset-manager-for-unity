using System;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class AssetIdentifier : IEquatable<AssetIdentifier>
    {
        public string assetId => m_AssetId;
        public string version => m_Version;
        public string organizationId => m_OrganizationId;
        public string projectId => m_ProjectId;

        [SerializeField]
        string m_AssetId;

        [SerializeField]
        string m_Version;

        [SerializeField]
        string m_OrganizationId;

        [SerializeField]
        string m_ProjectId;

        public AssetIdentifier()
        {
        }

        public AssetIdentifier(AssetDescriptor descriptor)
        {
            m_AssetId = descriptor.AssetId.ToString();
            m_Version = descriptor.AssetVersion.ToString();
            m_OrganizationId = descriptor.OrganizationGenesisId.ToString();
            m_ProjectId = descriptor.ProjectId.ToString();
        }

        public AssetIdentifier(string organizationId, string projectId, string assetId, string version)
        {
            m_AssetId = assetId;
            m_Version = version;
            m_OrganizationId = organizationId;
            m_ProjectId = projectId;
        }

        public bool IsValid() => !string.IsNullOrEmpty(m_AssetId);

        static bool IsSameId(string str1, string str2)
        {
            return (str1 ?? string.Empty) == (str2 ?? string.Empty);
        }

        public bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IsSameId(m_OrganizationId, other.m_OrganizationId)
                   && IsSameId(m_ProjectId, other.m_ProjectId)
                   && IsSameId(m_AssetId, other.m_AssetId)
                   && IsSameId(m_Version, other.m_Version);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj.GetType() != GetType())
                return false;

            return Equals((AssetIdentifier)obj);
        }

        public override int GetHashCode()
        {
            var orgIdHash = (organizationId ?? string.Empty).GetHashCode();
            var projIdHash = (projectId ?? string.Empty).GetHashCode();
            var assetIdHash = (assetId ?? string.Empty).GetHashCode();
            var versionHash = (version ?? string.Empty).GetHashCode();

            return HashCode.Combine(orgIdHash, projIdHash, assetIdHash, versionHash);
        }

        public override string ToString()
        {
            return $"[Org:{organizationId}, Proj:{m_ProjectId}, Id:{m_AssetId}, Ver:{m_Version}]";
        }

        public AssetDescriptor ToAssetDescriptor()
        {
            return new AssetDescriptor(new ProjectDescriptor(new OrganizationId(m_OrganizationId),
                new ProjectId(m_ProjectId)), new AssetId(m_AssetId), new AssetVersion(m_Version));
        }
    }
}