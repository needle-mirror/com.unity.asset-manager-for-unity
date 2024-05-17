using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IThumbnailDownloader : IService
    {
        void DownloadThumbnail(AssetIdentifier identifier, string thumbnailUrl,
            Action<AssetIdentifier, Texture2D> doneCallbackAction = null);

        Texture2D GetCachedThumbnail(string thumbnailUrl);
    }

    [Serializable]
    class ThumbnailDownloader : BaseService<IThumbnailDownloader>, IThumbnailDownloader, ISerializationCallbackReceiver
    {
        const string k_TempExt = ".tmp";

        [SerializeField]
        string[] m_SerializedThumbnailsKeys;

        [SerializeField]
        Texture2D[] m_SerializedThumbnails;

        [SerializeReference]
        ICacheEvictionManager m_CacheEvictionManager;

        [SerializeReference]
        IDownloadManager m_DownloadManager;

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        readonly Dictionary<ulong, AssetIdentifier> m_DownloadIdToAssetIdMap = new();
        readonly Dictionary<string, List<Action<AssetIdentifier, Texture2D>>> m_ThumbnailDownloadCallbacks = new();
        readonly Dictionary<string, Texture2D> m_Thumbnails = new();

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
        }

        public void OnAfterDeserialize()
        {
            for (var i = 0; i < m_SerializedThumbnailsKeys.Length; i++)
            {
                m_Thumbnails[m_SerializedThumbnailsKeys[i]] = m_SerializedThumbnails[i];
            }
        }

        public void DownloadThumbnail(AssetIdentifier identifier, string thumbnailUrl, Action<AssetIdentifier, Texture2D> doneCallbackAction = null)
        {
            if (string.IsNullOrEmpty(thumbnailUrl))
            {
                doneCallbackAction?.Invoke(identifier, null);
                return;
            }

            var thumbnailFileName = Hash128.Compute(thumbnailUrl).ToString();
            var thumbnail = LoadThumbnail(thumbnailUrl,
                Path.Combine(m_SettingsManager.ThumbnailsCacheLocation, thumbnailFileName));
            if (thumbnail != null)
            {
                doneCallbackAction?.Invoke(identifier, thumbnail);
                return;
            }

            if (m_ThumbnailDownloadCallbacks.TryGetValue(thumbnailUrl, out var callbacks))
            {
                callbacks.Add(doneCallbackAction);
                return;
            }

            var download = m_DownloadManager.CreateDownloadOperation(thumbnailUrl,
                Path.Combine(m_SettingsManager.ThumbnailsCacheLocation, thumbnailFileName + k_TempExt));
            m_DownloadManager.StartDownload(download);
            m_DownloadIdToAssetIdMap[download.Id] = identifier;
            var newCallbacks = new List<Action<AssetIdentifier, Texture2D>>();
            if (doneCallbackAction != null)
            {
                newCallbacks.Add(doneCallbackAction);
            }

            m_ThumbnailDownloadCallbacks[thumbnailUrl] = newCallbacks;
        }

        public Texture2D GetCachedThumbnail(string thumbnailUrl)
        {
            return m_Thumbnails.TryGetValue(thumbnailUrl, out var result) ? result : null;
        }

        public override void OnEnable()
        {
            m_DownloadManager.DownloadFinalized += OnDownloadFinalized;
        }

        public override void OnDisable()
        {
            m_DownloadManager.DownloadFinalized -= OnDownloadFinalized;
        }

        void OnDownloadFinalized(DownloadOperation operation)
        {
            if (!m_DownloadIdToAssetIdMap.TryGetValue(operation.Id, out var assetId))
                return;

            m_DownloadIdToAssetIdMap.Remove(operation.Id);
            if (!m_ThumbnailDownloadCallbacks.TryGetValue(operation.Url, out var callbacks) || callbacks.Count == 0)
                return;

            var finalPath = operation.Path.Substring(0, operation.Path.Length - k_TempExt.Length);
            m_IOProxy.DeleteFileIfExists(finalPath);
            m_IOProxy.FileMove(operation.Path, finalPath);

            var thumbnail = LoadThumbnail(operation.Url, finalPath);
            m_CacheEvictionManager.OnCheckEvictConditions(finalPath);
            m_ThumbnailDownloadCallbacks.Remove(operation.Url);
            foreach (var callback in callbacks)
            {
                callback?.Invoke(assetId, thumbnail);
            }
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
