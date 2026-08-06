using System;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Identifies a tracked asset, independently of its version.
    /// Identity is the organization and the asset id only. <see cref="ProjectId"/> is carried for
    /// diagnostics and so fetches can try the last known project first, but it is deliberately not
    /// compared: an asset can be linked to several projects and can be moved between them after
    /// import, so a project is a hint about where to look, never part of what the asset is.
    /// </summary>
    [Serializable]
    class TrackedAssetIdentifier : IEquatable<TrackedAssetIdentifier>, IEquatable<AssetIdentifier>
    {
        [SerializeField]
        string m_AssetId;

        [SerializeField]
        string m_ProjectId;

        [SerializeField]
        string m_OrganizationId;

        public string AssetId => m_AssetId ?? string.Empty;

        /// <summary>
        /// The last known project of the asset. Not part of the identity; see the class summary.
        /// </summary>
        public string ProjectId => m_ProjectId ?? string.Empty;

        public string OrganizationId => m_OrganizationId ?? string.Empty;

        public TrackedAssetIdentifier() { }

        public TrackedAssetIdentifier(AssetIdentifier identifier)
            : this(identifier.OrganizationId, identifier.ProjectId, identifier.AssetId)
        {
        }

        public TrackedAssetIdentifier(string organizationId, string projectId, string assetId)
        {
            m_AssetId = assetId;
            m_ProjectId = projectId;
            m_OrganizationId = organizationId;
        }

        public virtual bool IsIdValid()
        {
            return !string.IsNullOrEmpty(m_AssetId);
        }

        static bool IsSameId(string str1, string str2)
        {
            return (str1 ?? string.Empty) == (str2 ?? string.Empty);
        }

        public static bool IsFromSameAsset(AssetIdentifier first, AssetIdentifier second)
        {
            return new TrackedAssetIdentifier(first).Equals(new TrackedAssetIdentifier(second));
        }

        public static bool IsFromSameAsset(TrackedAssetIdentifier first, AssetIdentifier second)
        {
            return first.Equals(new TrackedAssetIdentifier(second));
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

            // The project is intentionally not compared; see the class summary.
            return IsSameId(m_OrganizationId, other.m_OrganizationId)
                   && IsSameId(m_AssetId, other.m_AssetId);
        }

        public virtual bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            // The project is intentionally not compared; see the class summary.
            return IsSameId(m_OrganizationId, other.OrganizationId)
                   && IsSameId(m_AssetId, other.AssetId);
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
                AssetIdentifier identifier => Equals(identifier),
                TrackedAssetIdentifier identifier => Equals(identifier),
                _ => false
            };
        }

        public override int GetHashCode()
        {
            // Must hash exactly the fields Equals compares; the project is excluded on purpose.
            var orgIdHash = (OrganizationId ?? string.Empty).GetHashCode();
            var assetIdHash = (AssetId ?? string.Empty).GetHashCode();

            return HashCode.Combine(orgIdHash, assetIdHash);
        }

        public override string ToString()
        {
            return $"[Org:{OrganizationId}, Id:{m_AssetId}] (tracked proj:{m_ProjectId})";
        }
    }
}
