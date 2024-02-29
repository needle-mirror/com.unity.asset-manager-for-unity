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
        protected override string OperationName => "Downloading";
        protected override string Description => $"{System.IO.Path.GetFileName(path)}";

        public DownloadOperation(BaseOperation parent) : base(parent)
        {
        }

        public void SetProgress(float progress)
        {
            m_Progress = progress;
            Report();
        }
    }
}
