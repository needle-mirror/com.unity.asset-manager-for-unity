using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Core.Editor
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

        List<FileDownloadRequests> m_DownloadRequests = new();

        [SerializeReference]
        BaseAssetData m_AssetData;

        public DateTime StartTime;
        public string TempDownloadPath;
        public string DestinationPath;

        public BaseAssetData AssetData => m_AssetData;
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

        public override void Finish(OperationStatus status)
        {
            base.Finish(status);

            if (status == OperationStatus.Success)
            {
                Remove();
            }
        }

        float m_LastReportedProgress = 0f;

        public ImportOperation(BaseAssetData assetData)
        {
            m_AssetData = assetData;
        }

        public async Task ImportAsync(CancellationToken token = default)
        {
            if (TypedAssetData == null)
                return;

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            var fetchDownloadUrlsOperation = new FetchDownloadUrlsOperation();
            fetchDownloadUrlsOperation.Start();

            try
            {
                var downloadUrls = await assetsProvider.GetAssetDownloadUrlsAsync(TypedAssetData, fetchDownloadUrlsOperation, token);

                fetchDownloadUrlsOperation.Finish(OperationStatus.Success);

                foreach (var (filePath, url) in downloadUrls)
                {
                    var tempPath = Path.Combine(TempDownloadPath, filePath);
                    m_DownloadRequests.Add(new FileDownloadRequests(tempPath, CreateFileDownloadRequest(url.AbsoluteUri, tempPath)));
                }
            }
            catch(TaskCanceledException)
            {
                fetchDownloadUrlsOperation.Finish(OperationStatus.None);
                throw;
            }

            if (m_DownloadRequests.Count == 0)
            {
                throw new InvalidOperationException($"Nothing to download from asset '{TypedAssetData?.Name}'. Asset is empty, unavailable or corrupted.");
            }

            await StartDownloadRequests();
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
