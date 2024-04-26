using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class DownloadOperation : BaseOperation
    {
        public ulong Id;
        public string Url;
        public string Path;
        public long TotalBytes;
        public string Error;

        float m_Progress;

        public override float Progress => m_Progress;
        public override string OperationName => "Downloading";
        public override string Description => $"{System.IO.Path.GetFileName(Path)}";

        public void SetProgress(float progress)
        {
            m_Progress = progress;
            Report();
        }
    }
}
