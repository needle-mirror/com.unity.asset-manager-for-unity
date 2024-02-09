using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IProjectIconDownloader : IService
    {
        void DownloadIcon(string projectId, Action<string, Texture2D> doneCallbackAction = null);
    }

    [Serializable]
    class AllProjectResponse
    {
        public List<AllProjectResponseItem> results { get; set; } = new();
    }

    [Serializable]
    class AllProjectResponseItem
    {
        public string id { get; set; } = string.Empty;
        public string iconUrl { get; set; } = string.Empty;
    }

    [Serializable]
    internal class ProjectIconDownloader : BaseService<IProjectIconDownloader>, IProjectIconDownloader, ISerializationCallbackReceiver
    {
        [SerializeField]
        string[] m_SerializedIconsKeys;

        [SerializeField]
        Texture2D[] m_SerializedIcons;

        static readonly string k_ProjectIconCacheLocation = "ProjectIconCache";
        const string k_TempExt = ".tmp";

        readonly Dictionary<ulong, string> m_DownloadIdToProjectIdMap = new();
        readonly Dictionary<string, List<Action<string, Texture2D>>> m_IconDownloadCallbacks = new();
        readonly Dictionary<string, Texture2D> m_Icons = new();
        readonly Dictionary<string, string> m_IconsUrls = new();

        readonly IDownloadManager m_DownloadManager;
        readonly IIOProxy m_IOProxy;
        readonly ISettingsManager m_SettingsManager;
        readonly ICacheEvictionManager m_CacheEvictionManager;
        readonly IAssetsProvider m_AssetsProvider;

        public ProjectIconDownloader(IDownloadManager downloadManager, IIOProxy ioProxy, ISettingsManager settingsManager, ICacheEvictionManager cacheEvictionManager, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
        {
            m_DownloadManager = RegisterDependency(downloadManager);
            m_IOProxy = RegisterDependency(ioProxy);
            m_SettingsManager = RegisterDependency(settingsManager);
            m_CacheEvictionManager = RegisterDependency(cacheEvictionManager);
            m_AssetsProvider = RegisterDependency(assetsProvider);
            projectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
        }

        public override void OnEnable()
        {
            m_DownloadManager.onDownloadFinalized += DownloadFinalized;
        }

        public override void OnDisable()
        {
            m_DownloadManager.onDownloadFinalized -= DownloadFinalized;
        }

        void DownloadFinalized(DownloadOperation operation)
        {
            if (!m_DownloadIdToProjectIdMap.TryGetValue(operation.id, out var projectId))
                return;

            m_DownloadIdToProjectIdMap.Remove(operation.id);
            if (!m_IconDownloadCallbacks.TryGetValue(operation.url, out var callbacks) || callbacks.Count == 0)
                return;

            var finalPath = operation.path.Substring(0, operation.path.Length - k_TempExt.Length);
            m_IOProxy.DeleteFileIfExists(finalPath);
            m_IOProxy.FileMove(operation.path, finalPath);

            var icon = LoadIcon(operation.url, finalPath);
            m_CacheEvictionManager.CheckEvictConditions(finalPath);
            m_IconDownloadCallbacks.Remove(operation.url);
            foreach (var callback in callbacks)
            {
                callback?.Invoke(projectId, icon);
            }
        }

        public void DownloadIcon(string projectId, Action<string, Texture2D> doneCallbackAction = null)
        {
            if (!m_IconsUrls.TryGetValue(projectId, out var iconUrl))
            {
                doneCallbackAction?.Invoke(projectId, null);
                return;
            }

            var iconFileName = Hash128.Compute(iconUrl).ToString();
            var icon = LoadIcon(iconUrl, Path.Combine(k_ProjectIconCacheLocation, iconFileName));
            if (icon != null)
            {
                doneCallbackAction?.Invoke(projectId, icon);
                return;
            }

            if (m_IconDownloadCallbacks.TryGetValue(iconUrl, out var callbacks))
            {
                callbacks.Add(doneCallbackAction);
                return;
            }

            var download = m_DownloadManager.StartDownload(iconUrl, Path.Combine(m_SettingsManager.ThumbnailsCacheLocation, iconFileName + k_TempExt));
            m_DownloadIdToProjectIdMap[download.id] = projectId;
            var newCallbacks = new List<Action<string, Texture2D>>();
            if (doneCallbackAction != null)
            {
                newCallbacks.Add(doneCallbackAction);
            }

            m_IconDownloadCallbacks[iconUrl] = newCallbacks;
        }

        Texture2D LoadIcon(string url, string iconPath)
        {
            if (m_Icons.TryGetValue(url, out var result))
                return result;

            if (!m_IOProxy.FileExists(iconPath))
                return null;

            var texture2D = new Texture2D(1, 1);
            texture2D.LoadImage(File.ReadAllBytes(iconPath));
            texture2D.hideFlags = HideFlags.HideAndDontSave;
            m_Icons[url] = texture2D;
            return texture2D;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedIconsKeys = m_Icons.Keys.ToArray();
            m_SerializedIcons = m_Icons.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            for (var i = 0; i < m_SerializedIconsKeys.Length; i++)
            {
                m_Icons[m_SerializedIconsKeys[i]] = m_SerializedIcons[i];
            }
        }

        async void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organizationInfo, bool isLoading)
        {
            if (organizationInfo != null)
            {
                var iconsUrls = await m_AssetsProvider.GetProjectIconUrlsAsync(organizationInfo.id, CancellationToken.None);
                foreach (var iconUrl in iconsUrls)
                {
                    m_IconsUrls[iconUrl.Key] = iconUrl.Value;
                }
            }
        }

        static readonly Color[] k_ProjectIconDefaultColors =
        {
            new Color32(233, 61, 130, 255), // Crimson
            new Color32(247, 107, 21, 255), // Orange
            new Color32(255, 166, 0, 255), // Amber
            new Color32(18, 165, 148, 255), // Teal
            new Color32(62, 99, 221, 255), // Indigo
            new Color32(110, 86, 207, 255), // Violet
        };

        public static readonly Color DefaultColor = new (40f/ 255f, 40f / 255f, 40f / 255f);

        public static Color GetProjectIconColor(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return k_ProjectIconDefaultColors[0];
            }

            var lastCharIndex = projectId.Length - 1;
            var lastCharCode = projectId[lastCharIndex];
            var colorIndex = lastCharCode % k_ProjectIconDefaultColors.Length;

            return k_ProjectIconDefaultColors[colorIndex];
        }
    }
}
