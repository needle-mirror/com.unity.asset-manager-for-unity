using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class DownloadOperation
    {
        public ulong id;
        public string url;
        public string path;
        public long totalBytes;
        public float progress;
        public string error;
        public OperationStatus status;
    }
}
