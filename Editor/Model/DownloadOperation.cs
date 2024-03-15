using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class DownloadOperation : BaseOperation
    {
        public ulong id;
        public string url;
        public string path;
        public long totalBytes;
        public string error;

        float m_Progress;

        public override float Progress => m_Progress;
        public override string OperationName => "Downloading";
        public override string Description => $"{System.IO.Path.GetFileName(path)}";

        public void SetProgress(float progress)
        {
            m_Progress = progress;
            Report();
        }
    }
}
