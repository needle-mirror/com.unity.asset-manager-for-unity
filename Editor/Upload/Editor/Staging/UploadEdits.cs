using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    [Serializable]
    // Quick solution to hold manual edits information between two UploadStaging.GenerateUploadAssetData
    // Without this, if the user manually edits assets, then changes the Dependency Mode, edits will be lost
    // Ideally, we should only generate the UploadAssetData once or find a way to re-use the same UploadAssetData instances
    class UploadEdits
    {
        [SerializeField]
        // Assets manually selected by the user
        List<string> m_MainAssetGuids = new();

        [SerializeField]
        // Assets manually ignored by the user
        List<string> m_IgnoredAssetGuids = new();

        [SerializeField]
        // Assets that should include All Scripts
        List<string> m_IncludesAllScripts = new();

        public IReadOnlyCollection<string> MainAssetGuids => m_MainAssetGuids;
        public IReadOnlyCollection<string> IgnoredAssetGuids => m_IgnoredAssetGuids;

        public void AddToSelection(string assetOrFolderGuid)
        {
            // Parse selection to extract folder content
            var mainGuids = UploadAssetStrategy.ResolveMainSelection(assetOrFolderGuid);

            foreach (var guid in mainGuids)
            {
                if (m_MainAssetGuids.Contains(guid))
                    continue;

                m_MainAssetGuids.Add(guid);
            }
        }

        public bool IsSelected(string guid)
        {
            return m_MainAssetGuids.Contains(guid);
        }

        public bool IsEmpty()
        {
            return m_MainAssetGuids.Count == 0;
        }

        public bool RemoveFromSelection(string guid)
        {
            if (!m_MainAssetGuids.Contains(guid))
                return false;

            m_MainAssetGuids.Remove(guid);
            return true;
        }

        public void Clear()
        {
            m_MainAssetGuids.Clear();
            m_IgnoredAssetGuids.Clear();
            m_IncludesAllScripts.Clear();
        }

        public void SetIgnore(string assetGuid, bool ignore)
        {
            if (ignore && !m_IgnoredAssetGuids.Contains(assetGuid))
            {
                m_IgnoredAssetGuids.Add(assetGuid);
            }
            else if (!ignore && m_IgnoredAssetGuids.Contains(assetGuid))
            {
                m_IgnoredAssetGuids.Remove(assetGuid);
            }
        }

        public bool IsIgnored(string assetGuid)
        {
            return m_IgnoredAssetGuids.Contains(assetGuid);
        }

        public bool IncludesAllScripts(string assetDataGuid)
        {
            return m_IncludesAllScripts.Contains(assetDataGuid);
        }

        public void SetIncludesAllScripts(string assetDataGuid, bool include)
        {
            if (include && !m_IncludesAllScripts.Contains(assetDataGuid))
            {
                m_IncludesAllScripts.Add(assetDataGuid);
            }
            else if (!include && m_IncludesAllScripts.Contains(assetDataGuid))
            {
                m_IncludesAllScripts.Remove(assetDataGuid);
            }
        }
    }
}
