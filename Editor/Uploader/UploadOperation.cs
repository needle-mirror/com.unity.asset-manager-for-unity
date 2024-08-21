using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class UploadOperation : AssetDataOperation, IProgress<HttpProgress>
    {
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
            var tasks = new List<Task>();

            // Dependency manifest
            if (m_UploadAsset.Dependencies.Any())
            {
                ReportStep("Preparing manifest...");

                foreach (var depGuid in m_UploadAsset.Dependencies)
                {
                    if (guidToAssetLookup.TryGetValue(depGuid, out var depAsset))
                    {
                        tasks.Add(UploadDependencySystemFileAsync(asset, depAsset, token));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task UploadAsync(AssetData asset, CancellationToken token = default)
        {
            ReportStep("Preparing for upload");

            var assetPath = AssetDatabase.GUIDToAssetPath(m_UploadAsset.Guid);
            var assetInstance = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            // Upload files tasks
            var uploadTasks = new List<Task>();
            var uploadThumbnailTask = Task.FromResult<AssetDataFile>(null);

            // Files inside the asset entry
            foreach (var file in m_UploadAsset.Files)
            {
                if (string.IsNullOrEmpty(file.SourcePath))
                {
                    continue;
                }

                ReportStep($"Preparing file {Path.GetFileName(file.SourcePath)}");

                uploadTasks.Add(assetsProvider.UploadFile(asset, file.DestinationPath, file.SourcePath, this, token));
            }

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

            await Task.WhenAll(uploadTasks);
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
                await assetsProvider.UpdateStatusAsync(asset, AssetStatusAction.SendForReview, token);
                await assetsProvider.UpdateStatusAsync(asset, AssetStatusAction.Approve, token);
                await assetsProvider.UpdateStatusAsync(asset, AssetStatusAction.Publish, token);
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

        async Task UploadDependencySystemFileAsync(AssetData assetData, AssetData dependencyAsset,
            CancellationToken token)
        {
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            var dependencyAssetId = dependencyAsset.Identifier.AssetId;
            var dependencyAssetVersion = dependencyAsset.Identifier.Version;
            var dependencyFilePath =
                AssetDataDependencyHelper.EncodeDependencySystemFilename(dependencyAssetId, dependencyAssetVersion);

            using var stream = new MemoryStream(new byte[] { });
            await assetsProvider.UploadFile(assetData, dependencyFilePath, stream, this, token);
        }

        static bool RequiresThumbnail(Object assetInstance, string assetPath)
        {
            return assetInstance is not Texture2D ||
                   !AssetDataTypeHelper.IsSupportingPreviewGeneration(Path.GetExtension(assetPath));
        }
    }
}