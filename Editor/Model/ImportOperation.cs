using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportOperation : BaseOperation
    {
        public ImportAction importAction;
        public DateTime startTime;
        public string tempDownloadPath;
        public string destinationPath;

        [SerializeReference]
        public IAssetData assetData;

        public AssetIdentifier assetId => assetData?.identifier;

        public List<DownloadOperation> downloads = new();

        protected override string OperationName => $"Importing {assetData?.name}";
        protected override string Description => downloads.Count > 0 ? "Downloading files..." : "Preparing download...";
        protected override bool StartIndefinite => true;
        protected override bool IsSticky => true;

        public ImportOperation() : base(null)
        {
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
