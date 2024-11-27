using System.IO;
using UnityEditor;
using UnityEditor.SettingsManagement;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface ISettingsManager : IService
    {
        string DefaultImportLocation { get; set; }
        bool IsSubfolderCreationEnabled { get; }
        string BaseCacheLocation { get; }
        string ThumbnailsCacheLocation { get; }
        int MaxCacheSizeGb { get; }
        int MaxCacheSizeMb { get; }
        bool IsTagsCreationUploadEnabled { get; }
        int TagsConfidenceThresholdPercent { get; }
        float TagsConfidenceThreshold { get; }

        void SetIsSubfolderCreationEnabled(bool value);
        void SetCacheLocation(string cacheLocation);
        void SetMaxCacheSize(int cacheSize);
        void SetIsTagsCreationUploadEnabled(bool value);
        void SetTagsCreationConfidenceThresholdPercent(int value);

        string ResetCacheLocation();
        string ResetImportLocation();
    }

    class AssetManagerSettingsManager : BaseService<ISettingsManager>, ISettingsManager
    {
        [SerializeReference]
        ICachePathHelper m_CachePathHelper;

        const string k_DefaultImportLocationKey = "AM4U.defaultImportLocation";
        const string k_IsSubfolderCreationEnabledKey = "AM4U.isSubfolderCreationEnabled";
        const string k_IsTagsCreationUploadEnabledKey = "AM4U.isTagsCreationUploadEnabled";
        const string k_CacheLocationKey = "AM4U.cacheLocation";
        const string k_MaxCacheSizeKey = "AM4U.cacheSize";
        const string k_TexturesCacheLocationKey = "AM4U.texturesCacheLocation";
        const string k_ThumbnailsCacheLocationKey = "AM4U.thumbnailsCaheLocation";
        const string k_AssetManagerCacheLocationKey = "AM4U.assetManagerCacheLocation";
        const string k_TagsCreationConfidenceThreshold = "AM4U.tagsCreationConfidenceThreshold";
        const int k_DefaultConfidenceLevel = 80;

        Settings m_Settings;

        Settings Instance
        {
            get
            {
                if (m_Settings == null)
                {
                    m_Settings = new Settings(AssetManagerCoreConstants.PackageName);
                }

                return m_Settings;
            }
        }

        public string DefaultImportLocation
        {
            set
            {
                Utilities.DevAssert(value.StartsWith(AssetManagerCoreConstants.AssetsFolderName));

                if (string.IsNullOrEmpty(value))
                    return;

                Instance.Set(k_DefaultImportLocationKey, value, SettingsScope.User);
            }

            get => Instance.Get(k_DefaultImportLocationKey, SettingsScope.User, GetDefaultImportLocation());
        }

        public bool IsSubfolderCreationEnabled => Instance.Get(k_IsSubfolderCreationEnabledKey, SettingsScope.User, false);

        public bool IsTagsCreationUploadEnabled => Instance.Get(k_IsTagsCreationUploadEnabledKey, SettingsScope.User, false);

        public int TagsConfidenceThresholdPercent => Instance.Get(k_TagsCreationConfidenceThreshold, SettingsScope.User, k_DefaultConfidenceLevel);
        public float TagsConfidenceThreshold => TagsConfidenceThresholdPercent / 100f;

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
                var thumbnailsCacheLocation = Instance.Get<string>(k_ThumbnailsCacheLocationKey, SettingsScope.User);
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
                SetMaxCacheSize(AssetManagerCoreConstants.DefaultCacheSizeGb);
                return AssetManagerCoreConstants.DefaultCacheSizeGb;
            }
        }

        public int MaxCacheSizeMb => MaxCacheSizeGb * 1024;

        [ServiceInjection]
        public void Inject(ICachePathHelper cachePathHelper)
        {
            m_CachePathHelper = cachePathHelper;
        }

        public void SetIsSubfolderCreationEnabled(bool value)
        {
            Instance.Set(k_IsSubfolderCreationEnabledKey, value, SettingsScope.User);
        }

        public void SetIsTagsCreationUploadEnabled(bool value)
        {
            Instance.Set(k_IsTagsCreationUploadEnabledKey, value, SettingsScope.User);
        }

        public void SetTagsCreationConfidenceThresholdPercent(int value)
        {
            Instance.Set(k_TagsCreationConfidenceThreshold, value, SettingsScope.User);
        }

        public void SetCacheLocation(string cacheLocation)
        {
            if (string.IsNullOrEmpty(cacheLocation))
            {
                cacheLocation = m_CachePathHelper.GetDefaultCacheLocation();
            }

            Instance.Set(k_CacheLocationKey, cacheLocation, SettingsScope.User);
            var assetManagerCacheLocation = m_CachePathHelper.CreateAssetManagerCacheLocation(cacheLocation);
            Instance.Set(k_AssetManagerCacheLocationKey, m_CachePathHelper.CreateAssetManagerCacheLocation(cacheLocation),
                SettingsScope.User);
            Instance.Set(k_ThumbnailsCacheLocationKey,
                Path.Combine(assetManagerCacheLocation, AssetManagerCoreConstants.CacheThumbnailsFolderName), SettingsScope.User);
            Instance.Set(k_TexturesCacheLocationKey,
                Path.Combine(assetManagerCacheLocation, AssetManagerCoreConstants.CacheTexturesFolderName), SettingsScope.User);
        }

        public void SetMaxCacheSize(int cacheSize)
        {
            if (cacheSize <= 0)
            {
                return;
            }

            Instance.Set(k_MaxCacheSizeKey, cacheSize, SettingsScope.User);
        }

        static string GetDefaultImportLocation()
        {
            return AssetManagerCoreConstants.AssetsFolderName;
        }

        public string ResetCacheLocation()
        {
            var defaultLocation = m_CachePathHelper.GetDefaultCacheLocation();
            SetCacheLocation(defaultLocation);
            return defaultLocation;
        }

        public string ResetImportLocation()
        {
            var defaultLocation = GetDefaultImportLocation();
            DefaultImportLocation = defaultLocation;
            return defaultLocation;
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
            SetCacheLocation(defaultLocation);
            return Path.Combine(defaultLocation, AssetManagerCoreConstants.CacheTexturesFolderName);
        }
    }
}
