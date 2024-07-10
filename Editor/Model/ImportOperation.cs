using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class ImportOperation : AssetDataOperation
    {
        public enum ImportType
        {
            Import,
            UpdateToLatest
        }

        public interface IFileDownload
        {
            public string DownloadPath { get; }
            public string Error { get; }
        }

        class FileDownloadRequests : IFileDownload
        {
            public string DownloadPath { get; }
            public string Error => m_WebRequest.error;

            public UnityWebRequest WebRequest => m_WebRequest;

            readonly UnityWebRequest m_WebRequest;

            public FileDownloadRequests(string downloadPath, UnityWebRequest webRequest)
            {
                DownloadPath = downloadPath;
                m_WebRequest = webRequest;
            }
        }

        static readonly string k_UVCSUrl = "cloud.plasticscm.com";
        const int k_MaxNumberOfFilesForAssetDownloadUrlFetch = 100;

        List<FileDownloadRequests> m_DownloadRequests = new();

        [SerializeReference]
        IAssetData m_AssetData;

        public DateTime StartTime;
        public string TempDownloadPath;
        public string DestinationPath;

        public IAssetData AssetData => m_AssetData;
        AssetData TypedAssetData => m_AssetData as AssetData; // keep this one private
        public IReadOnlyCollection<IFileDownload> DownloadRequests => m_DownloadRequests;

        public override AssetIdentifier Identifier => m_AssetData?.Identifier;
        public override string OperationName => $"Importing {m_AssetData?.Name}";
        public override string Description => DownloadRequests.Count > 0 ? "Downloading files..." : "Preparing download...";
        public override bool StartIndefinite => true;
        public override bool IsSticky => true;

        public override float Progress
        {
            get
            {
                var totalProgress = 0f;

                foreach (var download in m_DownloadRequests)
                {
                    totalProgress += Mathf.Max(download.WebRequest.downloadProgress, 0.0f); // Non started web request will return progress of -1
                }

                return m_DownloadRequests.Count > 0 ? totalProgress / m_DownloadRequests.Count : 0f;
            }
        }

        float m_LastReportedProgress = 0f;

        public ImportOperation(IAssetData assetData)
        {
            m_AssetData = assetData;
        }

        public async Task ImportAsync(CancellationToken token = default)
        {
            if (TypedAssetData == null)
                return;

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            var sourceDataset = await assetsProvider.GetSourceDatasetAsync(TypedAssetData, token);
            var sourceDatasetId = sourceDataset.Descriptor.DatasetId.ToString();

            var files = new List<IFile>();
            await foreach (var file in assetsProvider.ListFilesAsync(TypedAssetData, Range.All, token))
            {
                files.Add(file);
            }

            if (files.Count > k_MaxNumberOfFilesForAssetDownloadUrlFetch)
            {
                m_DownloadRequests = await FetchDownloadRequestsPerFile(files, sourceDatasetId, token);
            }
            else
            {
                m_DownloadRequests = await FetchDownloadRequests(assetsProvider, sourceDatasetId, token);
            }

            if (m_DownloadRequests == null || m_DownloadRequests.Count == 0)
            {
                Finish(OperationStatus.Error);
                throw new InvalidOperationException($"Nothing to download from asset '{TypedAssetData?.Name}'. Asset is empty, unavailable or corrupted.");
            }

            await StartDownloadRequests();
        }

        async Task<List<FileDownloadRequests>> FetchDownloadRequests(IAssetsProvider assetsProvider, string sourceDatasetId, CancellationToken token)
        {
            var downloadRequests = new List<FileDownloadRequests>();

            var urls = await assetsProvider.GetAssetDownloadUrlsAsync(TypedAssetData, token);

            foreach (var kvp in urls)
            {
                var url = kvp.Value;
                if (!url.ToString().Contains(sourceDatasetId) && !url.ToString().Contains(k_UVCSUrl))
                    continue;

                if (MetafilesHelper.IsOrphanMetafile(kvp.Key, urls.Keys))
                    continue;

                if (AssetDataDependencyHelper.IsASystemFile(kvp.Key))
                    continue;

                var tempPath = Path.Combine(TempDownloadPath, kvp.Key);

                downloadRequests.Add(new FileDownloadRequests(tempPath, CreateFileDownloadRequest(url.AbsoluteUri, tempPath)));
            }

            return downloadRequests;
        }

        async Task<List<FileDownloadRequests>> FetchDownloadRequestsPerFile(IReadOnlyList<IFile> files, string sourceDatasetId, CancellationToken token)
        {
            var downloadRequests = new List<FileDownloadRequests>();

            var fetchDownloadUrlsOperation = new FetchDownloadUrlsOperation();
            fetchDownloadUrlsOperation.Start();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var filepath = file.Descriptor.Path;

                fetchDownloadUrlsOperation.SetDescription(Path.GetFileName(filepath));

                if (MetafilesHelper.IsOrphanMetafile(filepath, files.Select(x => x.Descriptor.Path)))
                    continue;

                if (AssetDataDependencyHelper.IsASystemFile(filepath))
                    continue;

                var url = await file.GetDownloadUrlAsync(token);

                if (!url.ToString().Contains(sourceDatasetId) && !url.ToString().Contains(k_UVCSUrl))
                    continue;

                var tempPath = Path.Combine(TempDownloadPath, filepath);

                downloadRequests.Add(new FileDownloadRequests(tempPath, CreateFileDownloadRequest(url.AbsoluteUri, tempPath)));

                fetchDownloadUrlsOperation.SetProgress((float)i / files.Count);
            }

            fetchDownloadUrlsOperation.Finish(OperationStatus.Success);

            return downloadRequests;
        }

        static UnityWebRequest CreateFileDownloadRequest(string url, string path)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
                { disposeDownloadHandlerOnDispose = true };

            request.downloadHandler = new DownloadHandlerFile(path, false) { removeFileOnAbort = true };

            return request;
        }

        async Task StartDownloadRequests()
        {
            if (Status is not OperationStatus.InProgress)
                return;

            var operations = m_DownloadRequests.Select(r => r.WebRequest.SendWebRequest()).ToList();

            while (operations.Exists(x => !x.isDone))
            {
                if (Progress - m_LastReportedProgress > 0.01)
                {
                    m_LastReportedProgress = Progress;

                    Report();
                }

                await Task.Delay(200);
            }

            var status = m_DownloadRequests.Exists(x => !string.IsNullOrEmpty(x.WebRequest.error))
                ? OperationStatus.Error
                : OperationStatus.Success;

            Finish(status);
        }
    }
}
