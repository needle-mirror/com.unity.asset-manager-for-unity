using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportOperation
    {
        public ImportAction importAction;
        public long startTimeTicks;
        public string tempDownloadPath;
        public string destinationPath;
        
        [SerializeReference]
        public IAssetData assetData;
        
        public AssetIdentifier assetId => assetData?.identifier;
        
        public List<DownloadOperation> downloads = new();
        public OperationStatus status;

        public float progress
        {
            get
            {
                var totalBytes = 0L;
                var downloadedBytes = 0.0f;
                foreach (var download in downloads)
                {
                    totalBytes += download.totalBytes;
                    downloadedBytes += download.progress * download.totalBytes;
                }
                return totalBytes > 0 ? downloadedBytes / totalBytes : 0;
            }
        }

        public void UpdateDownloadOperation(DownloadOperation downloadOperation)
        {
            for (var i = 0; i < downloads.Count; i++)
            {
                if (downloads[i].id != downloadOperation.id)
                    continue;

                downloads[i] = downloadOperation;
                return;
            }
        }
    }
}

