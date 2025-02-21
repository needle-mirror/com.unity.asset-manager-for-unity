using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    [Serializable]
    class UploadStaging
    {
        public event Action RefreshStatusStarted;
        public event Action<string, float> RefreshStatusProgress;
        public event Action RefreshStatusFinished;
        public event Action UploadAssetEntriesChanged;
        public event Action StagingStatusChanged;

        [SerializeField]
        UploadSettings m_Settings = new();

        [SerializeField]
        AssetDataCollection<UploadAssetData> m_UploadAssets = new();

        [SerializeField]
        UploadEdits m_UploadEdits = new();

        [SerializeField]
        UploadStagingStatus m_StagingStatus;

        public IUploadStagingStatus StagingStatus => m_StagingStatus;

        public IReadOnlyCollection<BaseAssetData> UploadAssets => m_UploadAssets;

        public UploadDependencyMode DependencyMode
        {
            get => m_Settings.DependencyMode;
            set => m_Settings.DependencyMode = value;
        }

        public UploadFilePathMode FilePathMode
        {
            get => m_Settings.FilePathMode;
            set
            {
                if (m_Settings.FilePathMode == value)
                    return;

                m_Settings.FilePathMode = value;

                foreach (var uploadAssetData in m_UploadAssets)
                {
                    uploadAssetData.FilePathMode = value;
                }
            }
        }

        public UploadAssetMode UploadMode
        {
            get => m_Settings.UploadMode;
            set => m_Settings.UploadMode = value;
        }

        public string ProjectId => m_Settings.ProjectId;
        public string CollectionPath => m_Settings.CollectionPath;

        const string k_UploadPermission = "amc.assets.create";

        void SetStagingStatus(UploadStagingStatus status)
        {
            m_StagingStatus = status;
            StagingStatusChanged?.Invoke();
        }

        public void AddToSelection(string guid)
        {
            m_UploadEdits.AddToSelection(guid);
        }

        public bool IsSelected(string guid)
        {
            return m_UploadEdits.IsSelected(guid);
        }

        public bool IsEmpty()
        {
            return m_UploadEdits.IsEmpty();
        }

        public bool RemoveFromSelection(string guid)
        {
            return m_UploadEdits.RemoveFromSelection(guid);
        }

        public void Clear()
        {
            m_UploadEdits.Clear();
            m_UploadAssets.Clear();
        }

        public void SetIgnore(AssetIdentifier assetIdentifier, bool ignore)
        {
            var assetData = m_UploadAssets.Find(uploadAssetData => uploadAssetData.Identifier == assetIdentifier);

            m_UploadEdits.SetIgnore(assetData.Guid, ignore);

            assetData.IsIgnored = ignore;
        }

        public void GenerateUploadAssetData(Action<string, float> progressCallback)
        {
            // Check we actually need to regenerate the list
            var uploadAssetData = UploadAssetStrategy.GenerateUploadAssets(m_UploadEdits.MainAssetGuids,
                m_UploadEdits.IgnoredAssetGuids, m_Settings, progressCallback).ToList();

            // Restore manual edits made by the user
            foreach (var assetData in uploadAssetData)
            {
                assetData.IsIgnored = m_UploadEdits.IsIgnored(assetData.Guid);

                if (m_UploadEdits.IncludesAllScripts(assetData.Guid))
                {
                    AddAllScriptsInternal(assetData);
                }

                if (m_UploadEdits.TryGetModifiedMetadata(assetData.Guid, m_Settings.ProjectId, out var metadata))
                {
                    assetData.SetMetadata(metadata);
                }
            }

            // Because we are not able to detect if a user modify a metadata, we need to store the metadata for ALL UploadAssetData
            foreach (var assetData in uploadAssetData)
            {
                m_UploadEdits.SetModifiedMetadata(assetData.Guid, m_Settings.ProjectId, assetData.Metadata);
            }

            m_UploadAssets.SetValues(uploadAssetData);
            UploadAssetEntriesChanged?.Invoke();
        }

        public void SetIncludeAllScripts(AssetIdentifier identifier, bool include)
        {
            var assetData = m_UploadAssets.Find(uploadAssetData => uploadAssetData.Identifier == identifier);

            if (assetData == null)
                return;

            m_UploadEdits.SetIncludesAllScripts(assetData.Guid, include);

            if (include)
            {
                AddAllScriptsInternal(assetData);
            }
            else
            {
                assetData.RemoveAllAdditionalFiles();
            }
        }

        static void AddAllScriptsInternal(UploadAssetData assetData)
        {
            assetData.AddFiles(DependencyUtils.GetAllScriptGuids(), false);
        }

        public void AddMetadata(AssetIdentifier identifier, IMetadata metadata)
        {
            var assetData = m_UploadAssets.Find(uploadAssetData => uploadAssetData.Identifier == identifier);

            if (assetData == null)
                return;

            assetData.AddMetadata(metadata);
        }

        public void RemoveMetadata(AssetIdentifier identifier, string fieldKey)
        {
            var assetData = m_UploadAssets.Find(uploadAssetData => uploadAssetData.Identifier == identifier);

            if (assetData == null)
                return;

            assetData.RemoveMetadata(fieldKey);
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
        }

        UploadStagingStatus GenerateStagingStatus()
        {
            var readyAssets = m_UploadAssets.Where(asset => asset.CanBeUploaded).ToList();

            var status = new UploadStagingStatus(m_Settings.OrganizationId, m_Settings.ProjectId)
            {
                TotalAssetCount = m_UploadAssets.Count,
                IgnoredAssetCount = m_UploadAssets.Count(asset => m_UploadEdits.IsIgnored(asset.Guid)),
                SkippedAssetCount = m_UploadAssets.Count(asset => asset.UploadStatus is UploadAttribute.UploadStatus.Skip or UploadAttribute.UploadStatus.SourceControlled),
                UpdatedAssetCount = m_UploadAssets.Count(asset => !asset.IsIgnored && asset.UploadStatus == UploadAttribute.UploadStatus.Override),
                AddedAssetCount = m_UploadAssets.Count(asset => !asset.IsIgnored && asset.UploadStatus == UploadAttribute.UploadStatus.Add),
                ManuallyIgnoredDependencyCount = m_UploadAssets.Count(asset => asset.IsDependency && m_UploadEdits.IsIgnored(asset.Guid)),
                ReadyAssetCount = readyAssets.Count(asset => asset.CanBeUploaded),
                HasFilesOutsideProject = m_UploadAssets.Exists(asset => asset.UploadStatus == UploadAttribute.UploadStatus.ErrorOutsideProject),
                TotalFileCount = readyAssets.Sum(asset => asset.SourceFiles.Count()),
                TotalSize = readyAssets.Sum(asset => asset.SourceFiles.Sum(f => f.FileSize))
            };

            return status;
        }

        public async Task<bool> CheckPermissionToUploadAsync()
        {
            var permissionsManager = ServicesContainer.instance.Resolve<IPermissionsManager>();
            return await permissionsManager.CheckPermissionAsync(m_Settings.OrganizationId, m_Settings.ProjectId, k_UploadPermission);
        }

        public bool HasDirtyAssets()
        {
            var guids = m_UploadAssets.SelectMany(uploadAsset => uploadAsset.SourceFiles.Select(f => f.Guid)).ToList();

            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            return guids.Exists(guid => Utilities.IsFileDirty(assetDatabaseProxy.GuidToAssetPath(guid)));
        }

        public void SaveDirtyAssets()
        {
            var guids = m_UploadAssets.SelectMany(uploadAsset => uploadAsset.SourceFiles.Select(f => f.Guid)).ToList();

            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            foreach (var path in guids.Select(assetDatabaseProxy.GuidToAssetPath))
            {
                Utilities.SaveAssetIfDirty(path);
            }
        }

        public void SetCollectionPath(string collection)
        {
            m_Settings.CollectionPath = collection;
        }

        public IReadOnlyCollection<IUploadAsset> GenerateUploadAssets()
        {
            return m_UploadAssets
                .Where(assetData => assetData.CanBeUploaded)
                .Select(assetData => assetData.GenerateUploadAsset(m_Settings.CollectionPath))
                .ToList();
        }

        public void RebuildAssetList(IAssetDataManager assetDataManager)
        {
            // We want the UploadAssets to always use instance from the AssetDataManager.
            // Ideally we have called this in OnAfterDeserialize, but we can't because AssetDataManager might not be available at that time.
            m_UploadAssets.RebuildAssetList(assetDataManager);

            // It is possible that m_UploadAssets contains removed references from the AssetDataManager (like when switching page)
            // Make we clear the list if it's case
            if (m_UploadAssets.Contains(null))
            {
                m_UploadAssets.Clear();
            }
        }

        public async Task RefreshStatusAsync(bool checkWithCloud, CancellationToken token)
        {
            if (m_UploadAssets.Count == 0)
                return;

            RefreshStatusStarted?.Invoke();

            SetStagingStatus(null);

            foreach (var uploadAssetData in m_UploadAssets)
            {
                uploadAssetData.ResetAssetDataAttributes();
            }

            var total = m_UploadAssets.Count;
            var count = 0f;

            RefreshStatusProgress?.Invoke(string.Empty, 0);

            await TaskUtils.RunWithMaxConcurrentTasksAsync(m_UploadAssets, token,
                uploadAssetData => ResolveSelfStatusTask(uploadAssetData, m_Settings.UploadMode, checkWithCloud, item =>
                {
                    RefreshStatusProgress?.Invoke(item.Name, ++count / total);
                }, token),
                200);

            // Update every asset status depending on its dependencies
            var processed = new HashSet<UploadAssetData>();
            foreach (var uploadAssetData in m_UploadAssets)
            {
                ResolveFinalStatusRecursive(uploadAssetData, m_Settings.UploadMode, processed);
            }

            foreach (var uploadAssetData in m_UploadAssets)
            {
                // Optionally, we can force ignore assets that are not going to be uploaded
                // Status change have an effect on the ignore and canBeIgnored status
                // Make sure to notify the change
                uploadAssetData.NotifyIgnoredChanged();
            }

            SetStagingStatus(GenerateStagingStatus());

            RefreshStatusFinished?.Invoke();
        }

        void ResolveFinalStatusRecursive(UploadAssetData assetData, UploadAssetMode uploadMode, HashSet<UploadAssetData> processed)
        {
            if (!processed.Add(assetData))
                return;

            foreach (var identifier in assetData.Dependencies)
            {
                var depAssetData = m_UploadAssets.Find(uploadAssetData => uploadAssetData.Identifier == identifier);
                ResolveFinalStatusRecursive(depAssetData, uploadMode, processed);
            }

            assetData.ResolveFinalStatus(uploadMode);
        }

        static async Task ResolveSelfStatusTask(UploadAssetData uploadAssetData, UploadAssetMode uploadMode,
            bool checkWithCloud, Action<UploadAssetData> onItemFinished, CancellationToken token)
        {
            await uploadAssetData.ResolveSelfStatus(uploadMode, checkWithCloud, token);
            onItemFinished?.Invoke(uploadAssetData);
        }

        public void ResetDefaultSettings()
        {
            m_Settings?.ResetToDefault();
        }
    }
}
