using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Extension methods for AssetDataCacheEntry.
    /// </summary>
    static class AssetDataCacheEntryExtensions
    {
        /// <summary>
        /// Returns an AssetIdentifier for this entry, for use with IAssetsProvider (e.g. refresh).
        /// Returns null if required identifier fields are missing.
        /// </summary>
        public static AssetIdentifier ToAssetIdentifier(this AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return null;

            var id = new AssetIdentifier(entry.organizationId, entry.projectId, entry.assetId, entry.versionId ?? string.Empty, entry.versionLabel ?? string.Empty);
            if (!string.IsNullOrEmpty(entry.libraryId))
                id.LibraryId = entry.libraryId;
            return id;
        }
    }

    /// <summary>
    /// Extension methods for IAssetDataCacheManager providing higher-level operations.
    /// These methods handle conversion, fetching, and data manipulation.
    /// </summary>
    static class AssetDataCacheManagerExtensions
    {
        /// <summary>
        /// Writes a cache entry for the given asset data.
        /// Converts AssetData to AssetDataCacheEntry before writing.
        /// </summary>
        public static void WriteEntry(this IAssetDataCacheManager cacheManager, AssetData assetData)
        {
            if (cacheManager == null)
                return;
            if (assetData == null)
            {
                Utilities.DevLogWarning("Cannot write AssetDataCache entry: assetData is null");
                return;
            }

            var entry = AssetDataCacheConverter.FromAssetData(assetData);
            if (entry == null)
            {
                Utilities.DevLogWarning($"Failed to convert AssetData to AssetDataCacheEntry for asset '{assetData.Identifier?.AssetId}'");
                return;
            }

            cacheManager.WriteEntry(entry);
        }

        /// <summary>
        /// Updates the linked projects and collections for a cache entry.
        /// Call this after fetching linked data from the API.
        /// </summary>
        /// <param name="cacheManager">The cache manager instance.</param>
        /// <param name="assetId">The asset ID to update.</param>
        /// <param name="linkedProjects">The linked project identifiers.</param>
        /// <param name="linkedCollections">The linked collection identifiers.</param>
        public static void UpdateLinkedData(
            this IAssetDataCacheManager cacheManager,
            string assetId,
            IEnumerable<ProjectIdentifier> linkedProjects,
            IEnumerable<CollectionIdentifier> linkedCollections)
        {
            if (cacheManager == null || string.IsNullOrEmpty(assetId))
                return;

            var entry = cacheManager.GetEntry(assetId);
            if (entry == null)
            {
                Utilities.DevLogWarning($"Cannot update linked data: no cache entry for asset '{assetId}'");
                return;
            }

            AssetDataCacheConverter.UpdateLinkedProjects(entry, linkedProjects);
            AssetDataCacheConverter.UpdateLinkedCollections(entry, linkedCollections);

            cacheManager.WriteEntry(entry);
        }

        /// <summary>
        /// Populates an AssetData object with cached asset data.
        /// </summary>
        /// <param name="cacheManager">The cache manager instance.</param>
        /// <param name="assetData">The asset data to populate.</param>
        /// <returns>True if population occurred, false if no cache entry exists.</returns>
        public static bool PopulateFromCache(
            this IAssetDataCacheManager cacheManager,
            BaseAssetData assetData)
        {
            if (cacheManager == null || assetData?.Identifier == null)
                return false;

            var entry = cacheManager.GetEntry(assetData.Identifier.AssetId);
            if (entry == null)
                return false;

            return AssetDataCacheConverter.PopulateFromCache(assetData, entry);
        }

        /// <summary>
        /// Ensures cache entry exists for the given cache entry.
        /// If it doesn't exist, queues a background refresh.
        /// </summary>
        /// <param name="cacheManager">The cache manager instance.</param>
        /// <param name="entry">The cache entry to ensure (identifier fields used for refresh).</param>
        /// <returns>True if cache entry exists, false if refresh was queued.</returns>
        public static bool EnsureCacheEntry(this IAssetDataCacheManager cacheManager, AssetDataCacheEntry entry)
        {
            if (cacheManager == null || entry == null || string.IsNullOrEmpty(entry.assetId))
                return false;

            if (cacheManager.HasEntry(entry.assetId))
                return true;

            cacheManager.QueueRefresh(entry);
            return false;
        }

        /// <summary>
        /// Ensures cache entry exists for the given asset data.
        /// Converts to cache entry and queues refresh if missing.
        /// </summary>
        /// <param name="cacheManager">The cache manager instance.</param>
        /// <param name="assetData">The asset data to ensure cache for.</param>
        /// <returns>True if cache entry exists, false if refresh was queued.</returns>
        public static bool EnsureCacheEntry(this IAssetDataCacheManager cacheManager, AssetData assetData)
        {
            if (cacheManager == null || assetData == null)
                return false;

            var entry = AssetDataCacheConverter.FromAssetData(assetData);
            if (entry == null)
                return false;

            return EnsureCacheEntry(cacheManager, entry);
        }

        /// <summary>
        /// Queues a background refresh for the given asset data.
        /// Converts to cache entry (identifier fields) and enqueues.
        /// </summary>
        public static void QueueRefresh(this IAssetDataCacheManager cacheManager, AssetData assetData)
        {
            if (cacheManager == null || assetData == null)
                return;

            var entry = AssetDataCacheConverter.FromAssetData(assetData);
            if (entry != null)
                cacheManager.QueueRefresh(entry);
        }

        /// <summary>
        /// Queues background refreshes for the given asset data.
        /// </summary>
        public static void QueueRefresh(this IAssetDataCacheManager cacheManager, IEnumerable<AssetData> assetDatas)
        {
            if (cacheManager == null || assetDatas == null)
                return;

            foreach (var assetData in assetDatas)
            {
                QueueRefresh(cacheManager, assetData);
            }
        }

        /// <summary>
        /// Writes the cache entry for the given asset data without raising CacheEntryRefreshed.
        /// Converts AssetData to AssetDataCacheEntry then writes.
        /// </summary>
        public static void WriteEntryWithoutNotify(this IAssetDataCacheManager cacheManager, AssetData assetData)
        {
            if (cacheManager == null || assetData == null)
                return;

            var entry = AssetDataCacheConverter.FromAssetData(assetData);
            if (entry != null)
                cacheManager.WriteEntryWithoutNotify(entry);
        }

        /// <summary>
        /// Writes or updates the cache entry for the given asset data.
        /// If an entry exists, writes without notify; otherwise writes and raises CacheEntryRefreshed.
        /// </summary>
        public static void WriteOrUpdateEntry(this IAssetDataCacheManager cacheManager, AssetData assetData)
        {
            if (cacheManager == null || assetData == null)
                return;

            if (cacheManager.HasEntry(assetData.Identifier.AssetId))
                cacheManager.WriteEntryWithoutNotify(assetData);
            else
                cacheManager.WriteEntry(assetData);
        }
    }
}
