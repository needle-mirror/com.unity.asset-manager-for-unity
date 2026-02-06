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
        /// Gets a cache entry for the specified asset ID.
        /// </summary>
        /// <param name="assetId">The asset ID to look up.</param>
        /// <returns>The cache entry, or null if no entry exists or assetId is null/empty.</returns>
        AssetDataCacheEntry GetEntry(string assetId);

        /// <summary>
        /// Gets all cached entries from disk.
        /// </summary>
        /// <returns>Read-only collection of all cache entries (empty if none exist).</returns>
        IReadOnlyCollection<AssetDataCacheEntry> GetAllEntries();

        /// <summary>
        /// Writes a cache entry to disk.
        /// </summary>
        /// <param name="entry">The entry to write. Ignored if null or entry.assetId is null/empty.</param>
        void WriteEntry(AssetDataCacheEntry entry);

        /// <summary>
        /// Writes the cache entry to disk without raising CacheEntryRefreshed.
        /// Use when persisting in-memory changes to avoid re-notifying listeners.
        /// </summary>
        /// <param name="entry">The cache entry to write. Ignored if null or entry.assetId is null/empty.</param>
        void WriteEntryWithoutNotify(AssetDataCacheEntry entry);

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
        /// Queues a background refresh task for the specified cache entry.
        /// If a refresh is already in progress for this asset, it will not be queued again.
        /// </summary>
        /// <param name="entry">The cache entry (identifier fields used for provider calls).</param>
        void QueueRefresh(AssetDataCacheEntry entry);

        /// <summary>
        /// Queues refresh tasks for multiple cache entries.
        /// </summary>
        /// <param name="entries">The cache entries to refresh.</param>
        void QueueRefresh(IEnumerable<AssetDataCacheEntry> entries);

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

        // Queue of cache entries waiting to be refreshed
        readonly Queue<AssetDataCacheEntry> m_RefreshQueue = new();

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

        public AssetDataCacheEntry GetEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return null;

            return AssetDataCachePersistence.ReadEntry(m_IOProxy, assetId);
        }

        public IReadOnlyCollection<AssetDataCacheEntry> GetAllEntries()
        {
            return AssetDataCachePersistence.ReadAllEntries(m_IOProxy);
        }

        public void WriteEntry(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
            {
                Utilities.DevLogWarning("Cannot write AssetDataCache entry: entry or assetId is null/empty");
                return;
            }

            WriteEntryInternal(entry);
            CacheEntryRefreshed?.Invoke(entry.assetId);
        }

        public void WriteEntryWithoutNotify(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return;

            WriteEntryInternal(entry);
        }

        // Writes the entry to disk without raising CacheEntryRefreshed.
        void WriteEntryInternal(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return;

            AssetDataCachePersistence.WriteEntry(m_IOProxy, entry);
        }

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

        public void QueueRefresh(AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return;

            bool shouldStartProcessing = false;

            lock (m_RefreshQueue)
            {
                // Don't queue if already refreshing or already in queue
                if (m_RefreshingAssetIds.Contains(entry.assetId))
                    return;

                if (m_RefreshQueue.Any(e => e?.assetId == entry.assetId))
                    return;

                m_RefreshQueue.Enqueue(entry);

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

        public void QueueRefresh(IEnumerable<AssetDataCacheEntry> entries)
        {
            if (entries == null)
                return;

            foreach (var entry in entries)
            {
                QueueRefresh(entry);
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
                        entry = m_RefreshQueue.Dequeue();
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
                    await RefreshEntryAsyncInternal(identifier, m_AssetsProvider, cancellationToken);
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

        async Task RefreshEntryAsyncInternal(AssetIdentifier identifier, IAssetsProvider assetsProvider, CancellationToken token)
        {
            if (identifier == null || string.IsNullOrEmpty(identifier.AssetId))
                return;

            try
            {
                var freshAssetData = await assetsProvider.GetAssetAsync(identifier, token);

                if (freshAssetData == null)
                {
                    Utilities.DevLogWarning($"Failed to fetch asset data for '{identifier.AssetId}' from cloud");
                    return;
                }

                try
                {
                    var linkedProjects = await assetsProvider.GetLinkedProjectsAsync(freshAssetData, token);
                    if (linkedProjects != null)
                        freshAssetData.LinkedProjects = linkedProjects.ToList();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { Utilities.DevLogWarning($"Failed to fetch linked projects for '{identifier.AssetId}': {e.Message}"); }

                if (!freshAssetData.Identifier.IsAssetFromLibrary())
                {
                    try
                    {
                        var linkedCollections = await assetsProvider.GetLinkedCollectionsAsync(freshAssetData, token);
                        if (linkedCollections != null)
                            freshAssetData.LinkedCollections = linkedCollections.ToList();
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e) { Utilities.DevLogWarning($"Failed to fetch linked collections for '{identifier.AssetId}': {e.Message}"); }
                }

                try
                {
                    var dependencies = new List<AssetIdentifier>();
                    await foreach (var dependency in assetsProvider.GetDependenciesAsync(identifier, Range.All, token).WithCancellation(token))
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

