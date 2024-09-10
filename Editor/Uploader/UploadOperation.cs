using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class UploadOperation : AssetDataOperation, IProgress<HttpProgress>
    {
        const string k_StatusInReview = "InReview";
        const string k_StatusApproved = "Approved";

        // All files upload from every assets should be limited in how many can be uploaded at the same time.
        static readonly SemaphoreSlim k_MaxFileUploadSemaphore = new(50);

        readonly HashSet<HttpProgress> m_HttpProgresses = new();
        readonly IUploadAsset m_UploadAsset;

        string m_Description;
        float m_Progress;

        public override AssetIdentifier Identifier => new(m_UploadAsset.Guid);
        public override float Progress => m_Progress;
        public override string OperationName => $"Uploading {Path.GetFileName(m_UploadAsset.Name)}";
        public override string Description => m_Description;
        public override bool StartIndefinite => true;
        public override bool IsSticky => true;

        public UploadOperation(IUploadAsset uploadAsset)
        {
            m_UploadAsset = uploadAsset;
        }

        public void Report(HttpProgress value)
        {
            if (!m_HttpProgresses.Contains(value))
            {
                m_HttpProgresses.Add(value);
            }

            var totalProgress = m_HttpProgresses.Where(httpProgress => httpProgress.UploadProgress != null)
                .Sum(httpProgress => httpProgress.UploadProgress.Value);

            totalProgress /= m_HttpProgresses.Count;

            ReportStep($"Uploading {m_UploadAsset.Files.Count} file(s)", totalProgress);
        }

        public async Task PrepareUploadAsync(AssetData asset, IDictionary<string, AssetData> guidToAssetLookup,
            CancellationToken token = default)
        {
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            ReportStep("Preparing manifest...");

            var dependencies = new List<AssetIdentifier>();

            var tasks = new List<Task>();

            // Dependency manifest
            if (m_UploadAsset.Dependencies.Any())
            {
                foreach (var dependencyGuid in m_UploadAsset.Dependencies)
                {
                    if (guidToAssetLookup.TryGetValue(dependencyGuid, out var depAsset))
                    {
                        dependencies.Add(depAsset.Identifier);
                    }

                    // If the dependency wasn't found in the guid lookup it means it's an existing asset and not a new upload.
                    else
                    {
                        tasks.Add(AddDependencyAsync(dependencies, asset.Identifier, dependencyGuid, token));
                    }
                }
            }

            await Task.WhenAll(tasks);

            // Even if there are no dependencies, we may need to clear any existing dependencies
            await assetsProvider.UpdateDependenciesAsync(asset.Identifier, dependencies, token);
        }

        static async Task AddDependencyAsync(List<AssetIdentifier> dependencies, AssetIdentifier assetIdentifier,
            string dependencyGuid, CancellationToken token)
        {
            var existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(
                dependencyGuid, assetIdentifier.OrganizationId, assetIdentifier.ProjectId, token);

            if (existingAsset != null)
            {
                dependencies.Add(existingAsset.Identifier);
            }
        }

        public async Task UploadAsync(AssetData asset, CancellationToken token = default)
        {
            ReportStep("Preparing for upload");

            var assetPath = AssetDatabase.GUIDToAssetPath(m_UploadAsset.Guid);
            var assetInstance = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            // Upload files tasks
            var uploadThumbnailTask = Task.FromResult<AssetDataFile>(null);

            // Files inside the asset entry
            await TaskUtils.RunWithMaxConcurrentTasksAsync(m_UploadAsset.Files.Where(file => !string.IsNullOrEmpty(file.SourcePath)), token,
                file =>
                {
                    ReportStep($"Preparing file {Path.GetFileName(file.SourcePath)}");
                    var task = assetsProvider.UploadFile(asset, file.DestinationPath, file.SourcePath, this, token);
                    return task;
                }, k_MaxFileUploadSemaphore);

            // Thumbnail
            if (RequiresThumbnail(assetInstance, assetPath))
            {
                ReportStep("Preparing thumbnail");

                var texture = await GetThumbnailAsync(assetInstance, assetPath);
                if (texture != null)
                {
                    uploadThumbnailTask = assetsProvider.UploadThumbnail(asset, texture, this, token);
                }
            }

            var thumbnailFile = await uploadThumbnailTask;

            if (thumbnailFile != null && !string.IsNullOrEmpty(thumbnailFile.Path))
            {
                ReportStep("Applying thumbnail");

                var existingTags = asset.Tags ?? new List<string>();
                var assetUpdate = new AssetUpdate
                {
                    Type = m_UploadAsset.AssetType,
                    Tags = existingTags.Union(thumbnailFile.Tags ?? Array.Empty<string>()).ToList(),
                    PreviewFile = thumbnailFile.Path
                };

                await assetsProvider.UpdateAsync(asset, assetUpdate, token);
            }

            try
            {
                await assetsProvider.UpdateStatusAsync(asset, k_StatusInReview, token);
                await assetsProvider.UpdateStatusAsync(asset, k_StatusApproved, token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unable to publish asset '{asset?.Name}'. Asset will stay in Draft status.");
                Utilities.DevLog(e.ToString());
            }

            try
            {
                await assetsProvider.FreezeAsync(asset, Constants.UploadChangelog, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (asset != null)
                    Debug.LogWarning(
                        $"Unable to commit asset version for asset {asset.Identifier.AssetId}. Asset will stay in Pending status.");

                Utilities.DevLog(e.ToString());
            }

            await assetsProvider.RefreshAsync(asset, token);

            ReportStep("Done");
        }

        void ReportStep(string description, float progress = 0.0f)
        {
            m_Description = description;
            m_Progress = progress;

            Report();
        }

        static Task<Texture2D> GetThumbnailAsync(Object asset, string assetPath)
        {
            return AssetManagerPreviewer.GenerateAdvancedPreview(asset, assetPath, 512);
        }

        static bool RequiresThumbnail(Object assetInstance, string assetPath)
        {
            return assetInstance is not Texture2D ||
                   !AssetDataTypeHelper.IsSupportingPreviewGeneration(Path.GetExtension(assetPath));
        }
    }
}
