using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{

    [Serializable]
    public class UploadSettings
    {
        public string OrganizationId;
        public string ProjectId;
        public string CollectionPath; //TODO Use a list
    }

    class UploadTaskDispatcher
    {
        readonly Dictionary<string, Task> m_Tasks = new();

        const int k_MaxRunningTasks = 10;
        static readonly SemaphoreSlim s_Semaphore = new (k_MaxRunningTasks);

        public Task Start(IUploadAssetEntry assetEntry, UploadSettings settings)
        {
            if (m_Tasks.TryGetValue(assetEntry.Guid, out var task))
            {
                return task;
            }

            var newTask = UploadAsyncWithProgress(assetEntry, settings);

            m_Tasks.Add(assetEntry.Guid, newTask);

            return newTask;
        }

        static async Task UploadAsyncWithProgress(IUploadAssetEntry assetEntry, UploadSettings settings)
        {
            await s_Semaphore.WaitAsync();

            var uploadTask = new UploadTask(assetEntry, settings, null);

            uploadTask.Start();

            try
            {
                await uploadTask.UploadAsync();
                uploadTask.Finish(OperationStatus.Success);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                uploadTask.Finish(OperationStatus.Error);
            }
            finally
            {
                s_Semaphore.Release();
            }
        }
    }

    class UploadTask : BaseOperation, IProgress<HttpProgress>
    {
        readonly IUploadAssetEntry m_AssetEntry;
        readonly ProjectDescriptor m_TargetProject;
        readonly List<CollectionPath> m_TargetCollections;

        string m_Description;
        float m_Progress;

        public UploadTask(IUploadAssetEntry assetEntry, UploadSettings settings, BaseOperation parent)
            : base(parent)
        {
            m_AssetEntry = assetEntry;

            m_TargetProject = new ProjectDescriptor(new OrganizationId(settings.OrganizationId), new ProjectId(settings.ProjectId));

            if (!string.IsNullOrEmpty(settings.CollectionPath))
            {
                m_TargetCollections = new List<CollectionPath> { new (settings.CollectionPath) };
            }
        }

        public override float Progress => m_Progress;
        protected override string OperationName => $"Uploading {Path.GetFileName(m_AssetEntry.Files.First())}";
        protected override string Description => m_Description;
        protected override bool StartIndefinite => true;
        protected override bool IsSticky => true;

        public async Task UploadAsync()
        {
            ReportStep("Authenticating");

            await Services.InitializeAuthenticatorAsync(); // TODO Move from here

            ReportStep("Preparing for upload");

            var assetRepository = Services.AssetRepository;

            var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetEntry.Guid);
            var assetInstance = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            var assetCreation = new AssetCreation(m_AssetEntry.Name)
            {
                Collections = m_TargetCollections,
                /*
                Metadata = new Dictionary<string, object>
                {
                    { "AssetGuid", m_AssetEntry.Guid },
                    { "Dependencies", m_AssetEntry.Dependencies } // TODO Gather AssetIds of uploaded stuff.
                },
                */
                Type = m_AssetEntry.CloudType,
                Tags = m_AssetEntry.Tags.ToList()
            };

            ReportStep("Creating cloud asset");

            var project = await assetRepository.GetAssetProjectAsync(m_TargetProject, default);
            var asset = await project.CreateAssetAsync(assetCreation, default);

            var datasets = await GetDatasets(asset);
            var sourceDataset = datasets[0]; // TODO Use Dataset Tags

            // Upload files
            var tasks = new List<Task>();

            foreach (var file in m_AssetEntry.Files)
            {
                var relativePath = Path.GetRelativePath(Application.dataPath, file).Replace('\\', '/');

                ReportStep($"Preparing file {Path.GetFileName(file)}");
                tasks.Add(UploadFile(file, relativePath, sourceDataset));
            }

            string previewFile = null;

            if (assetInstance is not Texture2D)
            {
                ReportStep("Preparing thumbnail");

                var texture = await GetThumbnailAsync(assetInstance, assetPath);

                if (texture != null)
                {
                    var previewDataset = datasets[1]; // TODO Use Dataset Tags
                    previewFile = "unity_thumbnail.png";
                    tasks.Add(UploadFile(texture.EncodeToPNG(), previewFile, previewDataset));
                }
            }

            await Task.WhenAll(tasks);

            if (!string.IsNullOrEmpty(previewFile))
            {
                ReportStep("Applying thumbnail");
                var assetUpdate = new AssetUpdate
                {
                    Type = m_AssetEntry.CloudType,
                    PreviewFile = previewFile
                };

                await asset.UpdateAsync(assetUpdate, default);
            }

            var publish = false; // TODO Expose

            if (publish && asset.Status != "Published")
            {
                ReportStep("Publishing cloud asset");
                await asset.SendToReviewAsync(default);
                await asset.ApproveAsync(default);
                await asset.PublishAsync(default);
            }

            ReportStep("Done");
        }

        void ReportStep(string description, float progress = 0.0f)
        {
            m_Description = description;
            m_Progress = progress;

            if (Utilities.IsDevMode)
            {
                Debug.Log(description);
            }

            Report();
        }

        static async Task<IDataset[]> GetDatasets(IAsset asset)
        {
            var datasetsAsync = asset.ListDatasetsAsync(Range.All, default);
            List<IDataset> datasets = new();
            await foreach (var dataset in datasetsAsync)
            {
                datasets.Add(dataset);
            }

            return datasets.ToArray();
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

            ReportStep($"Uploading {m_AssetEntry.Files.Count} file(s)", totalProgress);
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

            await targetDataset.UploadFileAsync(fileCreation, stream, this, default);

            stream.Close();
        }
    }
}