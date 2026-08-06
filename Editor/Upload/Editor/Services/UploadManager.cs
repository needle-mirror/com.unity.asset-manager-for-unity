using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Editor;
using UnityEngine;
using AssetUpdate = Unity.AssetManager.Core.Editor.AssetUpdate;

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

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        CancellationTokenSource m_TokenSource;

        bool m_Uploading;

        public bool IsUploading => m_Uploading;
        public event Action UploadBegan;
        public event Action<UploadEndedStatus> UploadEnded;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager, IImportedAssetsTracker importTracker, IAssetsProvider assetsProvider, IAssetDataManager assetDataManager)
        {
            m_AssetOperationManager = assetOperationManager;
            m_ImportTracker = importTracker;
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
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

                Utilities.DevLog($"UploadAsync: received {uploadEntries.Count} entries; database keys: " +
                    $"[{string.Join(", ", database.Keys.Select(k => k.AssetId))}]", tag: "Upload");

                // Get all assets, including their dependencies
                foreach (var uploadEntry in uploadEntries)
                {
                    AddDependencies(uploadEntry, assetEntriesWithAllDependencies, database);
                }

                Utilities.DevLog($"UploadAsync: assetEntriesWithAllDependencies has {assetEntriesWithAllDependencies.Count} entries after AddDependencies traversal", tag: "Upload");
                foreach (var e in assetEntriesWithAllDependencies)
                {
                    Utilities.DevLog($"  '{e.Name}' id={e.LocalIdentifier.AssetId} declared deps: " +
                        $"[{string.Join(", ", e.Dependencies.Select(d => $"{d.AssetId}@{d.Version}{(d.IsLocal() ? "(local)" : "(cloud)")}"))}]", tag: "Upload");
                }

                token.ThrowIfCancellationRequested();

                // Ensure collection hierarchies exist before creating assets
                await EnsureCollectionHierarchiesExistAsync(assetEntriesWithAllDependencies, token);
                token.ThrowIfCancellationRequested();

                // Prepare the IAssets
                var createAssetTasks = await TaskUtils.RunAllTasksInQueue(assetEntriesWithAllDependencies,
                    (uploadEntry) =>
                    {
                        var operation = StartNewOperation(uploadEntry);
                        uploadEntryToOperationLookup[uploadEntry] = operation;

                        // Intentionally not cancellable: we must run creations to completion so that
                        // uploadEntryToAssetUploadInfoLookup is fully populated. Otherwise
                        // RevertCreationsAsync would receive an empty lookup on cancel and could not remove
                        // the assets already created, leaving them orphaned on the server.
                        return CreateOrRecycleAsset(operation, uploadEntry, CancellationToken.None);
                    }, cancellationToken:token);

                var anyAssetCreationFailed = false;
                foreach (var task in createAssetTasks)
                {
                    var assetUploadInfo = await (Task<AssetUploadInfo>)task;

                    if (assetUploadInfo == null)
                    {
                        // we don't throw an exception here since we need to know the successfull uploads to remove them
                        anyAssetCreationFailed = true;
                        continue;
                    }

                    var uploadEntry = assetUploadInfo.UploadAsset;
                    uploadEntryToAssetUploadInfoLookup[uploadEntry] = assetUploadInfo;
                    identifierToAssetLookup[uploadEntry.LocalIdentifier] = assetUploadInfo.TargetAssetData;
                }

                if (anyAssetCreationFailed)
                {
                    throw new AssetManagerException(
                        "One or more creation(s) failed. Upload process will be cancelled and created assets will be removed.");
                }

                Utilities.DevLog($"UploadAsync: identifierToAssetLookup populated with {identifierToAssetLookup.Count} entries: " +
                    $"[{string.Join(", ", identifierToAssetLookup.Keys.Select(k => k.AssetId))}]", tag: "Upload");

                token.ThrowIfCancellationRequested();

                // Prepare a cloud asset for every asset entry that we want to upload
                await TaskUtils.RunAllTasksBatched(uploadEntryToAssetUploadInfoLookup,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return FetchAssetDependenciesAsync(operation, entry.Value.TargetAssetData,
                            identifierToAssetLookup,
                            token);
                    });

                token.ThrowIfCancellationRequested();

                // Upload the assets
                var uploadTasks = await TaskUtils.RunAllTasksInQueue(uploadEntryToAssetUploadInfoLookup,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return UploadAssetAsync(entry.Value, operation, token);
                    },
                    cancellationToken: token);

                // We check first for cancellation to give it priority over faults
                token.ThrowIfCancellationRequested();

                if (uploadTasks.Any(t => t.IsFaulted))
                {
                    throw new AssetManagerException(
                        "One or more upload(s) failed. Upload process will be cancelled and created assets will be removed.");
                }

                // Update the dependencies after the upload
                var updateTasks = await TaskUtils.RunAllTasksInQueue(uploadEntryToAssetUploadInfoLookup,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return UpdateDependenciesAsync(operation, entry.Value.TargetAssetData, token);
                    },
                    cancellationToken: token);

                // We check first for cancellation to give it priority over faults
                token.ThrowIfCancellationRequested();

                if (updateTasks.Any(t => t.IsFaulted))
                {
                    throw new AssetManagerException(
                        "One or more dependencies update failed. Upload process will be cancelled and created assets will be removed.");
                }

                // Link assets to collections based on project structure
                await LinkAssetsToCollectionsAsync(uploadEntryToAssetUploadInfoLookup, token);
                token.ThrowIfCancellationRequested();

                // Track the assets
                await TaskUtils.RunAllTasksInQueue(uploadEntryToAssetUploadInfoLookup,
                    (entry) =>
                    {
                        var operation = uploadEntryToOperationLookup[entry.Key];
                        return TrackAsset(entry.Value.UploadAsset, entry.Value.TargetAssetData, operation, token);
                    },
                    cancellationToken: token);

                token.ThrowIfCancellationRequested();
            }
            catch (AssetManagerException e)
            {
                uploadEndedStatus = UploadEndedStatus.Error;
                Debug.LogError(e.Message);
                await RevertCreationsAsync(uploadEntryToAssetUploadInfoLookup, uploadEntryToOperationLookup);
            }
            catch (OperationCanceledException e)
            {
                uploadEndedStatus = UploadEndedStatus.Cancelled;

                await RevertCreationsAsync(uploadEntryToAssetUploadInfoLookup, uploadEntryToOperationLookup);

                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.Cancelled, e.Message));
            }
            catch (Exception)
            {
                uploadEndedStatus = UploadEndedStatus.Error;
                await RevertCreationsAsync(uploadEntryToAssetUploadInfoLookup, uploadEntryToOperationLookup);
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

        async Task RevertCreationsAsync(Dictionary<IUploadAsset, AssetUploadInfo> uploadEntryToAssetUploadInfoLookup,
            Dictionary<IUploadAsset, UploadOperation> uploadEntryToOperationLookup)
        {
            await TaskUtils.RunAllTasks(uploadEntryToAssetUploadInfoLookup.Values, RemoveAttemptedUploadAssetAsync);

            foreach (var (_, operation) in uploadEntryToOperationLookup)
            {
                if (operation.Status is not OperationStatus.Error)
                {
                    operation.Finish(OperationStatus.Cancelled);
                }
            }
        }

        public void CancelUpload()
        {
            m_TokenSource?.Cancel();
        }

        async Task TrackAsset(IUploadAsset uploadAsset, BaseAssetData asset, UploadOperation operation, CancellationToken token)
        {
            try
            {
                operation.Report("Tracking asset", -1);

                IEnumerable<(string originalPath, string finalPath, string checksum)> assetPaths = new List<(string, string, string)>();

                foreach (var f in uploadAsset.Files)
                {
                    assetPaths = assetPaths.Append((originalPath: f.DestinationPath, f.SourcePath, null));
                }

                AssetData cloudAsset = null;
                var assetData = asset;
                try
                {
                    // Force fresh fetch from cloud (TimeSpan.Zero) to ensure we get the newly uploaded version.
                    // The cache uses TrackedAssetIdentifier which matches by organization and AssetId
                    // only (ignoring version and project), so without forcing a fresh fetch, we might
                    // get stale data from a previous version.
                    cloudAsset = await m_AssetDataManager.GetAssetAsync(asset.Identifier, TimeSpan.Zero, token);
                    assetData = cloudAsset;

                    // Make sure additional data is populated.
                    var tasks = new List<Task>
                    {
                        assetData.ResolveDatasetsAsync(token),
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
                    Debug.LogError($"Error while trying get track data for cloud asset {asset.Name}\n{e.Message}");
                }

                await m_ImportTracker.TrackAssets(assetPaths, assetData);

                operation.Finish(OperationStatus.Success);
            }
            catch (OperationCanceledException)
            {
                operation.Finish(OperationStatus.Cancelled);
                throw;
            }
            catch (Exception e)
            {
                operation.Finish(OperationStatus.Error);
                Debug.LogException(e);
                throw;
            }

            Debug.Log($"Done tracking asset {asset.Name}");
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
                originalAsset = await m_AssetDataManager.GetAssetAsync(uploadAsset.ExistingAssetIdentifier, TimeSpan.MaxValue, token);
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

        async Task FetchAssetDependenciesAsync(UploadOperation operation, AssetData targetAssetData,
            IDictionary<AssetIdentifier, AssetData> identifierToAssetLookup, CancellationToken token = default)
        {
            try
            {
                await operation.FetchAssetDependenciesAsync(targetAssetData, identifierToAssetLookup, token);
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

        async Task UpdateDependenciesAsync(UploadOperation operation, AssetData targetAssetData, CancellationToken token = default)
        {
            try
            {
                await operation.UpdateDependenciesAsync(targetAssetData, token);
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

        async Task EnsureCollectionHierarchiesExistAsync(
            IEnumerable<IUploadAsset> uploadAssets,
            CancellationToken token)
        {
            var assetsWithCollections = uploadAssets
                .Where(asset => !string.IsNullOrEmpty(asset.TargetCollection))
                .ToList();

            if (assetsWithCollections.Count == 0)
                return;

            // Group assets by project to handle multi-project uploads correctly
            var assetsByProject = assetsWithCollections.GroupBy(asset => asset.TargetProject);

            foreach (var projectGroup in assetsByProject)
            {
                var projectIdentifier = projectGroup.Key;
                var uniqueCollectionPaths = projectGroup
                    .Select(asset => asset.TargetCollection)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .ToList();

                if (uniqueCollectionPaths.Count == 0)
                    continue;

                await EnsureCollectionHierarchiesExistForProjectAsync(projectIdentifier, uniqueCollectionPaths, token);
            }
        }

        async Task EnsureCollectionHierarchiesExistForProjectAsync(
            ProjectIdentifier projectIdentifier,
            List<string> collectionPaths,
            CancellationToken token)
        {
            // Get all segments that need to exist for each path
            // e.g., "Assets/Materials/Metal" → ["Assets", "Assets/Materials", "Assets/Materials/Metal"]
            var allRequiredPaths = new HashSet<string>();
            foreach (var path in collectionPaths)
            {
                var segments = path.Split('/');
                var currentPath = string.Empty;
                foreach (var segment in segments)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
                    allRequiredPaths.Add(currentPath);
                }
            }

            // Fetch existing collections
            HashSet<string> existingPaths;
            try
            {
                existingPaths = await m_AssetsProvider.GetExistingCollectionPathsAsync(projectIdentifier, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to fetch existing collections: {e.Message}");
                return;
            }

            // Find missing collections
            var missingPaths = allRequiredPaths.Except(existingPaths).ToList();
            if (missingPaths.Count == 0)
                return;

            // Sort by depth (number of slashes) to create parents first
            missingPaths.Sort((a, b) => a.Count(c => c == '/').CompareTo(b.Count(c => c == '/')));

            // Create missing collections in order
            foreach (var path in missingPaths)
            {
                token.ThrowIfCancellationRequested();

                var slashIndex = path.LastIndexOf('/');
                string name;
                string parentPath;

                if (slashIndex > 0)
                {
                    parentPath = path.Substring(0, slashIndex);
                    name = path.Substring(slashIndex + 1);
                }
                else
                {
                    parentPath = string.Empty;
                    name = path;
                }

                try
                {
                    await m_AssetsProvider.CreateCollectionHierarchyAsync(projectIdentifier, name, parentPath, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to create collection '{path}': {e.Message}");
                }
            }
        }

        async Task LinkAssetsToCollectionsAsync(
            Dictionary<IUploadAsset, AssetUploadInfo> uploadEntryToAssetUploadInfoLookup,
            CancellationToken token)
        {
            // Link recycled assets to their collections
            // (new assets are linked during creation via AssetCreation.Collections)
            var recycledAssets = uploadEntryToAssetUploadInfoLookup
                .Where(kvp => !string.IsNullOrEmpty(kvp.Key.TargetCollection) && kvp.Value.TargetAssetDataWasRecycled)
                .ToList();

            if (recycledAssets.Count == 0)
                return;

            // Group by both project and collection path to handle multi-project uploads correctly
            var assetsByProjectAndCollection = recycledAssets
                .GroupBy(kvp => (kvp.Key.TargetProject, kvp.Key.TargetCollection));

            foreach (var group in assetsByProjectAndCollection)
            {
                var projectIdentifier = group.Key.TargetProject;
                var collectionPath = group.Key.TargetCollection;
                var assetIdentifiers = group.Select(kvp => kvp.Value.TargetAssetData.Identifier).ToList();

                try
                {
                    await m_AssetsProvider.LinkAssetsToCollectionAsync(
                        projectIdentifier,
                        collectionPath,
                        assetIdentifiers,
                        token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to link {assetIdentifiers.Count} asset(s) to collection '{collectionPath}': {e.Message}");
                }
            }
        }

        async Task UploadAssetAsync(AssetUploadInfo assetUploadInfo, UploadOperation operation, CancellationToken token = default)
        {
            try
            {
                await operation.UploadAsync(assetUploadInfo.TargetAssetData, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.UploadError, e.Message));
                throw;
            }
        }

        async Task RemoveAttemptedUploadAssetAsync(AssetUploadInfo uploadInfo)
        {
            if (uploadInfo.TargetAssetDataWasRecycled)
            {
                await m_AssetsProvider.RemoveUnfrozenAssetVersion(uploadInfo.TargetAssetData.Identifier,
                    CancellationToken.None);
            }
            else
            {
                await m_AssetsProvider.RemoveAsset(uploadInfo.TargetAssetData.Identifier, CancellationToken.None);
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
                else if (id.IsLocal())
                {
                    Utilities.DevLogWarning($"AddDependencies: '{uploadAsset.Name}' references local dep {id} that is NOT in upload database. " +
                        "Asset will not be created and the manifest entry will be skipped during FetchAssetDependenciesAsync.", tag: "Upload");
                }
            }
        }

        async Task<AssetData> CreateNewAsset(IUploadAsset uploadAsset, CancellationToken token)
        {
            var targetCollection = uploadAsset.TargetCollection;
            var assetCreation = new AssetCreation
            {
                Name = uploadAsset.Name,
                Description = uploadAsset.Description,
                Collections = string.IsNullOrEmpty(targetCollection) ? null : new List<string> { new(targetCollection) },
                Type = uploadAsset.AssetType,
                Tags = uploadAsset.Tags.ToList(),
                Metadata = uploadAsset.Metadata.ToList(),
                StatusFlowIdentifier = string.IsNullOrWhiteSpace(uploadAsset.StatusFlowId)
                    ? null
                    : new StatusFlowIdentifier(uploadAsset.StatusFlowId, uploadAsset.TargetProject.OrganizationId),
            };

            return await m_AssetsProvider.CreateAssetAsync(uploadAsset.TargetProject, assetCreation, token);
        }

        async Task<AssetData> RecycleAsset(IUploadAsset uploadAsset, AssetData asset, CancellationToken token)
        {
            const ComparisonResults filesChanged = ComparisonResults.FilesAdded | ComparisonResults.FilesRemoved | ComparisonResults.FilesModified;
            const ComparisonResults metadataChanged = ComparisonResults.MetadataAdded | ComparisonResults.MetadataRemoved | ComparisonResults.MetadataModified;

            if (asset.IsFrozen)
            {
                var unfrozenAsset = await m_AssetsProvider.CreateUnfrozenVersionAsync(asset, token);
                if (unfrozenAsset == null)
                {
                    throw new InvalidOperationException($"Failed to create unfrozen version for asset '{asset.Name}' ({asset.Identifier}). The asset may have been deleted or you may not have permission to modify it.");
                }
                asset = unfrozenAsset;
            }

            var assetUpdate = new AssetUpdate
            {
                Name = uploadAsset.Name,
                Description = uploadAsset.Description,
                Type = uploadAsset.AssetType,
                Tags = uploadAsset.Tags.ToList(),
                StatusFlowIdentifier = string.IsNullOrWhiteSpace(uploadAsset.StatusFlowId)
                    ? null
                    : new StatusFlowIdentifier(uploadAsset.StatusFlowId, uploadAsset.TargetProject.OrganizationId),
            };

            // Only update metadata if it was considered modified in some way.
            if ((uploadAsset.ComparisonResults & metadataChanged) != 0)
            {
                assetUpdate.Metadata = uploadAsset.Metadata.ToList();
            }

            var tasks = new List<Task>
            {
                // Use UpdateWithoutRefreshAsync to avoid cache conflicts when updating a newly created unfrozen version.
                // The regular UpdateAsync would refresh properties from cache, which could overwrite our new version's
                // identifier with the old frozen version's identifier (TrackedAssetIdentifier matches
                // by organization and AssetId only, ignoring version and project).
                m_AssetsProvider.UpdateWithoutRefreshAsync(asset, assetUpdate, token),
                m_AssetsProvider.RemoveThumbnail(asset, token),
            };

            // Only remove files if they were considered modified in some way.
            if ((uploadAsset.ComparisonResults & filesChanged) != 0)
            {
                tasks.Add(m_AssetsProvider.RemoveAllFiles(asset, token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // If any of the tasks fail, we need to remove the asset version that was created before throwing the exception.
                await m_AssetsProvider.RemoveUnfrozenAssetVersion(asset.Identifier, token);
                throw;
            }

            return asset;
        }
    }
}
