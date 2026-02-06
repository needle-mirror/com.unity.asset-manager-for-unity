using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine;
using AssetType = Unity.AssetManager.Core.Editor.AssetType;

namespace Unity.AssetManager.UI.Editor
{
    class AssetInspectorViewModel
    {
        #region SelectedAsset properties
        BaseAssetData m_PreviouslySelectedAssetData;
        BaseAssetData m_SelectedAssetData;
        public BaseAssetData SelectedAssetData
        {
            get => m_SelectedAssetData;
            set
            {
                if (m_SelectedAssetData == value)
                    return;

                if (m_SelectedAssetData != null)
                {
                    m_SelectedAssetData.AssetDataChanged -= OnAssetDataEvent;
                }

                m_SelectedAssetData = value;

                if (m_SelectedAssetData != null)
                {
                    m_SelectedAssetData.AssetDataChanged += OnAssetDataEvent;
                }
            }
        }

        public AssetIdentifier AssetIdentifier => SelectedAssetData?.Identifier;
        public bool AssetIsLocal => AssetIdentifier?.IsLocal() ?? false;
        public bool IsAssetFromLibrary => AssetIdentifier?.IsAssetFromLibrary() ?? false;
        public string AssetName => SelectedAssetData?.Name ?? string.Empty;
        public string AssetDescription => SelectedAssetData?.Description ?? string.Empty;
        public AssetType AssetType => SelectedAssetData?.AssetType ?? AssetType.Other;
        public IEnumerable<string> AssetTags => SelectedAssetData?.Tags ?? Enumerable.Empty<string>();
        public string AssetId => AssetIdentifier?.AssetId;
        public string ProjectId => AssetIdentifier?.ProjectId;
        public IEnumerable<ProjectIdentifier> LinkedProjects => SelectedAssetData?.LinkedProjects;
        public IEnumerable<CollectionIdentifier> LinkedCollections => SelectedAssetData?.LinkedCollections;
        public IEnumerable<BaseAssetData> AssetVersions => SelectedAssetData?.Versions;
        public IEnumerable<AssetDataset> AssetDatasets => SelectedAssetData?.Datasets;
        public Texture2D AssetThumbnail => SelectedAssetData?.Thumbnail;
        public string AssetPrimaryExtension => SelectedAssetData?.PrimaryExtension;
        public AssetDataAttributeCollection AssetAttributes => SelectedAssetData?.AssetDataAttributeCollection;
        public string AssetStatus => SelectedAssetData?.Status;
        public IEnumerable<string> AssetReachableStatus => SelectedAssetData?.ReachableStatusNames;
        public DateTime? AssetUpdatedOn => SelectedAssetData?.Updated;
        public string AssetUpdatedBy => SelectedAssetData?.UpdatedBy;
        public DateTime? AssetCreatedOn => SelectedAssetData?.Created;
        public string AssetCreatedBy => SelectedAssetData?.CreatedBy;

        public bool IsNameEdited(string name)
        {
            if (SelectedAssetData is not UploadAssetData uploadAssetData)
                return false;

            var importedAssetData = GetImportedAssetInfo();
            return !string.Equals(importedAssetData?.AssetData?.Name, name, StringComparison.Ordinal);
        }

        public IEnumerable<BaseAssetDataFile> GetFiles()
        {
            return SelectedAssetData?.GetFiles();
        }

        public bool HasFiles => SelectedAssetData?.HasImportableFiles() ?? false;

        public IEnumerable<AssetPreview.IStatus> GetOverallStatus()
        {
            return SelectedAssetData?.AssetDataAttributeCollection?.GetOverallStatus() ?? Enumerable.Empty<AssetPreview.IStatus>();
        }

        public AssetPreview.IStatus GetImportStatus()
        {
            return SelectedAssetData?.AssetDataAttributeCollection?.GetStatusOfImport();
        }

        #endregion

        public BaseAssetData PreviouslySelectedAssetData => m_PreviouslySelectedAssetData;

