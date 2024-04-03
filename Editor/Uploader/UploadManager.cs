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
        Task UploadAsync(IReadOnlyCollection<IUploadAssetEntry> uploadEntries, UploadSettings settings, CancellationToken token = default);
    }

    enum AssetUploadMode
    {
        // TODO Communicates with the user to confirm if the current asset is identical to the one on the cloud
        IgnoreAlreadyUploadedAssets, // Skips the upload if an asset with the same ID already exists on the cloud
        OverrideExistingAssets, // Replaces and overrides any existing asset with the same ID on the cloud
        DuplicateExistingAssets, // Uploads new assets and potentially duplicates without checking for existing matches
    }

    [Serializable]
    class UploadSettings
    {
        public string OrganizationId;
        public string ProjectId;
        public string CollectionPath; //TODO Use a list
        public AssetUploadMode AssetUploadMode = AssetUploadMode.IgnoreAlreadyUploadedAssets;
    }

    [Serializable]
    class UploadManager : BaseService<IUploadManager>, IUploadManager
    {
        const int k_MaxConcurrentUploadTasks = 10;
        static readonly SemaphoreSlim s_UploadAssetSemaphore = new(k_MaxConcurrentUploadTasks);
        static readonly SemaphoreSlim s_CreateAssetSemaphore = new(k_MaxConcurrentUploadTasks);


        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager)
        {
            m_AssetOperationManager = assetOperationManager;
        }

        public async Task UploadAsync(IReadOnlyCollection<IUploadAssetEntry> uploadEntries, UploadSettings settings, CancellationToken token = default)
        {
            var assetEntriesWithAllDependencies = new List<IUploadAssetEntry>();

            var targetProject = new ProjectDescriptor(new OrganizationId(settings.OrganizationId), new ProjectId(settings.ProjectId));
            var targetCollection = settings.CollectionPath;

            var database = uploadEntries.ToDictionary(entry => entry.Guid);

            // Get all assets, including their dependencies
            foreach (var uploadEntry in uploadEntries)
            {
                AddDependencies(uploadEntry, assetEntriesWithAllDependencies, database);
            }

            var uploadEntryToAssetLookup = new Dictionary<IUploadAssetEntry, IAsset>();
            var uploadEntryToOperationLookup = new Dictionary<IUploadAssetEntry, UploadOperation>();
            var guidToAssetLookup = new Dictionary<string, IAsset>();


            // Prepare the IAssets
            var createAssetTasks = new List<Task<AssetUploadInfo>>();

            foreach (var uploadEntry in assetEntriesWithAllDependencies)
            {
                var operation = StartNewOperation(uploadEntry);
                uploadEntryToOperationLookup[uploadEntry] = operation;

                var task = CreateOrRecycleAsset(operation, uploadEntry, settings.AssetUploadMode, targetProject, targetCollection, token);
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
                    operation.Finish(OperationStatus.Success); // TODO Add a new Skip status
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
        }

        class AssetUploadInfo
        {
            public IUploadAssetEntry UploadEntry { get; }
            public IAsset Asset { get; }
            public bool Skip { get; }

            public AssetUploadInfo(IUploadAssetEntry uploadEntry, IAsset asset, bool skip)
            {
                UploadEntry = uploadEntry;
                Asset = asset;
                Skip = skip;
            }
        }

        async Task<AssetUploadInfo> CreateOrRecycleAsset(UploadOperation operation, IUploadAssetEntry uploadEntry,
            AssetUploadMode uploadMode, ProjectDescriptor targetProject, CollectionPath targetCollection, CancellationToken token)
        {
            await s_CreateAssetSemaphore.WaitAsync(token);

            try
            {
                return await CreateOrRecycleAsset(uploadEntry, uploadMode, targetProject,
                    targetCollection, token);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation.Finish(OperationStatus.Error);
            }
            finally
            {
                s_CreateAssetSemaphore.Release();
            }

            return null;
        }

        async Task<AssetUploadInfo> CreateOrRecycleAsset(IUploadAssetEntry uploadEntry, AssetUploadMode uploadMode,
            ProjectDescriptor targetProject, CollectionPath targetCollection, CancellationToken token)
        {
            IAsset existingAsset = null;

            if (uploadMode != AssetUploadMode.DuplicateExistingAssets)
            {
                try
                {
                    existingAsset = await AssetDataDependencyHelper.SearchForAssetWithGuid(targetProject.OrganizationId.ToString(),
                        targetProject.ProjectId.ToString(), uploadEntry.Guid, CancellationToken.None);

                    if (existingAsset != null)
                    {
                        // ReportStep("Asset is already on the cloud");

                        if (uploadMode == AssetUploadMode.IgnoreAlreadyUploadedAssets)
                        {
                            Utilities.DevLog($"Asset is already on the cloud: {existingAsset.Name}. Skipping...");
                            return new AssetUploadInfo(uploadEntry, existingAsset, true);
                        }
                    }
                }
                catch (Exception)
                {
                    // TODO The user might not have access to that project
                }
            }

            IAsset asset;

            if (existingAsset != null && uploadMode == AssetUploadMode.OverrideExistingAssets)
            {
                asset = existingAsset;
                await RecycleAsset(uploadEntry, asset, token);
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

        async Task PrepareUploadAsync(UploadOperation operation, IAsset asset, IDictionary<string, IAsset> guidToAssetLookup, CancellationToken token = default)
        {
            try
            {
                await operation.PrepareUploadAsync(asset, guidToAssetLookup, token);
                operation.Finish(OperationStatus.Success);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.PreparationError, e.Message));
                throw;
            }
            finally
            {
                s_UploadAssetSemaphore.Release();
            }
        }

        async Task UploadAssetAsync(UploadOperation operation, IAsset asset, CancellationToken token = default)
        {
            await s_UploadAssetSemaphore.WaitAsync(token);

            try
            {
                await operation.UploadAsync(asset, token);
                operation.Finish(OperationStatus.Success);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.Ok));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation.Finish(OperationStatus.Error);
                AnalyticsSender.SendEvent(new UploadEndEvent(UploadEndStatus.UploadError, e.Message));
                throw;
            }
            finally
            {
                s_UploadAssetSemaphore.Release();
            }
        }

        void AddDependencies(IUploadAssetEntry assetEntry, ICollection<IUploadAssetEntry> assetEntries, IReadOnlyDictionary<string, IUploadAssetEntry> database)
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

        async Task<IAsset> CreateNewAsset(IUploadAssetEntry uploadAssetEntry, ProjectDescriptor targetProject, CollectionPath targetCollection, CancellationToken token)
        {
            var project = await Services.AssetRepository.GetAssetProjectAsync(targetProject, token);

            var assetCreation = new AssetCreation(uploadAssetEntry.Name)
            {
                Collections = string.IsNullOrEmpty(targetCollection) ? null : new List<CollectionPath> { new (targetCollection) },
                Type = uploadAssetEntry.CloudType,
                Tags = uploadAssetEntry.Tags.ToList()
            };

            return await project.CreateAssetAsync(assetCreation, token);
        }

        async Task RecycleAsset(IUploadAssetEntry uploadAssetEntry, IAsset asset, CancellationToken token)
        {
            var assetUpdate = new AssetUpdate
            {
                Name = uploadAssetEntry.Name,
                Type = uploadAssetEntry.CloudType,
                Tags = uploadAssetEntry.Tags.ToList(),
            };

            var sourceDataset = await asset.GetSourceDatasetAsync(default);
            var previewDataset = await asset.GetPreviewDatasetAsync(default);

            var tasks = new List<Task>
            {
                asset.UpdateAsync(assetUpdate, token),
                WipeDataset(sourceDataset, token), // TODO Remove only what is not needed
                RemoveFileIfExistsAsync(previewDataset, Constants.ThumbnailFilename, token)
            };

            await Task.WhenAll(tasks);
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
                if (AssetDataDependencyHelper.IsAGuidSystemFile(file.Descriptor.Path)) // Keep the am4u_guid file so we can track back this asset if anything goes wrong
                    continue;

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
