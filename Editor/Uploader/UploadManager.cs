using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
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
        Task UploadAsync(IReadOnlyCollection<IUploadAssetEntry> uploadEntries, UploadSettings settings);
    }

    enum AssetUploadMode
    {
        Ignore, // Skips the upload if an asset with the same ID already exists on the cloud
        Override, // Replaces and overrides any existing asset with the same ID on the cloud
        Duplicate, // Uploads new assets and potentially duplicates without checking for existing matches
        None, // Not assigned
    }

    [Serializable]
    class UploadManager : BaseService<IUploadManager>, IUploadManager
    {
        class AssetUploadInfo
        {
            public AssetUploadInfo(IUploadAssetEntry uploadEntry, IAsset asset, bool skip)
            {
                UploadEntry = uploadEntry;
                Asset = asset;
                Skip = skip;
            }

            public IUploadAssetEntry UploadEntry { get; }
            public IAsset Asset { get; }
            public bool Skip { get; }
        }

        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [SerializeReference]
        IImportedAssetsTracker m_ImportTracker;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        const int k_MaxConcurrentUploadTasks = 10;

        CancellationTokenSource m_TokenSource;

        bool m_Uploading;

        public bool IsUploading => m_Uploading;
        public event Action UploadBegan;
        public event Action UploadEnded;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager, IImportedAssetsTracker importTracker, IAssetsProvider assetsProvider)
        {
            m_AssetOperationManager = assetOperationManager;
            m_ImportTracker = importTracker;
            m_AssetsProvider = assetsProvider;
        }

        public async Task UploadAsync(IReadOnlyCollection<IUploadAssetEntry> uploadEntries, UploadSettings settings)
        {
            if (m_Uploading)
                return;

            m_Uploading = true;
            UploadBegan?.Invoke();

            var uploadEntryToAssetLookup = new Dictionary<IUploadAssetEntry, IAsset>();
            var uploadEntryToOperationLookup = new Dictionary<IUploadAssetEntry, UploadOperation>();
            var guidToAssetLookup = new Dictionary<string, IAsset>();

            m_TokenSource = new CancellationTokenSource();
            var token = m_TokenSource.Token;

            try
            {
                var assetEntriesWithAllDependencies = new List<IUploadAssetEntry>();

                var targetProject = new ProjectDescriptor(new OrganizationId(settings.OrganizationId),
                    new ProjectId(settings.ProjectId));
                var targetCollection = settings.CollectionPath;

                var database = uploadEntries.ToDictionary(entry => entry.Guid);

                // Get all assets, including their dependencies
                foreach (var uploadEntry in uploadEntries)
                {
                    AddDependencies(uploadEntry, assetEntriesWithAllDependencies, database);
                }

                // Prepare the IAssets
                var createAssetTasks = new List<Task<AssetUploadInfo>>();

                foreach (var uploadEntry in assetEntriesWithAllDependencies)
                {
                    var operation = StartNewOperation(uploadEntry);
                    uploadEntryToOperationLookup[uploadEntry] = operation;

                    var task = CreateOrRecycleAsset(operation, uploadEntry, settings.AssetUploadMode, targetProject,
                        targetCollection, token);
                    createAssetTasks.Add(task);
                }

                await Task.WhenAll(createAssetTasks);

                foreach (var task in createAssetTasks)
                {
                    var assetUploadInfo = task.Result;

                    if (assetUploadInfo == null) // Something went wrong during asset creation and the error was already reported
                        continue;

                    var uploadEntry = assetUploadInfo.UploadEntry;

                    if (!assetUploadInfo.Skip)
                    {
                        uploadEntryToAssetLookup[uploadEntry] = assetUploadInfo.Asset;
                    }
                    else
                    {
                        var operation = uploadEntryToOperationLookup[uploadEntry];
                        operation.Finish(OperationStatus.Success);
                    }

                    guidToAssetLookup[uploadEntry.Guid] = assetUploadInfo.Asset;
                }

                // Generate dependency files
                var prepareTasks = new List<Task>();

                foreach (var (uploadEntry, asset) in uploadEntryToAssetLookup)
                {
                    var operation = uploadEntryToOperationLookup[uploadEntry];
                    prepareTasks.Add(PrepareUploadAsync(operation, asset, guidToAssetLookup, token));
                }

                await Task.WhenAll(prepareTasks);

                // Upload the assets
                var uploadTasks = new List<Task>();

                foreach (var (uploadEntry, asset) in uploadEntryToAssetLookup)
                {
                    var operation = uploadEntryToOperationLookup[uploadEntry];
                    uploadTasks.Add(UploadAssetAsync(operation, asset, token));
                }

                await Task.WhenAll(uploadTasks);

                var trackAssetsTasks = new List<Task>();

                foreach (var (uploadEntry, asset) in uploadEntryToAssetLookup)
                {
                    trackAssetsTasks.Add(TrackAsset(token, uploadEntry, asset));
                }

                await Task.WhenAll(trackAssetsTasks);
            }
            catch (OperationCanceledException)
            {
                // TODO: Delete already uploaded assets
                foreach (var (uploadEntry, operation) in uploadEntryToOperationLookup)
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

        async Task TrackAsset(CancellationToken token, IUploadAssetEntry uploadEntry, IAsset asset)
        {
            try
            {
                IEnumerable<(string originalPath, string finalPath)> assetPaths = new List<(string, string)>();

                foreach (var file in uploadEntry.Files)
                {
                    var originalFile = Utilities.GetPathRelativeToAssetsFolder(file);
                    assetPaths = assetPaths.Append((originalPath: originalFile, file));
                }

                IAssetData assetData;
                try
                {
                    var cloudAsset = await m_AssetsProvider.GetAssetAsync(asset.Descriptor, token);
                    assetData = new AssetData(cloudAsset);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogError("Error while trying to track the asset from the cloud: " + e.Message);
                    assetData = new AssetData(asset);
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

        async Task<AssetUploadInfo> CreateOrRecycleAsset(BaseOperation operation, IUploadAssetEntry uploadEntry,
            AssetUploadMode uploadMode, ProjectDescriptor targetProject, CollectionPath targetCollection,
            CancellationToken token)
        {

            try
            {
                return await CreateOrRecycleAsset(uploadEntry, uploadMode, targetProject,
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

        async Task<AssetUploadInfo> CreateOrRecycleAsset(IUploadAssetEntry uploadEntry, AssetUploadMode uploadMode,
            ProjectDescriptor targetProject, CollectionPath targetCollection, CancellationToken token)
        {
            IAsset existingAsset = null;

            if (uploadMode != AssetUploadMode.Duplicate)
            {
                try
                {
                    existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(uploadEntry.Guid,
                        targetProject.OrganizationId.ToString(), targetProject.ProjectId.ToString(), token);

                    if (existingAsset != null && uploadMode == AssetUploadMode.Ignore)
                    {
                        Utilities.DevLog($"Asset is already on the cloud: {existingAsset.Name}. Skipping...");
                        return new AssetUploadInfo(uploadEntry, existingAsset, true);
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

            IAsset asset;

            if (existingAsset != null && uploadMode == AssetUploadMode.Override)
            {
                asset = await RecycleAsset(uploadEntry, existingAsset, token);
            }
            else
            {
                asset = await CreateNewAsset(uploadEntry, targetProject, targetCollection, token);
            }

            return new AssetUploadInfo(uploadEntry, asset, false);
        }

        UploadOperation StartNewOperation(IUploadAssetEntry assetEntry)
        {
            var operation = new UploadOperation(assetEntry);
            m_AssetOperationManager.RegisterOperation(operation);

            operation.Start();

            return operation;
        }

        async Task PrepareUploadAsync(UploadOperation operation, IAsset asset,
            IDictionary<string, IAsset> guidToAssetLookup, CancellationToken token = default)
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

        async Task UploadAssetAsync(UploadOperation operation, IAsset asset, CancellationToken token = default)
        {
            try
            {
                await operation.UploadAsync(asset, token);
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
                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.UploadError, e.Message));
                throw;
            }
        }

        void AddDependencies(IUploadAssetEntry assetEntry, ICollection<IUploadAssetEntry> assetEntries,
            IReadOnlyDictionary<string, IUploadAssetEntry> database)
        {
            if (assetEntries.Contains(assetEntry))
                return;

            assetEntries.Add(assetEntry);
            foreach (var id in assetEntry.Dependencies)
            {
                if (database.TryGetValue(id, out var child))
                {
                    AddDependencies(child, assetEntries, database);
                }
            }
        }

        async Task<IAsset> CreateNewAsset(IUploadAssetEntry uploadAssetEntry, ProjectDescriptor targetProject,
            CollectionPath targetCollection, CancellationToken token)
        {
            var assetCreation = new AssetCreation(uploadAssetEntry.Name)
            {
                Collections = string.IsNullOrEmpty(targetCollection) ? null : new List<CollectionPath> { new(targetCollection) },
                Type = uploadAssetEntry.CloudType,
                Tags = uploadAssetEntry.Tags.ToList()
            };

            return await m_AssetsProvider.CreateAssetAsync(targetProject, assetCreation, token);
        }

        async Task<IAsset> RecycleAsset(IUploadAssetEntry uploadAssetEntry, IAsset asset, CancellationToken token)
        {
            if (asset.IsFrozen)
            {
                asset = await asset.CreateUnfrozenVersionAsync(token);
            }

            var assetUpdate = new AssetUpdate
            {
                Name = uploadAssetEntry.Name,
                Type = uploadAssetEntry.CloudType,
                Tags = uploadAssetEntry.Tags.ToList()
            };

            var sourceDataset = await asset.GetSourceDatasetAsync(default);
            var previewDataset = await asset.GetPreviewDatasetAsync(default);

            var tasks = new List<Task>
            {
                asset.UpdateAsync(assetUpdate, token),
                WipeDataset(sourceDataset, token),
                RemoveFileIfExistsAsync(previewDataset, Constants.ThumbnailFilename, token)
            };

            await Task.WhenAll(tasks);

            return asset;
        }

        static async Task RemoveFileIfExistsAsync(IDataset dataset, string path, CancellationToken token)
        {
            try
            {
                await dataset.RemoveFileAsync(path, token);
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound)
                    throw;
            }
        }

        static async Task WipeDataset(IDataset dataset, CancellationToken token)
        {
            var filesToWipe = new List<IFile>();
            await foreach (var file in dataset.ListFilesAsync(Range.All, token))
            {
                filesToWipe.Add(file);
            }

            var deleteTasks = new List<Task>();

            foreach (var file in filesToWipe)
            {
                deleteTasks.Add(dataset.RemoveFileAsync(file.Descriptor.Path, token));
            }

            await Task.WhenAll(deleteTasks);
        }
    }
}
