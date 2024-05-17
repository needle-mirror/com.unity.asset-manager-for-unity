using System;
using System.IO;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEditor.SettingsManagement;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface ISettingsManager : IService
    {
        string DefaultImportLocation { get; }
        bool IsSubfolderCreationEnabled { get; }
        string BaseCacheLocation { get; }
        string ThumbnailsCacheLocation { get; }
        int MaxCacheSizeGb { get; }
        int MaxCacheSizeMb { get; }

        event Action DefaultImportLocationChanged;
        event Action CacheLocationChanged;
        event Action CacheSizeChanged;

        void SetDefaultImportLocation(string importLocation);
        void SetIsSubfolderCreationEnabled(bool value);
        void SetCacheLocation(string cacheLocation);
        void SetMaxCacheSize(int cacheSize);
    }

    class AssetManagerSettingsManager : BaseService<ISettingsManager>, ISettingsManager
    {
        [SerializeReference]
        ICachePathHelper m_CachePathHelper;

        const string k_DefaultImportLocationKey = "defaultImportLocation";
        const string k_IsSubfolderCreationEnabledKey = "isSubfolderCreationEnabled";
        const string k_CacheLocationKey = "cacheLocation";
        const string k_MaxCacheSizeKey = "cacheSize";
        const string k_TexturesCacheLocation = "texturesCaheLocation";
        const string k_ThumbnailsCacheLocation = "thumbnailsCaheLocation";
        const string k_AssetManagerCacheLocation = "assetManagerCacheLocation";

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

        public event Action DefaultImportLocationChanged = delegate { };
        public event Action CacheLocationChanged = delegate { };
        public event Action CacheSizeChanged = delegate { };

        public string DefaultImportLocation
        {
            get
            {
                var defaultImportLocation = Instance.Get<string>(k_DefaultImportLocationKey, SettingsScope.User);
                if (Directory.Exists(defaultImportLocation))
                {
                    return defaultImportLocation;
                }

                // if the directory doesn't exist
                var defaultPath = Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName);
                SetDefaultImportLocation(defaultPath);

                return defaultPath;
            }
        }

        public bool IsSubfolderCreationEnabled => Instance.Get<bool>(k_IsSubfolderCreationEnabledKey, SettingsScope.User, true);

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

        [ServiceInjection]
        public void Inject(ICachePathHelper cachePathHelper)
        {
            m_CachePathHelper = cachePathHelper;
        }

        public void SetDefaultImportLocation(string importLocation)
        {
            if (string.IsNullOrEmpty(importLocation))
            {
                importLocation = Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName);
            }

            Instance.Set(k_DefaultImportLocationKey, importLocation, SettingsScope.User);
            DefaultImportLocationChanged?.Invoke();
        }

        public void SetIsSubfolderCreationEnabled(bool value)
        {
            Instance.Set(k_IsSubfolderCreationEnabledKey, value, SettingsScope.User);
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
            CacheLocationChanged?.Invoke();
        }

        public void SetMaxCacheSize(int cacheSize)
        {
            if (cacheSize <= 0)
            {
                return;
            }

            Instance.Set(k_MaxCacheSizeKey, cacheSize, SettingsScope.User);
            CacheSizeChanged?.Invoke();
        }

        string GetCacheLocationOrDefault(string cachePath)
        {
            var validationResult = m_CachePathHelper.EnsureBaseCacheLocation(cachePath);

            if (validationResult.Success)
            {
                return cachePath;
            }

            // Provided path is not valid, set default
            var defaultLocation = m_CachePathHelper.GetDefaultCacheLocation();
            SetCacheLocation(defaultLocation.FullName);
            return Path.Combine(defaultLocation.FullName, Constants.CacheTexturesFolderName);
        }
    }
}
