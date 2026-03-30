using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Core cache operations for AssetDataCache entries.
    /// Manages the fundamental key-value operations: assetId -> AssetDataCacheEntry
    /// and background refresh of cache entries for tracked assets.
    /// </summary>
    interface IAssetDataCacheManager : IService
    {
        /// <summary>
        /// Raised when a cache entry has been refreshed (either via background refresh or explicit update).
        /// </summary>
        event Action<string> CacheEntryRefreshed;

        /// <summary>
        /// Writes a cache entry for the given asset data.
        /// Converts AssetData to AssetDataCacheEntry before writing.
        /// Raises CacheEntryRefreshed after writing.
        /// </summary>
        /// <param name="assetData">The asset data to write. Ignored if null.</param>
        void WriteEntry(AssetData assetData);

        /// <summary>
        /// Writes the cache entry for the given asset data without raising CacheEntryRefreshed.
        /// Converts AssetData to AssetDataCacheEntry then writes.
        /// Use when persisting in-memory changes to avoid re-notifying listeners.
        /// </summary>
        /// <param name="assetData">The asset data to write. Ignored if null.</param>
        void WriteEntryWithoutNotify(AssetData assetData);

        /// <summary>
        /// Writes or updates the cache entry for the given asset data.
        /// If an entry exists, writes without notify; otherwise writes and raises CacheEntryRefreshed.
        /// </summary>
        /// <param name="assetData">The asset data to write or update. Ignored if null.</param>
        void WriteOrUpdateEntry(AssetData assetData);

        /// <summary>
        /// Populates an AssetData object with cached asset data.
        /// </summary>
        /// <param name="assetData">The asset data to populate.</param>
        /// <returns>True if population occurred, false if no cache entry exists.</returns>
        bool PopulateFromCache(BaseAssetData assetData);

        /// <summary>
        /// Updates the linked projects and collections for a cache entry.
        /// Call this after fetching linked data from the API.
        /// </summary>
        /// <param name="assetId">The asset ID to update.</param>
        /// <param name="linkedProjects">The linked project identifiers.</param>
        /// <param name="linkedCollections">The linked collection identifiers.</param>
        void UpdateLinkedData(
            string assetId,
            IEnumerable<ProjectIdentifier> linkedProjects,
            IEnumerable<CollectionIdentifier> linkedCollections);

        /// <summary>
        /// Ensures cache entry exists for the given asset data.
        /// Queues refresh if missing.
        /// </summary>
        /// <param name="assetData">The asset data to ensure cache for.</param>
        /// <returns>True if cache entry exists, false if refresh was queued.</returns>
        bool EnsureCacheEntry(AssetData assetData);

        /// <summary>
        /// Removes the cache entry for the specified asset ID.
        /// </summary>
        /// <param name="assetId">The asset ID whose cache entry should be removed.</param>
        void RemoveEntry(string assetId);

        /// <summary>
        /// Removes all cache entries.
        /// </summary>
        void RemoveAll();

        /// <summary>
        /// Checks if a cache entry exists for the specified asset ID.
        /// </summary>
        /// <param name="assetId">The asset ID to check.</param>
        /// <returns>True if an entry exists, false otherwise or if assetId is null/empty.</returns>
        bool HasEntry(string assetId);

        /// <summary>
        /// Gets the timestamp when the cache entry was written for the specified asset ID.
        /// </summary>
        /// <param name="assetId">The asset ID to check.</param>
        /// <returns>The DateTime (UTC) when the entry was cached, or null if no entry exists or timestamp is invalid.</returns>
        DateTime? GetCachedAt(string assetId);

        /// <summary>
        /// Queues a background refresh task for the specified cache entry.
        /// If a refresh is already in progress for this asset, it will not be queued again.
        /// </summary>
        /// <param name="assetData">The asset data to refresh.</param>
        /// <param name="addFirst">If true, the refresh will be added to the front of the queue (higher priority). Default is false (added to end of queue).</param>
        void QueueRefresh(AssetData assetData, bool addFirst = false);

        /// <summary>
        /// Queues background refresh tasks for the given asset data.
        /// Converts each AssetData to a cache entry and queues it.
        /// </summary>
        /// <param name="assetDatas">The assets data to refresh.</param>
        void QueueRefresh(IEnumerable<AssetData> assetDatas);

        /// <summary>
        /// Checks if a refresh is currently in progress for the specified asset ID.
        /// </summary>
        /// <param name="assetId">The asset ID to check.</param>
        /// <returns>True if a refresh is in progress or queued, false otherwise.</returns>
        bool IsRefreshInProgress(string assetId);

        /// <summary>
        /// Gets the number of pending refresh tasks in the queue.
        /// </summary>
        int PendingRefreshCount { get; }

        /// <summary>
        /// Clears all pending refresh tasks from the queue.
        /// Does not affect the item currently being refreshed.
        /// </summary>
        void ClearRefreshQueue();
    }

    [Serializable]
    class AssetDataCacheManager : BaseService<IAssetDataCacheManager>, IAssetDataCacheManager
    {
        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        // Track which assets are currently being refreshed to avoid duplicate work
        readonly HashSet<string> m_RefreshingAssetIds = new();

        // List of cache entries waiting to be refreshed (used as a queue, supports priority insertion at front)
        readonly List<AssetDataCacheEntry> m_RefreshQueue = new();

        // Cancellation token source for background refresh tasks
        CancellationTokenSource m_CancellationTokenSource;

        // Background task that processes the refresh queue
        Task m_BackgroundRefreshTask;

        public event Action<string> CacheEntryRefreshed;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetsProvider assetsProvider)
        {
            m_IOProxy = ioProxy;
            m_AssetsProvider = assetsProvider;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();

            m_IOProxy ??= ServicesContainer.instance.Get<IIOProxy>();
            m_AssetsProvider ??= ServicesContainer.instance.Get<IAssetsProvider>();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            // Background refresh task will be started when items are added to the queue
            m_CancellationTokenSource = new CancellationTokenSource();
        }

        public override void OnDisable()
        {
            // Cancel background refresh task. Do not wait for it to complete, as it runs on the
            // main thread context and waiting would cause a deadlock or freeze.
            m_CancellationTokenSource?.Cancel();

            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = null;
            m_BackgroundRefreshTask = null;

            base.OnDisable();
        }

        #region Interface Methods

        public void WriteEntry(AssetData assetData)
        {
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

            WriteEntryInternal(entry);
            CacheEntryRefreshed?.Invoke(entry.assetId);
        }

        public void WriteEntryWithoutNotify(AssetData assetData)
        {
            if (assetData == null)
                return;

            var entry = AssetDataCacheConverter.FromAssetData(assetData);
            if (entry != null)
                WriteEntryInternal(entry);
        }

        public void WriteOrUpdateEntry(AssetData assetData)
        {
            if (assetData == null)
                return;

            if (HasEntry(assetData.Identifier.AssetId))
                WriteEntryWithoutNotify(assetData);
            else
                WriteEntry(assetData);
        }

        public bool PopulateFromCache(BaseAssetData assetData)
        {
            if (assetData?.Identifier == null)
                return false;

            var entry = GetEntry(assetData.Identifier.AssetId);
            if (entry == null)
                return false;

            return AssetDataCacheConverter.PopulateFromCache(assetData, entry);
        }

        public void UpdateLinkedData(
            string assetId,
            IEnumerable<ProjectIdentifier> linkedProjects,
            IEnumerable<CollectionIdentifier> linkedCollections)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            var entry = GetEntry(assetId);
            if (entry == null)
            {
                Utilities.DevLogWarning($"Cannot update linked data: no cache entry for asset '{assetId}'");
                return;
            }

            AssetDataCacheConverter.UpdateLinkedProjects(entry, linkedProjects);
            AssetDataCacheConverter.UpdateLinkedCollections(entry, linkedCollections);

            WriteEntryInternal(entry);
            CacheEntryRefreshed?.Invoke(entry.assetId);
        }

        public bool EnsureCacheEntry(AssetData assetData)
        {
            if (assetData == null)
                return false;

            if (HasEntry(assetData.Identifier.AssetId))
                return true;

            QueueRefresh(assetData);
            return false;
        }

        #endregion

        #region Internal Cache Entry Operations

        AssetDataCacheEntry GetEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return null;

            return AssetDataCachePersistence.ReadEntry(m_IOProxy, assetId);
        }

        void WriteEntry(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
            {
                Utilities.DevLogWarning("Cannot write AssetDataCache entry: entry or assetId is null/empty");
                return;
            }

            WriteEntryInternal(entry);
            CacheEntryRefreshed?.Invoke(entry.assetId);
        }

        void WriteEntryInternal(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return;

            AssetDataCachePersistence.WriteEntry(m_IOProxy, entry);
        }

        #endregion

        public void RemoveEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            AssetDataCachePersistence.RemoveEntry(m_IOProxy, assetId);
        }

        public void RemoveAll()
        {
            AssetDataCachePersistence.ClearAllEntries(m_IOProxy);
        }

        public bool HasEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return false;

            return AssetDataCachePersistence.EntryExists(m_IOProxy, assetId);
        }

        public DateTime? GetCachedAt(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return null;

            var entry = GetEntry(assetId);
            if (entry == null || string.IsNullOrEmpty(entry.cachedAt))
                return null;

            if (DateTime.TryParse(entry.cachedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var cachedAt))
                return cachedAt;

            return null;
        }

        public void QueueRefresh(AssetData assetData, bool addFirst = false)
        {
            if (assetData == null)
                return;

            var entry = AssetDataCacheConverter.FromAssetData(assetData);

            var shouldStartProcessing = false;
            lock (m_RefreshQueue)
            {
                // Don't queue if already refreshing or already in queue
                if (m_RefreshingAssetIds.Contains(entry.assetId) && !addFirst)
                    return;

                if (m_RefreshQueue.Any(e => e?.assetId == entry.assetId) && !addFirst)
                    return;

                if (addFirst)
                    m_RefreshQueue.Insert(0, entry);
                else
                    m_RefreshQueue.Add(entry);

                if (m_BackgroundRefreshTask == null || m_BackgroundRefreshTask.IsCompleted)
                {
                    shouldStartProcessing = true;
                }
            }

            if (shouldStartProcessing)
            {
                StartProcessingQueue();
            }
        }


        public void QueueRefresh(IEnumerable<AssetData> assetsData)
        {
            if (assetsData == null)
                return;

            foreach (var assetData in assetsData)
            {
                if (assetData == null)
                    continue;

                QueueRefresh(assetData);
            }
        }

        public bool IsRefreshInProgress(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return false;

            lock (m_RefreshQueue)
            {
                return m_RefreshingAssetIds.Contains(assetId) ||
                       m_RefreshQueue.Any(e => e?.assetId == assetId);
            }
        }

        public int PendingRefreshCount
        {
            get
            {
                lock (m_RefreshQueue)
                {
                    return m_RefreshQueue.Count;
                }
            }
        }

        public void ClearRefreshQueue()
        {
            lock (m_RefreshQueue)
            {
                Utilities.DevLog("Clearing refresh queue. Pending refreshes cancelled: " + m_RefreshQueue.Count, highlight: true);
                m_RefreshQueue.Clear();
            }
        }

        /// <summary>
        /// Starts processing the refresh queue if not already running.
        /// </summary>
        void StartProcessingQueue()
        {
            lock (m_RefreshQueue)
            {
                // Only start if not already running and cancellation token is available
                if (m_BackgroundRefreshTask == null || m_BackgroundRefreshTask.IsCompleted)
                {
                    if (m_CancellationTokenSource == null || m_CancellationTokenSource.IsCancellationRequested)
                    {
                        // Can't start if cancelled
                        return;
                    }

                    m_BackgroundRefreshTask = ProcessRefreshQueueAsync(m_CancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// Background task that processes the refresh queue.
        /// Processes one asset at a time to avoid overwhelming the API.
        /// Stops when the queue is empty.
        /// </summary>
        async Task ProcessRefreshQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AssetDataCacheEntry entry = null;

                lock (m_RefreshQueue)
                {
                    if (m_RefreshQueue.Count > 0)
                    {
                        entry = m_RefreshQueue[0];
                        m_RefreshQueue.RemoveAt(0);
                        if (entry != null && !string.IsNullOrEmpty(entry.assetId))
                        {
                            m_RefreshingAssetIds.Add(entry.assetId);
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                if (entry == null || string.IsNullOrEmpty(entry.assetId))
                    continue;

                var assetId = entry.assetId;
                var identifier = entry.ToAssetIdentifier();
                if (identifier == null)
                {
                    Utilities.DevLogWarning($"Cannot refresh cache for asset '{assetId}': entry has insufficient identifier data");
                    lock (m_RefreshQueue) { m_RefreshingAssetIds.Remove(assetId); }
                    continue;
                }


                try
                {
                    Utilities.DevLog($"Refreshing cache for asset: {assetId}", highlight: true);
                    await RefreshEntryAsyncInternal(identifier, cancellationToken);
                    Utilities.DevLog($"Cache refresh complete for asset: {assetId}", highlight: true);
                }
                catch (OperationCanceledException)
                {
                    Utilities.DevLog($"Cache refresh cancelled for asset: {assetId}");
                    throw;
                }
                catch (Exception e)
                {
                    Utilities.DevLogWarning($"Failed to refresh cache for asset '{assetId}': {e.Message}");
                }
                finally
                {
                    lock (m_RefreshQueue)
                    {
                        m_RefreshingAssetIds.Remove(assetId);
                    }
                }
            }
        }

        async Task RefreshEntryAsyncInternal(AssetIdentifier identifier, CancellationToken token)
        {
            if (identifier == null || string.IsNullOrEmpty(identifier.AssetId))
                return;

            try
            {
                var freshAssetData = await m_AssetsProvider.GetAssetAsync(identifier, token);

                if (freshAssetData == null)
                {
                    Utilities.DevLogWarning($"Failed to fetch asset data for '{identifier.AssetId}' from cloud");
                    return;
                }

                // Resolve datasets to fetch file lists before writing to cache.
                // GetAssetAsync returns datasets without files; ResolveDatasetsAsync populates them.
                try
                {
                    await freshAssetData.ResolveDatasetsAsync(token);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Utilities.DevLogWarning($"Failed to resolve datasets for '{identifier.AssetId}': {e.Message}"); }

                try
                {
                    var linkedProjects = await m_AssetsProvider.GetLinkedProjectsAsync(freshAssetData, token);
                    if (linkedProjects != null)
                        freshAssetData.LinkedProjects = linkedProjects.ToList();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Utilities.DevLogWarning($"Failed to fetch linked projects for '{identifier.AssetId}': {e.Message}"); }

                if (!freshAssetData.Identifier.IsAssetFromLibrary())
                {
                    try
                    {
                        var linkedCollections = await m_AssetsProvider.GetLinkedCollectionsAsync(freshAssetData, token);
                        if (linkedCollections != null)
                            freshAssetData.LinkedCollections = linkedCollections.ToList();
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e) { Utilities.DevLogWarning($"Failed to fetch linked collections for '{identifier.AssetId}': {e.Message}"); }
                }

                try
                {
                    var dependencies = new List<AssetIdentifier>();
                    await foreach (var dependency in m_AssetsProvider.GetDependenciesAsync(identifier, Range.All, token).WithCancellation(token))
                    {
                        if (dependency?.TargetAssetIdentifier != null)
                            dependencies.Add(dependency.TargetAssetIdentifier);
                    }
                    if (dependencies.Count > 0 && freshAssetData is AssetData ad)
                        ad.Dependencies = dependencies;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Utilities.DevLogWarning($"Failed to fetch dependencies for '{identifier.AssetId}': {e.Message}"); }

                var entry = AssetDataCacheConverter.FromAssetData(freshAssetData);
                if (entry != null)
                    WriteEntry(entry);
                Utilities.DevLog($"Refreshed AssetDataCache for asset '{identifier.AssetId}'");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Utilities.DevLogWarning($"Failed to refresh AssetDataCache for asset '{identifier.AssetId}': {e.Message}");
            }
        }
    }
}

