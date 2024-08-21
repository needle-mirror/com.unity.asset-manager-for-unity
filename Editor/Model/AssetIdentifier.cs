using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class AssetIdentifier : IEquatable<AssetIdentifier>
    {
        [SerializeField]
        ProjectIdentifier m_ProjectIdentifier = new();

        [SerializeField]
        string m_AssetId = string.Empty;

        [SerializeField]
        string m_Version = string.Empty;

        [SerializeField]
        string m_PrimarySourceFileGuid = string.Empty;

        public string AssetId => m_AssetId;
        public string Version => m_Version;
        public string OrganizationId => m_ProjectIdentifier.OrganizationId;
        public string ProjectId => m_ProjectIdentifier.ProjectId;
        public string PrimarySourceFileGuid => m_PrimarySourceFileGuid;
        public ProjectIdentifier ProjectIdentifier => m_ProjectIdentifier;

        public AssetIdentifier() { }

        public AssetIdentifier(string primarySourceFileGuid)
            : this(null, null, null, "1", primarySourceFileGuid) { }

        public AssetIdentifier(string organizationId, string projectId, string assetId, string version)
            : this(organizationId, projectId, assetId, version, null) { }

        AssetIdentifier(string organizationId, string projectId, string assetId, string version, string primarySourceFileGuid)
        {
            m_ProjectIdentifier = new ProjectIdentifier(organizationId, projectId);
            m_AssetId = assetId ?? string.Empty;
            m_Version = version ?? string.Empty;
            m_PrimarySourceFileGuid = primarySourceFileGuid ?? string.Empty;
        }

        public override string ToString()
        {
            return $"[Org:{OrganizationId}, Proj:{ProjectId}, Id:{AssetId}, Ver:{Version}, PrimFileGuid:{PrimarySourceFileGuid}]";
        }

        public AssetIdentifier WithAssetId(string assetId)
        {
            return new AssetIdentifier(m_ProjectIdentifier.OrganizationId, m_ProjectIdentifier.ProjectId, assetId, m_Version, m_PrimarySourceFileGuid);
        }

        public AssetIdentifier WithVersion(string version)
        {
            return new AssetIdentifier(m_ProjectIdentifier.OrganizationId, m_ProjectIdentifier.ProjectId, m_AssetId, version, m_PrimarySourceFileGuid);
        }

        bool IsIdValid()
        {
            return !string.IsNullOrEmpty(m_AssetId);
        }

        bool IsPrimGuidValid()
        {
            return !string.IsNullOrEmpty(m_PrimarySourceFileGuid);
        }

        public bool IsAnyIdValid()
        {
            return IsIdValid() || IsPrimGuidValid();
        }

        public bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(m_ProjectIdentifier, other.m_ProjectIdentifier) &&
                m_AssetId == other.m_AssetId &&
                m_Version == other.m_Version &&
                m_PrimarySourceFileGuid == other.m_PrimarySourceFileGuid;
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

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((AssetIdentifier)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(m_ProjectIdentifier, m_AssetId, m_Version, m_PrimarySourceFileGuid);
        }

        public static bool operator ==(AssetIdentifier left, AssetIdentifier right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AssetIdentifier left, AssetIdentifier right)
        {
            return !Equals(left, right);
        }
    }
}
