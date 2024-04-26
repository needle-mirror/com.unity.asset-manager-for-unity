using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    class FetchDownloadUrlsOperation : BaseOperation
    {
        public override string OperationName => "Fetching download URLs";
        public override string Description => m_Description;
        public override float Progress => m_Progress;

        string m_Description;
        float m_Progress;

        public void SetDescription(string description)
        {
            m_Description = description;
        }
        
        public void SetProgress(float progress)
        {
            m_Progress = progress;
            
            Report();
        }
    }
}