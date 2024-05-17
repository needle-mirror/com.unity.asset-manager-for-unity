using System;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class LocalAssetIdentifier : AssetIdentifier
    {
        [SerializeField]
        string m_Guid;

        public string Guid => m_Guid ?? string.Empty;
        public override bool IsIdValid() => !string.IsNullOrEmpty(m_Guid);

        public LocalAssetIdentifier(string organizationId, string projectId, string assetId, string version, string guid)
            : base(organizationId, projectId, assetId, version)
        {
            m_Guid = guid;
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

            return Equals((LocalAssetIdentifier) obj);
        }

        public bool Equals(LocalAssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && m_Guid == other.m_Guid;
        }

        public override bool Equals(AssetIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is LocalAssetIdentifier localAssetIdentifier)
            {
                return Equals(localAssetIdentifier);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var guidHash = (Guid ?? string.Empty).GetHashCode();
            return HashCode.Combine(base.GetHashCode(), guidHash);
        }

        public override string ToString()
        {
            return $"{base.ToString()}, GUID:{m_Guid}";
        }
    }
}
