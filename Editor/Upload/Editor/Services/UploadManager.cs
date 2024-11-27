using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    enum UploadEndedStatus
    {
        Success,
        Error,
        Cancelled
    }

    interface IUploadManager : IService
    {
        event Action UploadBegan;
        event Action<UploadEndedStatus> UploadEnded;

        bool IsUploading { get; }

        void CancelUpload();
        Task UploadAsync(IReadOnlyCollection<IUploadAsset> uploadEntries);
    }

    [Serializable]
    class UploadManager : BaseService<IUploadManager>, IUploadManager
    {
        class AssetUploadInfo
        {
            public AssetUploadInfo(IUploadAsset uploadAsset, AssetData targetAssetData, bool targetAssetDataWasRecycled)
            {
                UploadAsset = uploadAsset;
                TargetAssetData = targetAssetData;
                TargetAssetDataWasRecycled = targetAssetDataWasRecycled;
            }

            public IUploadAsset UploadAsset { get; }
            public AssetData TargetAssetData { get; }
            public bool TargetAssetDataWasRecycled { get; }
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
        public event Action<UploadEndedStatus> UploadEnded;

        // Increasing this number can help speed up the whole process but mainly leads to timeouts and errors.
        // We prefer to upload steadily and safely rather than quickly and with potential issues.
        // Note that this number only controls how many assets are created/uploaded at the same time, and doesn't control how much files are uploaded simultaneously.
        static readonly int k_MaxConcurrentTasks = 5;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager, IImportedAssetsTracker importTracker, IAssetsProvider assetsProvider, IAssetDataManager assetDataManager)
        {
            m_AssetOperationManager = assetOperationManager;
            m_ImportTracker = importTracker;
            m_AssetsProvider = assetsProvider;
        }

        public async Task UploadAsync(IReadOnlyCollection<IUploadAsset> uploadEntries)
        {
            if (m_Uploading)
                return;

            m_Uploading = true;
            UploadBegan?.Invoke();

            var uploadEntryToAssetUploadInfoLookup = new Dictionary<IUploadAsset, AssetUploadInfo>();
            var uploadEntryToOperationLookup = new Dictionary<IUploadAsset, UploadOperation>();
            var identifierToAssetLookup = new Dictionary<AssetIdentifier, AssetData>();

            m_TokenSource = new CancellationTokenSource();
            var token = m_TokenSource.Token;
            var uploadEndedStatus = UploadEndedStatus.Success;

            try
            {
                var assetEntriesWithAllDependencies = new List<IUploadAsset>();

                var database = uploadEntries.ToDictionary(entry => entry.LocalIdentifier);

                // Get all assets, including their dependencies
                foreach (var uploadEntry in uploadEntries)
                {
                    AddDependencies(uploadEntry, assetEntriesWithAllDependencies, database);
                }

                // Prepare the IAssets
                var createAssetTasks = await TaskUtils.RunWithMaxConcurrentTasksAsync(assetEntriesWithAllDependencies, token,
                    (uploadEntry) =>
                    {
                        var operation = StartNewOperation(uploadEntry);
                        uploadEntryToOperationLookup[uploadEntry] = operation;

                        return CreateOrRecycleAsset(operation, uploadEntry, token);
                    }, k_MaxConcurrentTasks);

                foreach (var task in createAssetTasks)
                {
                    var assetUploadInfo = ((Task<AssetUploadInfo>)task).Result;

                    if (assetUploadInfo == null) // Something went wrong during asset creation and the error was already reported
                        continue;

                    var uploadEntry = assetUploadInfo.UploadAsset;

                    uploadEntryToAssetUploadInfoLookup[uploadEntry] = assetUploadInfo;
                    identifierToAssetLookup[uploadEntry.LocalIdentifier] = assetUploadInfo.TargetAssetData;
                }

                // Prepare a cloud asset for every asset entry that we want to upload
                await TaskUtils.RunWithMaxConcurrentTasksAsync(uploadEntryToAssetUploadInfoLookup, token,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return PrepareUploadAsync(operation, entry.Value.TargetAssetData, identifierToAssetLookup, token);
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
                    (entry) => TrackAsset(entry.Key, entry.Value.TargetAssetData, token)
                    , k_MaxConcurrentTasks);
            }
            catch (OperationCanceledException)
            {
                uploadEndedStatus = UploadEndedStatus.Cancelled;
                foreach (var (_, operation) in uploadEntryToOperationLookup)
                {
                    operation.Finish(OperationStatus.Cancelled);
                }
            }
            catch (Exception)
            {
                uploadEndedStatus = UploadEndedStatus.Error;
            }
            finally
            {
                m_Uploading = false;
                CancelUpload(); // Any failure should cancel whatever is left; if there's nothing left like on success, it's a no-op
                m_TokenSource?.Dispose();
                m_TokenSource = null;
                UploadEnded?.Invoke(uploadEndedStatus);
            }
        }

        public void CancelUpload()
        {
            m_TokenSource?.Cancel();
        }

        async Task TrackAsset(IUploadAsset uploadAsset, BaseAssetData asset, CancellationToken token)
        {
            try
            {
                IEnumerable<(string originalPath, string finalPath)> assetPaths = new List<(string, string)>();

                foreach (var f in uploadAsset.Files)
                {
                    assetPaths = assetPaths.Append((originalPath: f.DestinationPath, f.SourcePath));
                }

                BaseAssetData assetData;
                try
                {
                    var cloudAsset = await m_AssetsProvider.GetAssetAsync(asset.Identifier, token);
                    assetData = cloudAsset;

                    // Make sure additional data is populated.
                    var tasks = new List<Task>
                    {
                        assetData.ResolvePrimaryExtensionAsync(null, token),
                        assetData.RefreshDependenciesAsync(token),
                    };
                    await Task.WhenAll(tasks);
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
            CancellationToken token)
        {
            try
            {
                return await CreateOrRecycleAsset(uploadAsset, token);
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

        async Task<AssetUploadInfo> CreateOrRecycleAsset(IUploadAsset uploadAsset, CancellationToken token)
        {
            AssetData originalAsset = null;

            if (uploadAsset.ExistingAssetIdentifier != null)
            {
                originalAsset = await m_AssetsProvider.GetAssetAsync(uploadAsset.ExistingAssetIdentifier, token);
            }

            var isRecycled = originalAsset != null;

            var targetAssetData = isRecycled
                ? await RecycleAsset(uploadAsset, originalAsset, token)
                : await CreateNewAsset(uploadAsset, token);

            return new AssetUploadInfo(uploadAsset, targetAssetData, isRecycled);
        }

        UploadOperation StartNewOperation(IUploadAsset uploadAsset)
        {
            var operation = new UploadOperation(uploadAsset);
            m_AssetOperationManager.RegisterOperation(operation);

            operation.Start();

            return operation;
        }

        async Task PrepareUploadAsync(UploadOperation operation, AssetData targetAssetData,
            IDictionary<AssetIdentifier, AssetData> identifierToAssetLookup, CancellationToken token = default)
        {
            try
            {
                await operation.PrepareUploadAsync(targetAssetData, identifierToAssetLookup, token);
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
                await operation.UploadAsync(assetUploadInfo.TargetAssetData, token);
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
                if (!assetUploadInfo.TargetAssetDataWasRecycled)
                {
                    await m_AssetsProvider.RemoveAsset(assetUploadInfo.TargetAssetData.Identifier, token);
                }

                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.UploadError, e.Message));
                throw;
            }
        }

        void AddDependencies(IUploadAsset uploadAsset, ICollection<IUploadAsset> assetEntries,
            IReadOnlyDictionary<AssetIdentifier, IUploadAsset> database)
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

        async Task<AssetData> CreateNewAsset(IUploadAsset uploadAsset, CancellationToken token)
        {
            var targetCollection = uploadAsset.TargetCollection;
            var assetCreation = new AssetCreation
            {
                Name = uploadAsset.Name,
                Collections = string.IsNullOrEmpty(targetCollection) ? null : new List<string> { new(targetCollection) },
                Type = uploadAsset.AssetType,
                Tags = uploadAsset.Tags.ToList()
            };

            return await m_AssetsProvider.CreateAssetAsync(uploadAsset.TargetProject, assetCreation, token);
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
