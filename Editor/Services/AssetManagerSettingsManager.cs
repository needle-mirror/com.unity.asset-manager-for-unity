using System;
using System.IO;
using UnityEditor;
using UnityEditor.SettingsManagement;

namespace Unity.AssetManager.Editor
{
    internal interface ISettingsManager : IService
    {
        event Action onCacheLocationChanged;
        event Action onCacheSizeChanged;

        string BaseCacheLocation { get; }
        string ThumbnailsCacheLocation { get; }

        int MaxCacheSizeGb { get; }
        int MaxCacheSizeMb { get; }
        void SetCacheLocation(string cacheLocation);
        void SetMaxCacheSize(int cacheSize);
    }

    internal class AssetManagerSettingsManager : BaseService<ISettingsManager>, ISettingsManager
    {
        const string k_CacheLocationKey = "cacheLocation";
        const string k_MaxCacheSizeKey = "cacheSize";
        const string k_TexturesCacheLocation = "texturesCaheLocation";
        const string k_ThumbnailsCacheLocation = "thumbnailsCaheLocation";
        const string k_AssetManagerCacheLocation = "assetManagerCacheLocation";
        public event Action onCacheLocationChanged = delegate { };
        public event Action onCacheSizeChanged = delegate { };

        public string BaseCacheLocation
        {
            get
            {
                var cacheLocation = Instance.Get<string>(k_CacheLocationKey, SettingsScope.User);
                return GetCacheLocationOrDefault(cacheLocation);
            }
        }

        public string ThumbnailsCacheLocation
        {
            get
            {
                var thumbnailsCacheLocation = Instance.Get<string>(k_ThumbnailsCacheLocation, SettingsScope.User);
                return GetCacheLocationOrDefault(thumbnailsCacheLocation);
            }
        }

        private readonly ICachePathHelper m_CachePathHelper;

        internal AssetManagerSettingsManager(ICachePathHelper cachePathHelper)
        {
            m_CachePathHelper = cachePathHelper;
        }

        private string GetCacheLocationOrDefault(string cachePath)
        {
            var validationResult = m_CachePathHelper.EnsureBaseCacheLocation(cachePath);
            
            if (validationResult.success)
            {
                return cachePath;
            }

            // Provided path is not valid, set default
            var defaultLocation = m_CachePathHelper.GetDefaultCacheLocation();
            SetCacheLocation(defaultLocation.FullName);
            return Path.Combine(defaultLocation.FullName, Constants.CacheTexturesFolderName);
        }

        public int MaxCacheSizeGb
        {
            get
            {
                var cacheSize = Instance.Get<int>(k_MaxCacheSizeKey, SettingsScope.User);
                if (cacheSize > 0)
                {
                    return cacheSize;
                }

                // if the cache size is lower than 0
                SetMaxCacheSize(Constants.DefaultCacheSizeGb);
                return Constants.DefaultCacheSizeGb;
            }
        }

        public int MaxCacheSizeMb => MaxCacheSizeGb * 1024;

        Settings m_Settings;

        internal Settings Instance
        {
            get
            {
                if (m_Settings == null)
                {
                    m_Settings = new Settings(Constants.PackageName);
                }

                return m_Settings;
            }
        }

        public void SetCacheLocation(string cacheLocation)
        {
            if (string.IsNullOrEmpty(cacheLocation))
            {
                cacheLocation = m_CachePathHelper.GetDefaultCacheLocation().FullName;
            }
            Instance.Set(k_CacheLocationKey, cacheLocation, SettingsScope.User);
            var assetManagerCacheLocation = m_CachePathHelper.CreateAssetManagerCacheLocation(cacheLocation);
            Instance.Set(k_AssetManagerCacheLocation, m_CachePathHelper.CreateAssetManagerCacheLocation(cacheLocation),
                SettingsScope.User);
            Instance.Set(k_ThumbnailsCacheLocation,
                Path.Combine(assetManagerCacheLocation, Constants.CacheThumbnailsFolderName), SettingsScope.User);
            Instance.Set(k_TexturesCacheLocation,
                Path.Combine(assetManagerCacheLocation, Constants.CacheTexturesFolderName), SettingsScope.User);
            onCacheLocationChanged?.Invoke();
        }

        public void SetMaxCacheSize(int cacheSize)
        {
            if (cacheSize <= 0)
            {
                return;
            }

            Instance.Set(k_MaxCacheSizeKey, cacheSize, SettingsScope.User);
            onCacheSizeChanged?.Invoke();
        }
    }
}
