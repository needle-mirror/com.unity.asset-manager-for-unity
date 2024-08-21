using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadContext
    {
        [SerializeField]
        UploadSettings m_Settings;

        [SerializeReference]
        List<IUploadAsset> m_UploadAssets = new();

        [SerializeField]
        // Assets manually selected by the user
        List<string> m_MainAssetGuids = new();

        [SerializeField]
        // Assets manually ignored by the user
        List<string> m_IgnoredAssetGuids = new();

        [SerializeField]
        // Assets that was indirectly added, like dependencies
        List<string> m_DependencyAssetGuids = new();

        public IReadOnlyCollection<IUploadAsset> UploadAssets => m_UploadAssets;

        public IReadOnlyCollection<string> IgnoredAssetGuids => m_IgnoredAssetGuids;

        public UploadSettings Settings => m_Settings;
        public string ProjectId => m_Settings.ProjectId;
        public string CollectionPath => m_Settings.CollectionPath;

        public event Action ProjectIdChanged;
        public event Action UploadAssetEntriesChanged;

        public UploadContext()
        {
            m_Settings = new UploadSettings();
        }

        public bool AddToSelection(string guid)
        {
            if (m_MainAssetGuids.Contains(guid))
                return false;

            m_MainAssetGuids.Add(guid);
            return true;
        }

        public bool IsSelected(string guid)
        {
            return m_MainAssetGuids.Contains(guid);
        }

        public bool IsDependency(string guid)
        {
            return m_DependencyAssetGuids.Contains(guid);
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

        public void ClearSelection()
        {
            m_MainAssetGuids.Clear();
            m_DependencyAssetGuids.Clear();
        }

        public void ClearAll()
        {
            m_MainAssetGuids.Clear();
            m_DependencyAssetGuids.Clear();
            m_IgnoredAssetGuids.Clear();
        }

        public void AddToIgnoreList(string guid)
        {
            if (m_IgnoredAssetGuids.Contains(guid))
                return;

            m_IgnoredAssetGuids.Add(guid);
        }

        public bool IsIgnored(string guid)
        {
            return m_IgnoredAssetGuids.Contains(guid);
        }

        public bool HasIgnoredDependencies()
        {
            return m_IgnoredAssetGuids.Exists(guid => m_DependencyAssetGuids.Contains(guid));
        }

        public void RemoveFromIgnoreList(string guid)
        {
            if (!m_IgnoredAssetGuids.Contains(guid))
                return;

            m_IgnoredAssetGuids.Remove(guid);
        }

        public void SetUploadAssetEntries(IEnumerable<IUploadAsset> uploadAssetEntries)
        {
            m_UploadAssets.Clear();
            m_UploadAssets.AddRange(uploadAssetEntries);
            UploadAssetEntriesChanged?.Invoke();
        }

        public void SetOrganizationInfo(OrganizationInfo organizationInfo)
        {
            if (organizationInfo != null)
            {
                m_Settings.OrganizationId = organizationInfo.Id;
            }
        }

        public void SetProjectId(string id)
        {
            m_Settings.ProjectId = id;
            ProjectIdChanged?.Invoke();
        }

        public void SetCollectionPath(string collection)
        {
            m_Settings.CollectionPath = collection;
        }

        public IEnumerable<string> ResolveFullAssetSelection()
        {
            m_DependencyAssetGuids.Clear();

            var processed = new HashSet<string>();

            // Process main assets first
            foreach (var mainGuid in m_MainAssetGuids)
            {
                var assetGuids = ProcessAssetsAndFolders(mainGuid);

                foreach (var guid in assetGuids)
                {
                    if (processed.Contains(guid))
                        continue;

                    processed.Add(guid);

                    yield return guid;
                }
            }

            if (m_Settings.DependencyMode != UploadDependencyMode.Separate)
                yield break;

            var mainGuids = processed.ToList();

            // Process Dependencies
            foreach (var guid in mainGuids)
            {
                var allDependencies = Utilities.GetValidAssetDependencyGuids(guid, true);

                foreach (var depGuid in allDependencies)
                {
                    if (processed.Contains(depGuid))
                        continue;

                    processed.Add(depGuid);

                    m_DependencyAssetGuids.Add(depGuid);

                    yield return depGuid;
                }
            }
        }

        static IEnumerable<string> ProcessAssetsAndFolders(string guid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.IsValidFolder(assetPath)
                ? AssetDatabaseProxy.GetAssetsInFolder(assetPath)
                : new[] { guid };
        }
    }
}