using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class UploadOperation : AssetDataOperation, IProgress<HttpProgress>
    {
        readonly IUploadAssetEntry m_UploadEntry;
        readonly ProjectDescriptor m_TargetProject;
        readonly List<CollectionPath> m_TargetCollections;

        readonly AssetUploadMode m_UploadMode;

        const int k_MaxConcurrentFileTasks = 10;
        static readonly SemaphoreSlim s_UploadFileSemaphore = new(k_MaxConcurrentFileTasks);

        string m_Description;
        float m_Progress;
        public override AssetIdentifier AssetId => new(null, null, m_UploadEntry.Guid, "1");

        public UploadOperation(IUploadAssetEntry uploadEntry, UploadSettings settings)
        {
            m_UploadEntry = uploadEntry;

            m_TargetProject = new ProjectDescriptor(new OrganizationId(settings.OrganizationId), new ProjectId(settings.ProjectId));

            if (!string.IsNullOrEmpty(settings.CollectionPath))
            {
                m_TargetCollections = new List<CollectionPath> { new (settings.CollectionPath) };
            }

            m_UploadMode = settings.AssetUploadMode;
        }

        public override float Progress => m_Progress;
        public override string OperationName => $"Uploading {Path.GetFileName(m_UploadEntry.Files.First())}";
        public override string Description => m_Description;
        public override bool StartIndefinite => true;
        public override bool IsSticky => true;

        static readonly string k_ThumbnailFilename = "unity_thumbnail.png";

        public async Task UploadAsync(IDictionary<string, string> assetIdDatabase, CancellationToken token = default)
        {
            IAsset existingAsset = null;

            if (m_UploadMode != AssetUploadMode.DuplicateExistingAssets)
            {
                try
                {
                    existingAsset = await SearchAssetWithGuid(m_UploadEntry.Guid, token);
                    if (existingAsset != null)
                    {
                        assetIdDatabase.Add(m_UploadEntry.Guid, existingAsset.Descriptor.AssetId.ToString());
                        ReportStep("Asset is already on the cloud");

                        if (m_UploadMode == AssetUploadMode.IgnoreAlreadyUploadedAssets)
                        {
                            Utilities.DevLog($"Asset is already on the cloud: {existingAsset.Name}. Skipping...");
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    // TODO The user might not have access to that project
                }
            }

            // Validate dependencies
            ValidateDependencies(m_UploadEntry, assetIdDatabase);

            ReportStep("Preparing for upload");

            var assetRepository = Services.AssetRepository;

            var assetPath = AssetDatabase.GUIDToAssetPath(m_UploadEntry.Guid);
            var assetInstance = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            IAsset asset;

            if (existingAsset != null && m_UploadMode == AssetUploadMode.OverrideExistingAssets)
            {
                ReportStep("Updating cloud asset");

                asset = existingAsset;
                await RecycleAsset(asset, token);
            }
            else
            {
                ReportStep("Creating cloud asset");
                asset = await CreateNewAsset(assetRepository, token);
            }

            assetIdDatabase[m_UploadEntry.Guid] = asset.Descriptor.AssetId.ToString();

            var sourceDataset = await asset.GetSourceDatasetAsync(default);

            // Upload files
            var tasks = new List<Task>();

            // Upload the Guid file
            var guidFile = AssetDataDependencyHelper.GenerateGuidSystemFilename(m_UploadEntry);
            tasks.Add(UploadFile(new byte[] { }, guidFile, sourceDataset));

            // Dependency manifest
            if (m_UploadEntry.Dependencies.Any())
            {
                ReportStep("Preparing manifest...");

                foreach (var depGuid in m_UploadEntry.Dependencies)
                {
                    tasks.Add(UploadDependencySystemFileAsync(sourceDataset, depGuid, assetIdDatabase, token));
                }
            }

            // Files inside the asset entry

            foreach (var file in m_UploadEntry.Files)
            {
                var relativePath = Path.GetRelativePath(Application.dataPath, file).Replace('\\', '/');

                ReportStep($"Preparing file {Path.GetFileName(file)}");
                tasks.Add(UploadFile(file, relativePath, sourceDataset));
            }

            // Thumbnail

            string previewFile = null;

            if (assetInstance is not Texture2D)
            {
                ReportStep("Preparing thumbnail");

                var texture = await GetThumbnailAsync(assetInstance, assetPath);

                if (texture != null)
                {
                    var previewDataset = await asset.GetPreviewDatasetAsync(default);
                    previewFile = k_ThumbnailFilename;
                    tasks.Add(UploadFile(texture.EncodeToPNG(), previewFile, previewDataset));
                }
            }

            await Task.WhenAll(tasks);

            if (!string.IsNullOrEmpty(previewFile))
            {
                ReportStep("Applying thumbnail");
                var assetUpdate = new AssetUpdate
                {
                    Type = m_UploadEntry.CloudType,
                    PreviewFile = previewFile
                };

                await asset.UpdateAsync(assetUpdate, token);
            }

            var publish = false; // TODO Expose

            if (publish && asset.Status != "Published")
            {
                ReportStep("Publishing cloud asset");
                await asset.UpdateStatusAsync(AssetStatusAction.SendForReview,default);
                await asset.UpdateStatusAsync(AssetStatusAction.Approve, default);
                await asset.UpdateStatusAsync(AssetStatusAction.Publish,default);
            }

            ReportStep("Done");
        }

        void ReportStep(string description, float progress = 0.0f)
        {
            m_Description = description;
            m_Progress = progress;

            Report();
        }

        static Task<Texture2D> GetThumbnailAsync(UnityEngine.Object asset, string assetPath)
        {
            return AssetManagerPreviewer.GenerateAdvancedPreview(asset, assetPath, 512);
        }

        readonly HashSet<HttpProgress> m_HttpProgresses = new();

        public void Report(HttpProgress value)
        {
            if (!m_HttpProgresses.Contains(value))
            {
                m_HttpProgresses.Add(value);
            }

            var totalProgress = m_HttpProgresses.Where(httpProgress => httpProgress.UploadProgress != null)
                .Sum(httpProgress => httpProgress.UploadProgress.Value);

            totalProgress /= m_HttpProgresses.Count;

            ReportStep($"Uploading {m_UploadEntry.Files.Count} file(s)", totalProgress);
        }

        async Task UploadFile(string sourcePath, string destPath, IDataset targetDataset)
        {
            await UploadFile(File.OpenRead(sourcePath), destPath, targetDataset);
        }

        async Task UploadFile(byte[] bytes, string destPath, IDataset targetDataset)
        {
            await UploadFile(new MemoryStream(bytes), destPath, targetDataset);
        }

        async Task UploadFile(Stream stream, string destPath, IDataset targetDataset)
        {
            var fileCreation = new FileCreation { Path = destPath };

            await s_UploadFileSemaphore.WaitAsync();

            try
            {
                await targetDataset.UploadFileAsync(fileCreation, stream, this, default);
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.Conflict)
                    throw;
            }
            finally
            {
                s_UploadFileSemaphore.Release();
            }

            stream.Close();
        }

        static void ValidateDependencies(IUploadAssetEntry uploadEntry, IDictionary<string, string> assetIdDatabase)
        {
            if (uploadEntry.Dependencies.Any(guid => !assetIdDatabase.ContainsKey(guid)))
            {
                throw new Exception($"Some dependency for {uploadEntry.Name} was not uploaded. Aborting.");
            }
        }

        async Task UploadDependencySystemFileAsync(IDataset sourceDataset, string dependencyGuid, IDictionary<string, string> assetIdDatabase, CancellationToken token)
        {
            IAsset asset = null;

            try
            {
                asset = await SearchAssetWithGuid(dependencyGuid, token);
            }
            catch (Exception)
            {
                // ignored
            }

            if (asset != null)
            {
                Utilities.DevLog($"Found existing asset for dependency: {asset.Name}");
            }

            var assetId = asset != null ? asset.Descriptor.AssetId.ToString() : assetIdDatabase[dependencyGuid];
            var depFile = AssetDataDependencyHelper.GenerateDependencySystemFilename(assetId);

            await UploadFile(new byte[] { }, depFile, sourceDataset);
        }

        async Task<IAsset> SearchAssetWithGuid(string guid, CancellationToken token)
        {
            return await AssetDataDependencyHelper.SearchForAssetWithGuid(m_TargetProject.OrganizationId.ToString(),
                m_TargetProject.ProjectId.ToString(), guid, token);
        }

        async Task<IAsset> CreateNewAsset(IAssetRepository assetRepository, CancellationToken token)
        {
            var project = await assetRepository.GetAssetProjectAsync(m_TargetProject, token);

            var assetCreation = new AssetCreation(m_UploadEntry.Name)
            {
                Collections = m_TargetCollections,
                Type = m_UploadEntry.CloudType,
                Tags = m_UploadEntry.Tags.ToList()
            };

            return await project.CreateAssetAsync(assetCreation, token);
        }

        async Task RecycleAsset(IAsset asset, CancellationToken token)
        {
            var assetUpdate = new AssetUpdate
            {
                Name = m_UploadEntry.Name,
                Type = m_UploadEntry.CloudType,
                Tags = m_UploadEntry.Tags.ToList(),
            };

            var sourceDataset = await asset.GetSourceDatasetAsync(default);
            var previewDataset = await asset.GetPreviewDatasetAsync(default);

            var tasks = new List<Task>
            {
                asset.UpdateAsync(assetUpdate, token),
                WipeDataset(sourceDataset, token), // TODO Remove only what is not needed
                RemoveFileIfExistsAsync(previewDataset, k_ThumbnailFilename, token)
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