using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    internal interface IWebRequestItem
    {
        bool isDone { get; }
        float downloadProgress { get; }
        string error { get; }
        UnityWebRequest.Result result { get; }

        void Abort();
        void Dispose();
    }

    internal class WebRequestItem : IWebRequestItem
    {
        private readonly UnityWebRequest m_UnityWebRequest;
        public WebRequestItem(UnityWebRequest unityWebRequest)
        {
            m_UnityWebRequest = unityWebRequest;
        }

        public bool isDone => m_UnityWebRequest.isDone;
        public float downloadProgress => m_UnityWebRequest.downloadProgress;
        public string error => m_UnityWebRequest.error;
        public UnityWebRequest.Result result => m_UnityWebRequest.result;
        public void Abort() => m_UnityWebRequest.Abort();
        public void Dispose() => m_UnityWebRequest.Dispose();
    }
}
