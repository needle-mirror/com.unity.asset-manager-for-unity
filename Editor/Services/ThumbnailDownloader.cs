using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    interface IThumbnailDownloader : IService
    {
        void DownloadThumbnail(AssetIdentifier identifier, string url,
            Action<AssetIdentifier, Texture2D> doneCallbackAction = null);

        Texture2D GetCachedThumbnail(string thumbnailUrl);
        Texture2D GetCachedThumbnail(AssetIdentifier identifier);
    }

    [Serializable]
    class ThumbnailDownloader : BaseService<IThumbnailDownloader>, IThumbnailDownloader, ISerializationCallbackReceiver
    {
        const string k_TempExt = ".tmp";

        [SerializeField]
        string[] m_SerializedThumbnailsKeys;

        [SerializeField]
        Texture2D[] m_SerializedThumbnails;

        [SerializeField]
        AssetIdentifier[] m_SerializedThumbnailUrlsKeys;

        [SerializeField]
        string[] m_SerializedThumbnailUrls;


        [SerializeReference]
        ICacheEvictionManager m_CacheEvictionManager;

        [SerializeReference]
        IDownloadManager m_DownloadManager;

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        readonly Dictionary<string, AssetIdentifier> m_DownloadIdToAssetIdMap = new();
        readonly Dictionary<string, List<Action<AssetIdentifier, Texture2D>>> m_ThumbnailDownloadCallbacks = new();
        readonly Dictionary<string, Texture2D> m_Thumbnails = new();
        readonly Dictionary<AssetIdentifier, string> m_ThumbnailUrls = new();

        [ServiceInjection]
        public void Inject(IDownloadManager downloadManager, IIOProxy ioProxy, ISettingsManager settingsManager,
            ICacheEvictionManager cacheEvictionManager)
        {
            m_DownloadManager = downloadManager;
            m_IOProxy = ioProxy;
            m_SettingsManager = settingsManager;
            m_CacheEvictionManager = cacheEvictionManager;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedThumbnailsKeys = m_Thumbnails.Keys.ToArray();
            m_SerializedThumbnails = m_Thumbnails.Values.ToArray();

            m_SerializedThumbnailUrlsKeys = m_ThumbnailUrls.Keys.ToArray();
            m_SerializedThumbnailUrls = m_ThumbnailUrls.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            for (var i = 0; i < m_SerializedThumbnailsKeys.Length; i++)
            {
                m_Thumbnails[m_SerializedThumbnailsKeys[i]] = m_SerializedThumbnails[i];
            }

            for(var i=0; i < m_SerializedThumbnailUrlsKeys.Length; i++)
            {
                m_ThumbnailUrls[m_SerializedThumbnailUrlsKeys[i]] = m_SerializedThumbnailUrls[i];
            }
        }

        public void DownloadThumbnail(AssetIdentifier identifier, string url, Action<AssetIdentifier, Texture2D> doneCallbackAction = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                doneCallbackAction?.Invoke(identifier, null);
                return;
            }

            m_ThumbnailUrls[identifier] = url;

            var thumbnailFileName = Hash128.Compute(url).ToString();

            var thumbnail = LoadThumbnail(url,
                Path.Combine(m_SettingsManager.ThumbnailsCacheLocation, thumbnailFileName));

            if (thumbnail != null)
            {
                doneCallbackAction?.Invoke(identifier, thumbnail);
                return;
            }

            if (m_ThumbnailDownloadCallbacks.TryGetValue(url, out var callbacks))
            {
                callbacks.Add(doneCallbackAction);
                return;
            }

            m_DownloadIdToAssetIdMap[url] = identifier;

            DownloadThumbnail(url, true);

            var newCallbacks = new List<Action<AssetIdentifier, Texture2D>>();
            if (doneCallbackAction != null)
            {
                newCallbacks.Add(doneCallbackAction);
            }

            m_ThumbnailDownloadCallbacks[url] = newCallbacks;
        }

        void DownloadThumbnail(string url, bool resize)
        {
            var finaleUrl = resize ? $"https://transformation.unity.com/api/images?url={Uri.EscapeDataString(url)}&width={180}" : url;

            var unityWebRequest = new UnityWebRequest(finaleUrl, UnityWebRequest.kHttpVerbGET) { disposeDownloadHandlerOnDispose = true };
            unityWebRequest.downloadHandler = new DownloadHandlerTexture();

            var webRequestAsyncOperation = unityWebRequest.SendWebRequest();
            webRequestAsyncOperation.completed += asyncOp =>
            {
                var webOperation = (UnityWebRequestAsyncOperation)asyncOp;
                if (resize && webOperation.webRequest.result != UnityWebRequest.Result.Success)
                {
                    Utilities.DevLogWarning($"Resizing thumbnail failed, trying regular thumbnail using '{url}'");

                    // Try again without resizing
                    DownloadThumbnail(url, false);
                }
                else
                {
                    OnRequestCompletion(webOperation, url);
                }
            };
        }

        public Texture2D GetCachedThumbnail(string thumbnailUrl)
        {
            return m_Thumbnails.TryGetValue(thumbnailUrl, out var result) ? result : null;
        }

        public Texture2D GetCachedThumbnail(AssetIdentifier identifier)
        {
            if (!m_ThumbnailUrls.TryGetValue(identifier, out var url))
            {
                return null;
            }

            return GetCachedThumbnail(url);
        }

        void OnRequestCompletion(UnityWebRequestAsyncOperation asyncOperation, string thumbnailUrl)
        {
            if (!m_DownloadIdToAssetIdMap.TryGetValue(thumbnailUrl, out var assetId))
                return;

            m_DownloadIdToAssetIdMap.Remove(thumbnailUrl);
            if (!m_ThumbnailDownloadCallbacks.TryGetValue(thumbnailUrl, out var callbacks) || callbacks.Count == 0)
                return;

            m_ThumbnailDownloadCallbacks.Remove(thumbnailUrl);

            Texture2D thumbnail = null;

            if (asyncOperation.webRequest.result == UnityWebRequest.Result.Success)
            {
                thumbnail = DownloadHandlerTexture.GetContent(asyncOperation.webRequest);
            }
            else
            {
                Utilities.DevLogError("Unable to download thumbnail. Error: " + asyncOperation.webRequest.error);
            }

            foreach (var callback in callbacks)
            {
                callback?.Invoke(assetId, thumbnail);
            }

            SaveThumbnailInCache(thumbnail, thumbnailUrl);
        }

        void SaveThumbnailInCache(Texture2D texture, string url)
        {
            if (texture == null)
                return;

            var thumbnailFileName = Hash128.Compute(url).ToString();
            var finalPath = Path.Combine(m_SettingsManager.ThumbnailsCacheLocation, thumbnailFileName);
            var bytes = texture.EncodeToPNG();
            Task.Run(() => File.WriteAllBytes(finalPath, bytes));
        }

        Texture2D LoadThumbnail(string url, string thumbnailPath)
        {
            if (m_Thumbnails.TryGetValue(url, out var result))
            {
                return result;
            }

            if (!m_IOProxy.FileExists(thumbnailPath))
            {
                return null;
            }

            var texture2D = new Texture2D(1, 1);
            texture2D.LoadImage(File.ReadAllBytes(thumbnailPath));
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            m_Thumbnails[url] = texture2D;

            return texture2D;
        }
    }
}
