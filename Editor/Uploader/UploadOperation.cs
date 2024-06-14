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
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class UploadOperation : AssetDataOperation, IProgress<HttpProgress>
    {
        const int k_MaxConcurrentFileTasks = 10;

        readonly HashSet<HttpProgress> m_HttpProgresses = new();
        readonly IUploadAssetEntry m_UploadEntry;

        string m_Description;
        float m_Progress;

        public override AssetIdentifier Identifier => UploadAssetData.LocalAssetIdentifier(m_UploadEntry.Guid);
        public override float Progress => m_Progress;
        public override string OperationName => $"Uploading {Path.GetFileName(m_UploadEntry.Name)}";
        public override string Description => m_Description;
        public override bool StartIndefinite => true;
        public override bool IsSticky => false;

        public UploadOperation(IUploadAssetEntry uploadEntry)
        {
            m_UploadEntry = uploadEntry;
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

            ReportStep($"Uploading {m_UploadEntry.Files.Count} file(s)", totalProgress);
        }

        public async Task PrepareUploadAsync(IAsset asset, IDictionary<string, IAsset> guidToAssetLookup,
            CancellationToken token = default)
        {
            var tasks = new List<Task>();

            var sourceDataset = await asset.GetSourceDatasetAsync(token);

            // Dependency manifest
            if (m_UploadEntry.Dependencies.Any())
            {
                ReportStep("Preparing manifest...");

                foreach (var depGuid in m_UploadEntry.Dependencies)
                {
                    if (guidToAssetLookup.TryGetValue(depGuid, out var depAsset))
                    {
                        tasks.Add(UploadDependencySystemFileAsync(sourceDataset, depAsset, token));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task UploadAsync(IAsset asset, CancellationToken token = default)
        {
            ReportStep("Preparing for upload");

            var assetPath = AssetDatabase.GUIDToAssetPath(m_UploadEntry.Guid);
            var assetInstance = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            var sourceDataset = await asset.GetSourceDatasetAsync(default);

            // Upload files
            var tasks = new List<Task>();

            // Files inside the asset entry
            foreach (var file in m_UploadEntry.Files)
            {
                var relativePath = Utilities.GetPathRelativeToAssetsFolder(file);

                ReportStep($"Preparing file {Path.GetFileName(file)}");
                tasks.Add(UploadFile(file, relativePath, sourceDataset, token));
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
                    previewFile = Constants.ThumbnailFilename;
                    tasks.Add(UploadFile(texture.EncodeToPNG(), previewFile, previewDataset, token));
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

            try
            {
                await asset.FreezeAsync(Constants.UploadChangelog, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unable to commit asset version for asset {asset.Descriptor.AssetId}. Asset will stay in Pending status.");
                Utilities.DevLog(e.ToString());
            }

            await asset.RefreshAsync(token);

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

        async Task UploadFile(string sourcePath, string destPath, IDataset targetDataset, CancellationToken token)
        {
            await using (var stream = File.OpenRead(sourcePath))
            {
                await UploadFile(stream, destPath, targetDataset, token);
            }
        }

        async Task UploadFile(byte[] bytes, string destPath, IDataset targetDataset, CancellationToken token)
        {
            await using (var stream = new MemoryStream(bytes))
            {
                await UploadFile(stream, destPath, targetDataset, token);
            }
        }

        async Task UploadFile(Stream stream, string destPath, IDataset targetDataset, CancellationToken token)
        {
            var fileCreation = new FileCreation(destPath.Replace('\\', '/')); // Backend doesn't support backslashes AMECO-2616

            try
            {
                await targetDataset.UploadFileAsync(fileCreation, stream, this, token);
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.Conflict)
                {
                    Debug.LogError($"Unable to upload file {destPath} to dataset {targetDataset?.Name}");
                    throw;
                }
            }
        }

        async Task UploadDependencySystemFileAsync(IDataset sourceDataset, IAsset asset, CancellationToken token)
        {
            var assetId = asset.Descriptor.AssetId.ToString();
            var assetVersion = asset.Descriptor.AssetVersion.ToString();
            var depFile = AssetDataDependencyHelper.EncodeDependencySystemFilename(assetId, assetVersion);

            await UploadFile(new byte[] { }, depFile, sourceDataset, token);
        }
    }
}