        readonly IAssetImporter m_AssetImporter;
        readonly ILinksProxy m_LinksProxy;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IPermissionsManager m_PermissionsManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        // Events
        public event Action OperationStateChanged;
        public event Action AssetDataChanged;
        public event Action StatusInformationUpdated;
        public event Action LocalStatusUpdated;
        public event Action AssetDependenciesUpdated;
        public event Action<Texture2D> PreviewImageUpdated;
        public event Action<IEnumerable<AssetPreview.IStatus>> PreviewStatusUpdated;
        public event Action<AssetDataAttributeCollection> AssetDataAttributesUpdated;
        public event Action PropertiesUpdated;
        public event Action FilesChanged;
        public event Action LinkedProjectsUpdated;
        public event Action VersionsRefreshed;

        public AssetInspectorViewModel()
        {
        }

        public AssetInspectorViewModel(IAssetImporter assetImporter, ILinksProxy linksProxy,
            IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager,
            IProjectOrganizationProvider projectOrganizationProvider, IPermissionsManager permissionsManager,
            IUnityConnectProxy unityConnectProxy)
        {
            m_AssetImporter = assetImporter;
            m_LinksProxy = linksProxy;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PermissionsManager = permissionsManager;
            m_UnityConnectProxy = unityConnectProxy;
        }

