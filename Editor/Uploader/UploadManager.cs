using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
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
        SkipExisting, // Skips the upload if an asset with the same ID already exists on the cloud
        Override, // Replaces and overrides any existing asset with the same ID on the cloud
        Duplicate, // Uploads new assets and potentially duplicates without checking for existing matches
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
        public void Inject(IAssetOperationManager assetOperationManager, IImportedAssetsTracker importTracker, IAssetsProvider assetsProvider)
        {
            m_AssetOperationManager = assetOperationManager;
            m_ImportTracker = importTracker;
            m_AssetsProvider = assetsProvider;
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
                var createAssetTasks = await TaskUtils.RunWithMaxConcurrentTasks(assetEntriesWithAllDependencies, token,
                    (uploadEntry) =>
                    {
                        var operation = StartNewOperation(uploadEntry);
                        uploadEntryToOperationLookup[uploadEntry] = operation;

                        return CreateOrRecycleAsset(operation, uploadEntry, settings.UploadMode, targetProject,
                            targetCollection, token);
                    }, k_MaxConcurrentTasks);

                foreach (var task in createAssetTasks)
                {
                    var assetUploadInfo = ((Task<AssetUploadInfo>)task).Result;

                    if (assetUploadInfo == null) // Something went wrong during asset creation and the error was already reported
                        continue;

                    var uploadEntry = assetUploadInfo.UploadAsset;

                    if (!assetUploadInfo.Skip)
                    {
                        uploadEntryToAssetUploadInfoLookup[uploadEntry] = assetUploadInfo;
                    }
                    else
                    {
                        var operation = uploadEntryToOperationLookup[uploadEntry];
                        operation.Finish(OperationStatus.Success);
                    }

                    guidToAssetLookup[uploadEntry.Guid] = assetUploadInfo.AssetData;
                }

                // Prepare a cloud asset for every asset entry that we want to upload
                await TaskUtils.RunWithMaxConcurrentTasks(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return PrepareUploadAsync(operation, entry.Value.AssetData, guidToAssetLookup, token);
                    }, k_MaxConcurrentTasks);

                // Upload the assets
                await TaskUtils.RunWithMaxConcurrentTasks(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return UploadAssetAsync(entry.Value, operation, token);
                    }, k_MaxConcurrentTasks);

                // Track upload asset as imported
                await TaskUtils.RunWithMaxConcurrentTasks(uploadEntryToAssetUploadInfoLookup, token,
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

                foreach (var sourcePath in uploadAsset.Files.Select(f => f.SourcePath))
                {
                    var originalFile = Utilities.GetPathRelativeToAssetsFolder(sourcePath);
                    assetPaths = assetPaths.Append((originalPath: originalFile, sourcePath));
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

                m_ImportTracker.TrackAssets(assetPaths, assetData);
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
                operation.Finish(OperationStatus.Error);
            }

            return null;
        }

        async Task<AssetUploadInfo> CreateOrRecycleAsset(IUploadAsset uploadAsset, UploadAssetMode uploadMode,
            ProjectIdentifier targetProject, string targetCollection, CancellationToken token)
        {
            AssetData existingAsset = null;

            if (uploadMode != UploadAssetMode.Duplicate)
            {
                try
                {
                    existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(uploadAsset.Guid,
                        targetProject.OrganizationId.ToString(), targetProject.ProjectId.ToString(), token);

                    if (existingAsset != null && uploadMode == UploadAssetMode.SkipExisting)
                    {
                        Utilities.DevLog($"Asset is already on the cloud: {existingAsset.Name}. Skipping...");
                        return new AssetUploadInfo(uploadAsset, existingAsset, true, true);
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
            var canRecycleAsset = existingAsset != null && uploadMode == UploadAssetMode.Override;
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
                if (e is not ServiceException)
                {
                    Debug.LogException(e);
                }

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
