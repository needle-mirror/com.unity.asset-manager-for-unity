using System;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class AssetIdentifier : IEquatable<AssetIdentifier>
    {
        [SerializeField]
        string m_AssetId;

        [SerializeField]
        string m_Version;

        [SerializeField]
        string m_OrganizationId;

        [SerializeField]
        string m_ProjectId;

        public string AssetId => m_AssetId ?? string.Empty;
        public string Version => m_Version ?? string.Empty;
        public string OrganizationId => m_OrganizationId ?? string.Empty;
        public string ProjectId => m_ProjectId ?? string.Empty;

        public AssetIdentifier() { }

        public AssetIdentifier(AssetDescriptor descriptor)
        {
            m_AssetId = descriptor.AssetId.ToString();
            m_Version = descriptor.AssetVersion.ToString();
            m_OrganizationId = descriptor.OrganizationId.ToString();
            m_ProjectId = descriptor.ProjectId.ToString();
        }

        public AssetIdentifier(string organizationId, string projectId, string assetId, string version)
        {
            m_AssetId = assetId;
            m_Version = version;
            m_OrganizationId = organizationId;
            m_ProjectId = projectId;
        }

        public virtual bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return IsSameId(m_OrganizationId, other.m_OrganizationId)
                   && IsSameId(m_ProjectId, other.m_ProjectId)
                   && IsSameId(m_AssetId, other.m_AssetId)
                   && IsSameId(m_Version, other.m_Version);
        }

        public virtual bool IsIdValid()
        {
            return !string.IsNullOrEmpty(m_AssetId);
        }

        static bool IsSameId(string str1, string str2)
        {
            return (str1 ?? string.Empty) == (str2 ?? string.Empty);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((AssetIdentifier)obj);
        }

        public override int GetHashCode()
        {
            var orgIdHash = (OrganizationId ?? string.Empty).GetHashCode();
            var projIdHash = (ProjectId ?? string.Empty).GetHashCode();
            var assetIdHash = (AssetId ?? string.Empty).GetHashCode();
            var versionHash = (Version ?? string.Empty).GetHashCode();

            return HashCode.Combine(orgIdHash, projIdHash, assetIdHash, versionHash);
        }

        public override string ToString()
        {
            return $"[Org:{OrganizationId}, Proj:{m_ProjectId}, Id:{m_AssetId}, Ver:{m_Version}]";
        }

        public AssetDescriptor ToAssetDescriptor()
        {
            return new AssetDescriptor(new ProjectDescriptor(new OrganizationId(m_OrganizationId),
                new ProjectId(m_ProjectId)), new AssetId(m_AssetId), new AssetVersion(m_Version));
        }
    }
}