        public void ShowInProjectBrowser()
        {
            m_AssetImporter.ShowInProject(SelectedAssetData?.Identifier);

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Show));
        }

        public bool AssetHasValidDashboardLink()
        {
            if (AssetIdentifier == null)
                return false;

            if (AssetIdentifier.IsAssetFromLibrary())
                return true;

            return CanOpenToDashboard() &&
                   !(string.IsNullOrEmpty(AssetIdentifier.OrganizationId) ||
                     string.IsNullOrEmpty(AssetIdentifier.ProjectId) ||
                     string.IsNullOrEmpty(AssetIdentifier.AssetId));
        }

        public bool CanOpenToDashboard()
        {
            return m_LinksProxy.CanOpenAssetManagerDashboard;
        }

        public void LinkToDashboard()
        {
            m_LinksProxy.OpenAssetManagerDashboard(SelectedAssetData?.Identifier);
        }

        public bool RemoveFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            try
            {
                var removeAssets = m_AssetDataManager.FindExclusiveDependencies(new List<AssetIdentifier> {SelectedAssetData?.Identifier});
                return m_AssetImporter.RemoveImports(removeAssets.ToList(), true);
            }
            catch (Exception)
            {
                OperationStateChanged?.Invoke();
                throw;
            }
        }

        public bool RemoveOnlyAssetFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.RemoveSelected));

            try
            {
                return m_AssetImporter.RemoveImport(SelectedAssetData?.Identifier, true);
            }
            catch (Exception)
            {
                OperationStateChanged?.Invoke();
                throw;
            }
        }

        public bool StopTracking()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTracking));

            try
            {
                var removeAssets = m_AssetDataManager.FindExclusiveDependencies(new List<AssetIdentifier> {SelectedAssetData?.Identifier});
                m_AssetImporter.StopTrackingAssets(removeAssets.ToList());
                return true;
            }
            catch (Exception)
            {
                OperationStateChanged?.Invoke();
                throw;
            }
        }

        public bool StopTrackingOnlyAsset()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTrackingSelected));

            try
            {
                m_AssetImporter.StopTrackingAssets(new List<AssetIdentifier> { SelectedAssetData?.Identifier });
                return true;
            }
            catch (Exception)
            {
                OperationStateChanged?.Invoke();
                throw;
            }
        }

        public List<BaseAssetDataFile> GetSelectedAssetFiles()
        {
            return m_SelectedAssetData?.GetFiles()?.Where(f =>
            {
                if (string.IsNullOrEmpty(f?.Path))
                    return false;

                return !AssetDataDependencyHelper.IsASystemFile(Path.GetExtension(f.Path));
            }).ToList();
        }

        public string GetFilesCount()
        {
            var files = GetSelectedAssetFiles();
            if (files == null || !files.Any())
                return "0";

            var totalFilesCount = files.Count;
            var incompleteFilesCount = files.Count(f => !f.Available);

            return incompleteFilesCount > 0
                ? $"{totalFilesCount} [{incompleteFilesCount} incomplete]"
                : totalFilesCount.ToString();
        }

        public string GetFileSize()
        {
            var files = GetSelectedAssetFiles();
            if (files == null || !files.Any())
                return Utilities.BytesToReadableString(0);

            long totalFileSize = 0;
            var assetFileSize = files.Sum(i => i.FileSize);
            totalFileSize += assetFileSize;

            return Utilities.BytesToReadableString(totalFileSize);
        }

        public ImportedAssetInfo GetImportedAssetInfo()
        {
            var assetId = (SelectedAssetData as UploadAssetData)?.ExistingAssetIdentifier?.AssetId ??
                          AssetId;
            return m_AssetDataManager.GetImportedAssetInfo(assetId);
        }

        /// <summary>
        /// Returns overlap details for files in this asset that are also tracked by another asset.
        /// Empty when the asset is not in the project or has no overlapping tracking.
        /// </summary>
        public IReadOnlyList<TrackingOverlapInfo> GetTrackingOverlaps()
        {
            var importedInfo = GetImportedAssetInfo();
            if (importedInfo?.FileInfos == null)
                return Array.Empty<TrackingOverlapInfo>();

            var seen = new HashSet<(string path, string otherAssetId)>();
            var result = new List<TrackingOverlapInfo>();

            foreach (var fileInfo in importedInfo.FileInfos)
            {
                if (string.IsNullOrEmpty(fileInfo?.Guid))
                    continue;

                var trackedAssets = m_AssetDataManager.GetImportedAssetInfosFromFileGuid(fileInfo.Guid);
                if (trackedAssets == null)
                    continue;

                foreach (var info in trackedAssets)
                {
                    if (info?.Identifier == null || TrackedAssetIdentifier.IsFromSameAsset(info.Identifier, importedInfo.Identifier))
                        continue;

                    var key = (fileInfo.OriginalPath ?? string.Empty, info.Identifier.AssetId ?? string.Empty);
                    if (!seen.Add(key))
                        continue;

                    result.Add(new TrackingOverlapInfo
                    {
                        FilePath = fileInfo.OriginalPath ?? string.Empty,
                        ConflictingAssetName = info.AssetData?.Name ?? string.Empty,
                        ConflictingAssetId = info.Identifier.AssetId ?? string.Empty
                    });
                }
            }

            return result;
        }

        public AssetDataOperation GetAssetOperation()
        {
            return m_AssetOperationManager.GetAssetOperation(AssetIdentifier);
        }

        public async Task<bool> CheckPermissionAsync()
        {
            if (AssetIdentifier == null)
                return false;

            return await m_PermissionsManager.CheckPermissionAsync(AssetIdentifier.OrganizationId, AssetIdentifier.ProjectId, Constants.ImportPermission);
        }

        public UIEnabledStates GetUIEnabledStates()
        {
            var importOperation = GetAssetOperation();

            var status = GetImportStatus();
            var enabled = UIEnabledStates.CanImport.GetFlag(SelectedAssetData is AssetData);
            enabled |= UIEnabledStates.InProject.GetFlag(AssetIdentifier != null && !AssetIdentifier.IsLocal() && m_AssetDataManager.IsInProject(AssetIdentifier));
            enabled |= UIEnabledStates.ServicesReachable.GetFlag(m_UnityConnectProxy.AreCloudServicesReachable);
            enabled |= UIEnabledStates.ValidStatus.GetFlag(status == null || !string.IsNullOrEmpty(status.ActionText));
            enabled |= UIEnabledStates.IsImporting.GetFlag(importOperation?.Status == OperationStatus.InProgress);
            enabled |= UIEnabledStates.HasPermissions.GetFlag(false);

            var files = GetFiles()?.ToList();
            if (files == null || !files.Any()) return enabled;

            if (!HasCaseInsensitiveMatch(files.Select(f => f.Path)) // files have unique names
                && files.All(file => file.Available)) // files are all available
            {
                enabled |= UIEnabledStates.CanImport;
            }
            else
            {
                enabled &= ~UIEnabledStates.CanImport;
            }

            return enabled;
        }

        public bool HasCaseInsensitiveMatch(IEnumerable<string> files)
        {
            if (files == null)
                return false;

            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return files.Any(file => !seenFiles.Add(file));
        }

        #region Loading Tasks

        public async Task RefreshAssetVersionsAsync()
        {
            if (SelectedAssetData == null)
                return;

            await SelectedAssetData.RefreshVersionsAsync();
            VersionsRefreshed?.Invoke();
        }

        public async Task RefreshInfosAsync(bool completeRefresh)
        {
            if (SelectedAssetData == null)
                return;

            var tasks = new List<Task>
            {
                SelectedAssetData.GetThumbnailAsync(),
                SelectedAssetData.RefreshAssetDataAttributesAsync(),
                SelectedAssetData.RefreshLinkedProjectsAsync(),
                SelectedAssetData.RefreshLinkedCollectionsAsync(),
                SelectedAssetData.RefreshReachableStatusNamesAsync(),
            };
            if (completeRefresh)
            {
                tasks.Add(SelectedAssetData.RefreshPropertiesAsync());
                tasks.Add(SelectedAssetData.ResolveDatasetsAsync());
                tasks.Add(SelectedAssetData.RefreshDependenciesAsync());
            }

            await TaskUtils.WaitForTasksWithHandleExceptions(tasks);
        }

        #endregion

        #region Import
                public async void ImportAssetAsync(ImportTrigger importTrigger, string importDestination, List<BaseAssetData> assetsToImport = null)
        {
            m_PreviouslySelectedAssetData = SelectedAssetData;
            try
            {
                var settings = new ImportSettings
                {
                    DestinationPathOverride = importDestination,
                    Type = assetsToImport == null ? ImportOperation.ImportType.UpdateToLatest : ImportOperation.ImportType.Import
                };

                // If assets have been targeted for import, we use the first asset as the selected asset
                if (assetsToImport != null)
                    SelectedAssetData = assetsToImport.FirstOrDefault();
                else
                    assetsToImport = new List<BaseAssetData> { SelectedAssetData };

                var importResult = await m_AssetImporter.StartImportAsync(importTrigger, assetsToImport.ToList(), settings);
                SelectedAssetData = importResult.Assets?.FirstOrDefault() ?? m_PreviouslySelectedAssetData;

                if (importResult.Assets == null || !importResult.Assets.Any())
                {
                    AssetDataChanged?.Invoke();
                }
                else
                {
                    await SelectedAssetData.RefreshVersionsAsync();
                    OperationStateChanged?.Invoke();
                }
            }
            catch (Exception)
            {
                SelectedAssetData = m_PreviouslySelectedAssetData;
                AssetDataChanged?.Invoke();
                throw;
            }
        }

        public void CancelOrClearImport(AssetIdentifier assetId)
        {
            var operation = m_AssetOperationManager.GetAssetOperation(assetId);
            if (operation == null)
                return;

            if (operation.Status == OperationStatus.InProgress)
            {
                m_AssetImporter.CancelImport(assetId, true);
            }
            else
            {
                m_AssetOperationManager.ClearFinishedOperations();
            }
        }
        #endregion

        #region Asset Data Changed Events

        void OnAssetDataEvent(BaseAssetData assetData, AssetDataEventType eventType)
        {
            if (assetData != SelectedAssetData)
                return;

            switch (eventType)
            {
                case AssetDataEventType.FilesChanged:
                case AssetDataEventType.PrimaryFileChanged:  // Intentional fallthrough
                    FilesChanged?.Invoke();
                    break;
                case AssetDataEventType.LinkedProjectsChanged:
                    LinkedProjectsUpdated?.Invoke();
                    break;
                case AssetDataEventType.PropertiesChanged:
                    PropertiesUpdated?.Invoke();
                    break;
                case AssetDataEventType.AssetDataAttributesChanged:
                    PreviewStatusUpdated?.Invoke(GetOverallStatus());
                    AssetDataAttributesUpdated?.Invoke(AssetAttributes);
                    break;
                case AssetDataEventType.ThumbnailChanged:
                    PreviewImageUpdated?.Invoke(AssetThumbnail);
                    break;
                case AssetDataEventType.DependenciesChanged:
                    AssetDependenciesUpdated?.Invoke();
                    break;
                case AssetDataEventType.LocalStatusChanged:
                    LocalStatusUpdated?.Invoke();
                    break;
                case AssetDataEventType.StatusInformationChanged:
                    StatusInformationUpdated?.Invoke();
                    break;
            }
        }

        #endregion
    }
}
