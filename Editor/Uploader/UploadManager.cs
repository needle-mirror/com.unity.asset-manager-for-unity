using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IUploadManager : IService
    {
        event Action UploadBegan;
        event Action UploadEnded;

        bool IsUploading { get; }

        void CancelUpload();
        Task UploadAsync(IReadOnlyCollection<IUploadAsset> uploadEntries, UploadSettings settings);
    }

    enum UploadAssetMode
    {
        SkipIdentical, // Upload a new version of asset and skips any existing asset on the cloud that are identical
        ForceNewVersion, // Always force a new version on the asset
        ForceNewAsset, // Uploads new assets and potentially duplicates without checking for existing matches
    }

    enum UploadDependencyMode
    {
        Ignore, // Do not add any dependencies
        Separate, // Add dependencies as separate assets
        Embedded, // Add dependencies as files in the parent asset
    }

    enum UploadFilePathMode
    {
        Full, // Keep the path relative to the project Assets folder
        Compact, // Reduce files nesting by removing common path parts
        Flatten, // Flatten all files to the root of the asset and rename them in case of collision
    }

    [Serializable]
    class UploadManager : BaseService<IUploadManager>, IUploadManager
    {
        class AssetUploadInfo
        {
            public AssetUploadInfo(IUploadAsset uploadAsset, AssetData assetData, bool assetRecycled, bool skip)
            {
                UploadAsset = uploadAsset;
                AssetData = assetData;
                AssetRecycled = assetRecycled;
                Skip = skip;
            }

            public IUploadAsset UploadAsset { get; }
            public AssetData AssetData { get; }
            public bool AssetRecycled { get; }
            public bool Skip { get; }
        }

        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [SerializeReference]
        IImportedAssetsTracker m_ImportTracker;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IPageManager m_PageManager;

        CancellationTokenSource m_TokenSource;

        bool m_Uploading;

        public bool IsUploading => m_Uploading;
        public event Action UploadBegan;
        public event Action UploadEnded;

        // Increasing this number can help speed up the whole process but mainly leads to timeouts and errors.
        // We prefer to upload steadily and safely rather than quickly and with potential issues.
        // Note that this number only controls how many assets are created/uploaded at the same time, and doesn't control how much files are uploaded simultaneously.
        static readonly int k_MaxConcurrentTasks = 5;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager, IImportedAssetsTracker importTracker, IAssetsProvider assetsProvider, IAssetDataManager assetDataManager, IPageManager pageManager)
        {
            m_AssetOperationManager = assetOperationManager;
            m_ImportTracker = importTracker;
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_PageManager = pageManager;
        }

        public async Task UploadAsync(IReadOnlyCollection<IUploadAsset> uploadEntries, UploadSettings settings)
        {
            if (m_Uploading)
                return;

            m_Uploading = true;
            UploadBegan?.Invoke();

            var uploadEntryToAssetUploadInfoLookup = new Dictionary<IUploadAsset, AssetUploadInfo>();
            var uploadEntryToOperationLookup = new Dictionary<IUploadAsset, UploadOperation>();
            var guidToAssetLookup = new Dictionary<string, AssetData>();

            m_TokenSource = new CancellationTokenSource();
            var token = m_TokenSource.Token;

            try
            {
                var assetEntriesWithAllDependencies = new List<IUploadAsset>();

                var targetProject = new ProjectIdentifier(settings.OrganizationId, settings.ProjectId);
                var targetCollection = settings.CollectionPath;

                var database = uploadEntries.ToDictionary(entry => entry.Guid);

                // Get all assets, including their dependencies
                foreach (var uploadEntry in uploadEntries)
                {
                    AddDependencies(uploadEntry, assetEntriesWithAllDependencies, database);
                }

                // Prepare the IAssets
                var createAssetTasks = await TaskUtils.RunWithMaxConcurrentTasksAsync(assetEntriesWithAllDependencies, token,
                    (uploadEntry) =>
                    {
                        UploadOperation operation = null;

                        var skipped = false;
                        foreach (var assetData in m_PageManager.ActivePage.AssetList)
                        {
                            if (assetData is UploadAssetData uploadAssetData &&
                                uploadEntry.Guid == uploadAssetData.Guid &&
                                uploadAssetData.IsSkipped)
                            {
                                skipped = true;
                                break;
                            }
                        }

                        if (!skipped)
                        {
                            operation = StartNewOperation(uploadEntry);
                            uploadEntryToOperationLookup[uploadEntry] = operation;
                        }

                        var assetUploadInfo = CreateOrRecycleAsset(operation, uploadEntry, settings.UploadMode, targetProject,
                            targetCollection, token);

                        return assetUploadInfo;
                    }, k_MaxConcurrentTasks);

                foreach (var task in createAssetTasks)
                {
                    var assetUploadInfo = ((Task<AssetUploadInfo>)task).Result;

                    if (assetUploadInfo == null) // Something went wrong during asset creation and the error was already reported
                        continue;

                    var uploadEntry = assetUploadInfo.UploadAsset;

                    if (!assetUploadInfo.Skip)
                    {
                        // In the case we modified the asset after it was staged for upload
                        if(!uploadEntryToOperationLookup.TryGetValue(uploadEntry, out _))
                        {
                            uploadEntryToOperationLookup[uploadEntry] = StartNewOperation(uploadEntry);
                        }

                        uploadEntryToAssetUploadInfoLookup[uploadEntry] = assetUploadInfo;
                    }
                    // In the case we undo changes on an asset after it was staged for upload
                    else if(uploadEntryToOperationLookup.TryGetValue(uploadEntry, out var operation))
                    {
                        operation.Finish(OperationStatus.None);
                    }

                    guidToAssetLookup[uploadEntry.Guid] = assetUploadInfo.AssetData;
                }

                // Prepare a cloud asset for every asset entry that we want to upload
                await TaskUtils.RunWithMaxConcurrentTasksAsync(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return PrepareUploadAsync(operation, entry.Value.AssetData, guidToAssetLookup, token);
                    }, k_MaxConcurrentTasks);

                // Upload the assets
                await TaskUtils.RunWithMaxConcurrentTasksAsync(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return UploadAssetAsync(entry.Value, operation, token);
                    }, k_MaxConcurrentTasks);

                // Track upload asset as imported
                await TaskUtils.RunWithMaxConcurrentTasksAsync(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) => TrackAsset(entry.Key, entry.Value.AssetData, token)
                    , k_MaxConcurrentTasks);
            }
            catch (OperationCanceledException)
            {
                foreach (var (_, operation) in uploadEntryToOperationLookup)
                {
                    operation.Finish(OperationStatus.Cancelled);
                }
            }
            finally
            {
                m_Uploading = false;
                CancelUpload(); // Any failure should cancel whatever is left; if there's nothing left like on success, it's a no-op
                m_TokenSource?.Dispose();
                m_TokenSource = null;
                UploadEnded?.Invoke();
            }
        }

        public void CancelUpload()
        {
            m_TokenSource?.Cancel();
        }

        async Task TrackAsset(IUploadAsset uploadAsset, IAssetData asset, CancellationToken token)
        {
            try
            {
                IEnumerable<(string originalPath, string finalPath)> assetPaths = new List<(string, string)>();

                foreach (var f in uploadAsset.Files)
                {
                    assetPaths = assetPaths.Append((originalPath: f.DestinationPath, f.SourcePath));
                }

                IAssetData assetData;
                try
                {
                    var cloudAsset = await m_AssetsProvider.GetAssetAsync(asset.Identifier, token);
                    assetData = cloudAsset;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogError("Error while trying to track the asset from the cloud: " + e.Message);
                    assetData = asset;
                }

                // Make sure additional information like dependencies are populated
                await assetData.SyncWithCloudAsync(null, token);

                await m_ImportTracker.TrackAssets(assetPaths, assetData);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        async Task<AssetUploadInfo> CreateOrRecycleAsset(BaseOperation operation, IUploadAsset uploadAsset,
            UploadAssetMode uploadMode, ProjectIdentifier targetProject, string targetCollection,
            CancellationToken token)
        {
            try
            {
                return await CreateOrRecycleAsset(uploadAsset, uploadMode, targetProject,
                    targetCollection, token);
            }
            catch (OperationCanceledException)
            {
                // Do nothing if cancelled
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation?.Finish(OperationStatus.Error);
            }

            return null;
        }

        async Task<AssetUploadInfo> CreateOrRecycleAsset(IUploadAsset uploadAsset, UploadAssetMode uploadMode,
            ProjectIdentifier targetProject, string targetCollection, CancellationToken token)
        {
            AssetData existingAsset = null;

            if (uploadMode != UploadAssetMode.ForceNewAsset)
            {
                try
                {
                    existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(uploadAsset.Guid,
                        targetProject.OrganizationId, targetProject.ProjectId, token);

                    if (existingAsset != null)
                    {
                        var assetUploadInfo = await CheckExistingAsset(existingAsset, uploadAsset, uploadMode, token);
                        if (assetUploadInfo != null)
                        {
                            return assetUploadInfo;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // The user might not have access to that project
                    // We cannot recycle the asset in this case.
                }
            }

            AssetData asset;
            var canRecycleAsset = existingAsset != null && uploadMode == UploadAssetMode.SkipIdentical;
            if (canRecycleAsset)
            {
                asset = await RecycleAsset(uploadAsset, existingAsset, token);
            }
            else
            {
                var createdAssetData = await CreateNewAsset(uploadAsset, targetProject, targetCollection, token);
                asset = createdAssetData;
            }

            return new AssetUploadInfo(uploadAsset, asset, canRecycleAsset, false);
        }

        async Task<AssetUploadInfo> CheckExistingAsset(AssetData existingAsset, IUploadAsset uploadAsset, UploadAssetMode uploadMode, CancellationToken token)
        {
            switch (uploadMode)
            {
                case UploadAssetMode.SkipIdentical:
                {
                    var hasModifiedFiles = await Utilities.IsLocallyModifiedAsync(uploadAsset, existingAsset, m_AssetDataManager, token);
                    if (!hasModifiedFiles && !await Utilities.CheckDependenciesModifiedAsync(existingAsset, m_AssetDataManager, token))
                    {
                        Utilities.DevLog($"Asset is already as is on the cloud: {existingAsset.Name}. Skipping...");
                        return new AssetUploadInfo(uploadAsset, existingAsset, true, true);
                    }

                    break;
                }
            }

            return null;
        }

        UploadOperation StartNewOperation(IUploadAsset uploadAsset)
        {
            var operation = new UploadOperation(uploadAsset);
            m_AssetOperationManager.RegisterOperation(operation);

            operation.Start();

            return operation;
        }

        async Task PrepareUploadAsync(UploadOperation operation, AssetData asset,
            IDictionary<string, AssetData> guidToAssetLookup, CancellationToken token = default)
        {
            try
            {
                await operation.PrepareUploadAsync(asset, guidToAssetLookup, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.PreparationError, e.Message));
                throw;
            }
        }

        async Task UploadAssetAsync(AssetUploadInfo assetUploadInfo, UploadOperation operation, CancellationToken token = default)
        {
            try
            {
                await operation.UploadAsync(assetUploadInfo.AssetData, token);
                operation.Finish(OperationStatus.Success);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.Ok));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                // On upload failure, unlink any non recycled asset
                if (!assetUploadInfo.AssetRecycled)
                {
                    await m_AssetsProvider.RemoveAsset(assetUploadInfo.AssetData.Identifier, token);
                }

                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.UploadError, e.Message));
                throw;
            }
        }

        void AddDependencies(IUploadAsset uploadAsset, ICollection<IUploadAsset> assetEntries,
            IReadOnlyDictionary<string, IUploadAsset> database)
        {
            if (assetEntries.Contains(uploadAsset))
                return;

            assetEntries.Add(uploadAsset);
            foreach (var id in uploadAsset.Dependencies)
            {
                if (database.TryGetValue(id, out var child))
                {
                    AddDependencies(child, assetEntries, database);
                }
            }
        }

        async Task<AssetData> CreateNewAsset(IUploadAsset uploadAsset, ProjectIdentifier targetProject,
            string targetCollection, CancellationToken token)
        {
            var assetCreation = new AssetCreation
            {
                Name = uploadAsset.Name,
                Collections = string.IsNullOrEmpty(targetCollection) ? null : new List<string> { new ( targetCollection ) },
                Type = uploadAsset.AssetType,
                Tags = uploadAsset.Tags.ToList()
            };

            return await m_AssetsProvider.CreateAssetAsync(targetProject, assetCreation, token);
        }

        async Task<AssetData> RecycleAsset(IUploadAsset uploadAsset, AssetData asset, CancellationToken token)
        {
            if (asset.IsFrozen)
            {
                asset = await m_AssetsProvider.CreateUnfrozenVersionAsync(asset, token);
            }

            var assetUpdate = new AssetUpdate
            {
                Name = uploadAsset.Name,
                Type = uploadAsset.AssetType,
                Tags = uploadAsset.Tags.ToList()
            };

            var tasks = new List<Task>
            {
                m_AssetsProvider.UpdateAsync(asset, assetUpdate, token),
                m_AssetsProvider.RemoveAllFiles(asset, token),
                m_AssetsProvider.RemoveThumbnail(asset, token)
            };

            await Task.WhenAll(tasks);

            return asset;
        }
    }
}
