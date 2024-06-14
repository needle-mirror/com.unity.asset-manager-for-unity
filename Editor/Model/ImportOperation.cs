using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
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

        static readonly string k_UVCSUrl = "cloud.plasticscm.com"; // TODO: Would probably need to be changed to support local servers
        const int k_MaxNumberOfFilesForAssetDownloadUrlFetch = 100;
        const int k_MaxConcurrentDownloads = 10;

        static readonly SemaphoreSlim k_DownloadFileSemaphore = new(k_MaxConcurrentDownloads);

        Dictionary<string, UnityWebRequest> m_DownloadRequests = new();

        [SerializeReference]
        IAssetData m_AssetData;

        public DateTime StartTime;
        public string TempDownloadPath;
        public string DestinationPath;

        public IAssetData AssetData => m_AssetData;
        AssetData TypedAssetData => m_AssetData as AssetData; // keep this one private
        public IReadOnlyDictionary<string, UnityWebRequest> DownloadRequests => m_DownloadRequests;

        public override AssetIdentifier Identifier => m_AssetData?.Identifier;
        public override string OperationName => $"Importing {m_AssetData?.Name}";
        public override string Description => DownloadRequests.Count > 0 ? "Downloading files..." : "Preparing download...";
        public override bool StartIndefinite => true;
        public override bool IsSticky => false;
        public override float Progress
        {
            get
            {
                var totalProgress = 0f;
                foreach (var download in DownloadRequests)
                {
                    totalProgress += Mathf.Max(download.Value.downloadProgress, 0.0f); // Non started web request will return progress of -1
                }

                return DownloadRequests.Count > 0 ? totalProgress / DownloadRequests.Count : 0f;
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

            await k_DownloadFileSemaphore.WaitAsync(token);
            try
            {
                var sourceDataset = await TypedAssetData.Asset.GetSourceDatasetAsync(token);
                var sourceDatasetId = sourceDataset.Descriptor.DatasetId.ToString();

                var files = new List<IFile>();
                await foreach (var file in TypedAssetData.Asset.ListFilesAsync(Range.All, token))
                {
                    files.Add(file);
                }

                if (files.Count > k_MaxNumberOfFilesForAssetDownloadUrlFetch)
                {
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
                        var request = new UnityWebRequest(url.ToString(), UnityWebRequest.kHttpVerbGET)
                            { disposeDownloadHandlerOnDispose = true };
                        request.downloadHandler = new DownloadHandlerFile(tempPath, false) { removeFileOnAbort = true };
                        m_DownloadRequests.Add(tempPath, request);

                        fetchDownloadUrlsOperation.SetProgress((float)i / files.Count);
                    }

                    fetchDownloadUrlsOperation.Finish(OperationStatus.Success);

                    StartImport();
                }
                else
                {
                    var urls = await TypedAssetData.Asset.GetAssetDownloadUrlsAsync(token);
                    foreach (var url in urls)
                    {
                        if ((sourceDatasetId == null || !url.ToString().Contains(sourceDatasetId)) && !url.ToString().Contains(k_UVCSUrl))
                            continue;

                        if (MetafilesHelper.IsOrphanMetafile(url.Key, urls.Keys))
                            continue;

                        if (AssetDataDependencyHelper.IsASystemFile(url.Key))
                            continue;

                        var tempPath = Path.Combine(TempDownloadPath, url.Key);
                        var request = new UnityWebRequest(url.Value.ToString(), UnityWebRequest.kHttpVerbGET)
                            { disposeDownloadHandlerOnDispose = true };
                        request.downloadHandler = new DownloadHandlerFile(tempPath, false) { removeFileOnAbort = true };
                        m_DownloadRequests.Add(tempPath, request);
                    }

                    StartImport();
                }
            }
            finally
            {
                k_DownloadFileSemaphore.Release();
            }
        }

        void StartImport()
        {
            EditorApplication.update += Update;

            Start();

            foreach (var downloadRequest in m_DownloadRequests)
            {
                downloadRequest.Value.SendWebRequest();
            }
        }

        void Update()
        {
            if (Progress - m_LastReportedProgress > 0.01)
            {
                m_LastReportedProgress = Progress;

                Report();
            }

            if (DownloadRequests.Any(x => !string.IsNullOrEmpty(x.Value.error)))
            {
                FinishImport(OperationStatus.Error);
            }
            else if (DownloadRequests.All(x => x.Value.isDone))
            {
                FinishImport(OperationStatus.Success);
            }
        }

        void FinishImport(OperationStatus status)
        {
            Finish(status);

            EditorApplication.update -= Update;
        }
    }
}
