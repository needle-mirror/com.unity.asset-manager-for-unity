using System;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class TrackedAssetIdentifier : IEquatable<TrackedAssetIdentifier>, IEquatable<AssetIdentifier>, IEquatable<LocalAssetIdentifier>
    {
        [SerializeField]
        string m_Guid;

        [SerializeField]
        string m_AssetId;

        [SerializeField]
        string m_ProjectId;

        [SerializeField]
        string m_OrganizationId;

        public string Guid => m_Guid ?? string.Empty;
        public string AssetId => m_AssetId ?? string.Empty;
        public string ProjectId => m_ProjectId ?? string.Empty;
        public string OrganizationId => m_OrganizationId ?? string.Empty;

        public TrackedAssetIdentifier() { }

        public TrackedAssetIdentifier(AssetIdentifier identifier)
            : this(identifier.OrganizationId, identifier.ProjectId, identifier.AssetId, string.Empty)
        {
            if (identifier is LocalAssetIdentifier localIdentifier)
            {
                m_Guid = localIdentifier.Guid;
            }
        }

        public TrackedAssetIdentifier(string organizationId, string projectId, string assetId, string guid)
        {
            m_Guid = guid;
            m_AssetId = assetId;
            m_ProjectId = projectId;
            m_OrganizationId = organizationId;
        }

        public virtual bool IsIdValid()
        {
            return !string.IsNullOrEmpty(m_AssetId) || !string.IsNullOrEmpty(m_Guid);
        }

        static bool IsSameId(string str1, string str2)
        {
            return (str1 ?? string.Empty) == (str2 ?? string.Empty);
        }

        public virtual bool Equals(TrackedAssetIdentifier other)
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
                   && IsSameId(m_Guid, other.m_Guid);
        }

        public virtual bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (other is LocalAssetIdentifier localIdentifier)
            {
                return Equals(localIdentifier);
            }

            return IsSameId(m_OrganizationId, other.OrganizationId)
                   && IsSameId(m_ProjectId, other.ProjectId)
                   && IsSameId(m_AssetId, other.AssetId);
        }

        public virtual bool Equals(LocalAssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            return IsSameId(m_OrganizationId, other.OrganizationId)
                   && IsSameId(m_ProjectId, other.ProjectId)
                   && IsSameId(m_AssetId, other.AssetId)
                   && IsSameId(m_Guid, other.Guid);
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

            return obj switch
            {
                LocalAssetIdentifier identifier => Equals(identifier),
                AssetIdentifier identifier => Equals(identifier),
                TrackedAssetIdentifier identifier => Equals(identifier),
                _ => false
            };
        }

        public override int GetHashCode()
        {
            var orgIdHash = (OrganizationId ?? string.Empty).GetHashCode();
            var projIdHash = (ProjectId ?? string.Empty).GetHashCode();
            var assetIdHash = (AssetId ?? string.Empty).GetHashCode();
            var guidHash = (Guid ?? string.Empty).GetHashCode();

            return HashCode.Combine(orgIdHash, projIdHash, assetIdHash, guidHash);
        }

        public override string ToString()
        {
            return $"[Org:{OrganizationId}, Proj:{m_ProjectId}, Id:{m_AssetId}], GUID:{m_Guid}";
        }
    }
}
