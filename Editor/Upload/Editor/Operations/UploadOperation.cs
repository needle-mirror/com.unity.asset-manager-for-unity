using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.Cloud.CommonEmbedded;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Utilities = Unity.AssetManager.Core.Editor.Utilities;

namespace Unity.AssetManager.Upload.Editor
{
    class UploadOperation : AssetDataOperation, IProgress<HttpProgress>
    {
        // All files upload from every assets should be limited in how many can be uploaded at the same time.
        static readonly SemaphoreSlim k_MaxFileUploadSemaphore = new(15);

        readonly HashSet<HttpProgress> m_HttpProgresses = new();
        readonly IUploadAsset m_UploadAsset;

        string m_Description;
        float m_Progress;

        public override AssetIdentifier Identifier => m_UploadAsset.LocalIdentifier;
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
            m_HttpProgresses.Add(value);

            var totalProgress = m_HttpProgresses.Where(httpProgress => httpProgress.UploadProgress != null)
                .Sum(httpProgress => httpProgress.UploadProgress.Value);

            totalProgress /= m_HttpProgresses.Count;

            ReportStep(totalProgress);
        }

        public async Task PrepareUploadAsync(AssetData targetAssetData, IDictionary<AssetIdentifier, AssetData> identifierToAssetLookup,
            CancellationToken token = default)
        {
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            ReportStep("Preparing manifest...");

            var dependencies = new List<AssetIdentifier>();

            // Dependency manifest
            foreach (var dependency in m_UploadAsset.Dependencies)
            {
                if (dependency.IsLocal())
                {
                    // If the dependency is pointing to a local asset, we need to resolve its target asset data
                    dependencies.Add(identifierToAssetLookup[dependency].Identifier);
                }
                else
                {
                    // Otherwise, the dependency is already pointing to a cloud asset
                    dependencies.Add(dependency);
                }
            }

            // Even if there are no dependencies, we may need to clear any existing dependencies
            await assetsProvider.UpdateDependenciesAsync(targetAssetData.Identifier, dependencies, token);
        }

        public async Task UploadAsync(AssetData targetAssetData, CancellationToken token = default)
        {
            ReportStep("Preparing for upload");

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            // Create a thumbnail

            GetDatabaseAssetInfo(m_UploadAsset, out var assetPath, out var assetInstance);
            var thumbnailFile = await UploadThumbnailAsync(assetsProvider, assetInstance, assetPath, targetAssetData, token);

            // Upload files tasks

            var fileNumber = 0;
            await TaskUtils.RunWithMaxConcurrentTasksAsync(m_UploadAsset.Files.Where(file => !string.IsNullOrEmpty(file.SourcePath)), token,
                file =>
                {
                    Interlocked.Increment(ref fileNumber);
                    ReportStep($"Preparing file {Path.GetFileName(file.SourcePath)} ({fileNumber} of {m_UploadAsset.Files.Count})");
                    return UploadFile(file.DestinationPath, file.SourcePath);
                }, k_MaxFileUploadSemaphore);

            // Finalize asset

            await ApplyThumbnailAsync(assetsProvider, targetAssetData, thumbnailFile, token);
            await UpdateStatusAsync(assetsProvider, targetAssetData, token);
            await FreezeAssetAsync(assetsProvider, targetAssetData, token);

            ReportStep("Done");

            async Task UploadFile(string destinationPath, string sourcePath)
            {
                await using var stream = File.OpenRead(sourcePath);
                var file = await assetsProvider.UploadFile(targetAssetData, destinationPath, stream, this, token);
                // If the thumbnail has not been set, select a file that is supported for preview
                thumbnailFile ??= AssetDataTypeHelper.IsSupportingPreviewGeneration(Path.GetExtension(destinationPath)) ? file : null;
            }
        }

        void ReportStep(string description)
        {
            m_Description = description;
            Report();
        }

        void ReportStep(float progress = 0.0f)
        {
            m_Progress = progress;
            Report();
        }

        static void GetDatabaseAssetInfo(IUploadAsset uploadAsset, out string assetPath, out Object assetInstance)
        {
            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            assetPath = assetDatabaseProxy.GuidToAssetPath(uploadAsset.PreviewGuid);
            assetInstance = assetDatabaseProxy.LoadAssetAtPath(assetPath);
        }

        async Task<AssetDataFile> UploadThumbnailAsync(IAssetsProvider assetsProvider, Object assetInstance, string assetPath, AssetData targetAssetData,
            CancellationToken token)
        {
            if (!RequiresThumbnail(assetInstance, assetPath))
                return null;

            ReportStep("Preparing thumbnail");

            AssetDataFile thumbnailFile = null;
            var texture = await AssetPreviewer.GenerateAdvancedPreview(assetInstance, assetPath, 512);
            if (texture != null)
            {
                thumbnailFile = await assetsProvider.UploadThumbnail(targetAssetData, texture, this, token);
            }

            return thumbnailFile;
        }

        static bool RequiresThumbnail(Object assetInstance, string assetPath)
        {
            return assetInstance is not Texture2D ||
                   !AssetDataTypeHelper.IsSupportingPreviewGeneration(Path.GetExtension(assetPath));
        }

        async Task ApplyThumbnailAsync(IAssetsProvider assetsProvider, AssetData targetAssetData, AssetDataFile thumbnailFile, CancellationToken token)
        {
            if (thumbnailFile != null && !string.IsNullOrEmpty(thumbnailFile.Path))
            {
                ReportStep("Applying thumbnail");

                var existingTags = targetAssetData.Tags ?? new List<string>();
                var assetUpdate = new AssetUpdate
                {
                    // Bubble up the generated tags from the thumbnail to the asset
                    Tags = existingTags.Union(thumbnailFile.Tags ?? Array.Empty<string>()).ToList(),
                    PreviewFile = thumbnailFile.Path
                };

                await assetsProvider.UpdateAsync(targetAssetData, assetUpdate, token);
            }
        }

        static async Task UpdateStatusAsync(IAssetsProvider assetsProvider, AssetData targetAssetData, CancellationToken token)
        {
            try
            {
                await assetsProvider.UpdateStatusAsync(targetAssetData, AssetManagerCoreConstants.StatusInReview, token);
                await assetsProvider.UpdateStatusAsync(targetAssetData, AssetManagerCoreConstants.StatusApproved, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unable to publish asset '{targetAssetData?.Name}'. Asset will stay in Draft status.");
                Utilities.DevLogException(e);
            }
        }

        static async Task FreezeAssetAsync(IAssetsProvider assetsProvider, AssetData targetAssetData, CancellationToken token)
        {
            try
            {
                await assetsProvider.FreezeAsync(targetAssetData, null, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unable to commit asset version for asset {targetAssetData?.Name}. Asset version will remain unfrozen.");
                Utilities.DevLogException(e);
            }
        }
    }
}
