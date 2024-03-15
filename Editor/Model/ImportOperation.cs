using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportOperation : AssetDataOperation
    {
        public DateTime startTime;
        public string tempDownloadPath;
        public string destinationPath;

        [SerializeReference]
        IAssetData m_AssetData;

        public IAssetData assetData => m_AssetData;

        public override AssetIdentifier AssetId => m_AssetData?.identifier;

        [SerializeField]
        List<DownloadOperation> m_Downloads = new();

        public IReadOnlyCollection<DownloadOperation> downloads => m_Downloads;

        public override string OperationName => $"Importing {m_AssetData?.name}";
        public override string Description => downloads.Count > 0 ? "Downloading files..." : "Preparing download...";
        public override bool StartIndefinite => true;
        public override bool IsSticky => false;

        public ImportOperation(IAssetData assetData)
        {
            m_AssetData = assetData;
        }

        public override float Progress
        {
            get
            {
                var totalBytes = 0L;
                var downloadedBytes = 0.0f;
                foreach (var download in downloads)
                {
                    totalBytes += download.totalBytes;
                    downloadedBytes += download.Progress * download.totalBytes;
                }

                return totalBytes > 0 ? downloadedBytes / totalBytes : 0;
            }
        }

        public void UpdateDownloadOperation(DownloadOperation downloadOperation)
        {
            for (var i = 0; i < m_Downloads.Count; i++)
            {
                if (m_Downloads[i].id != downloadOperation.id)
                    continue;

                m_Downloads[i] = downloadOperation;
                return;
            }
        }

        public void AddDownload(DownloadOperation downloadOperation)
        {
            m_Downloads.Add(downloadOperation);
        }
    }
}
