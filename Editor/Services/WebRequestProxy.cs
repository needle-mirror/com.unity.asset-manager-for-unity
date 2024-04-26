using System;
using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    interface IWebRequestProxy : IService
    {
        IWebRequestItem SendWebRequest(string url, string path, bool append = false, string bytesRange = null);
    }

    class WebRequestProxy : BaseService<IWebRequestProxy>, IWebRequestProxy
    {
        public IWebRequestItem SendWebRequest(string url, string path, bool append = false, string bytesRange = null)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET) {disposeDownloadHandlerOnDispose = true};
            if (!string.IsNullOrWhiteSpace(bytesRange))
            {
                request.SetRequestHeader("Range", bytesRange);
            }

            request.downloadHandler = new DownloadHandlerFile(path, append) { removeFileOnAbort = true };
            request.SendWebRequest();
            return new WebRequestItem(request);
        }
    }
}
