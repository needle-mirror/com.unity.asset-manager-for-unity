using System;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Service responsible for synchronizing imported asset data with the cloud cache.
    /// Coordinates between IAssetDataManager and IAssetDataCacheManager.
    /// </summary>
    interface IAssetDataCacheSyncService : IService
    {
        /// <summary>
        /// Gets the timestamp of the last sync operation.
        /// </summary>
        DateTime? LastSyncTime { get; }

        /// <summary>
        /// Performs a full sync by refreshing all imported assets from the cloud.
        /// </summary>
        void Sync();
    }

    [Serializable]
    class AssetDataCacheSyncService : BaseService<IAssetDataCacheSyncService>, IAssetDataCacheSyncService
    {
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IAssetDataCacheManager m_CacheManager;

        // Serialized as string to survive domain reloads (DateTime is not serializable)
        [SerializeField]
        string m_LastSyncTimeString;

        public DateTime? LastSyncTime
        {
            get
            {
                if (string.IsNullOrEmpty(m_LastSyncTimeString))
                    return null;

                if (DateTime.TryParse(m_LastSyncTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                    return result;

                return null;
            }
            private set
            {
                m_LastSyncTimeString = value?.ToString("o");
            }
        }

        [ServiceInjection]
        public void Inject(IAssetDataManager assetDataManager, IAssetDataCacheManager cacheManager)
        {
            m_AssetDataManager = assetDataManager;
            m_CacheManager = cacheManager;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();

            m_AssetDataManager ??= ServicesContainer.instance.Get<IAssetDataManager>();
            m_CacheManager ??= ServicesContainer.instance.Get<IAssetDataCacheManager>();
        }

        public void Sync()
        {
            LastSyncTime = DateTime.Now;

            m_CacheManager.ClearRefreshQueue();

            var importedAssets = m_AssetDataManager.ImportedAssetInfos
                .Select(info => info.AssetData)
                .OfType<AssetData>();

            m_CacheManager.QueueRefresh(importedAssets);
        }
    }
}
