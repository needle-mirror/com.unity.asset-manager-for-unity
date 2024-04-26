using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class ImportOperation : AssetDataOperation
    {
        [SerializeField]
        List<DownloadOperation> m_Downloads = new();

        [SerializeReference]
        IAssetData m_AssetData;

        public DateTime StartTime;
        public string TempDownloadPath;
        public string DestinationPath;

        public IAssetData AssetData => m_AssetData;
        public IReadOnlyCollection<DownloadOperation> Downloads => m_Downloads;

        public override AssetIdentifier AssetId => m_AssetData?.Identifier;
        public override string OperationName => $"Importing {m_AssetData?.Name}";
        public override string Description => Downloads.Count > 0 ? "Downloading files..." : "Preparing download...";
        public override bool StartIndefinite => true;
        public override bool IsSticky => false;
        public override float Progress
        {
            get
            {
                var totalProgress = 0f;
                foreach (var download in Downloads)
                {
                    totalProgress += download.Progress;
                }

                return Downloads.Count > 0 ? totalProgress / Downloads.Count : 0f;
            }
        }

        public ImportOperation(IAssetData assetData)
        {
            m_AssetData = assetData;
        }

        public void OnDownloadProgress(float progress)
        {
            Report();
        }

        public void OnDownloadCompleted(OperationStatus status, DownloadOperation downloadOperation)
        {
            if (Downloads.All(x => x.Status == OperationStatus.Success) || status == OperationStatus.Cancelled ||
                status == OperationStatus.Error)
            {
                Finish(status);
            }
        }

        public void AddDownload(DownloadOperation downloadOperation)
        {
            m_Downloads.Add(downloadOperation);
            downloadOperation.ProgressChanged += OnDownloadProgress;
            downloadOperation.Finished += status => OnDownloadCompleted(status, downloadOperation);
        }
    }
}
