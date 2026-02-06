using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    class AssetChangeArgs
    {
        public IReadOnlyCollection<TrackedAssetIdentifier> Added = Array.Empty<TrackedAssetIdentifier>();
        public IReadOnlyCollection<TrackedAssetIdentifier> Removed = Array.Empty<TrackedAssetIdentifier>();
        public IReadOnlyCollection<TrackedAssetIdentifier> Updated = Array.Empty<TrackedAssetIdentifier>();
    }

    interface IAssetDataManager : IService
    {
        event Action<AssetChangeArgs> ImportedAssetInfoChanged;
        event Action<AssetChangeArgs> AssetDataChanged;

        IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos { get; }

        void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos);

        void AddOrUpdateGuidsToImportedAssetInfo(BaseAssetData assetData,
            IReadOnlyCollection<ImportedFileInfo> fileInfos,
            bool shouldUpdateCache = true);

        void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove);
        void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<BaseAssetData> assetDatas);
        ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier assetIdentifier);
        ImportedAssetInfo GetImportedAssetInfo(string assetId);
        void RemoveImportedAssetInfo(IEnumerable<AssetIdentifier> assetIdentifiers);
        List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string guid);
        string GetImportedFileGuid(AssetIdentifier assetIdentifier, string path);
        BaseAssetData GetAssetData(AssetIdentifier assetIdentifier);
        List<BaseAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids);
        Task<BaseAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token);
        bool IsInProject(AssetIdentifier id);
        HashSet<AssetIdentifier> FindExclusiveDependencies(IEnumerable<AssetIdentifier> assetIdentifiersToDelete);
        void QueueMissingCacheRefreshes();
    }

    [Serializable]
    class AssetDataManager : BaseService<IAssetDataManager>, IAssetDataManager, ISerializationCallbackReceiver
    {
        class Node
        {
            readonly TrackedAssetIdentifier m_Identifier;
            readonly HashSet<Node> m_Dependencies = new();
            readonly HashSet<Node> m_DependentBy = new();

            public TrackedAssetIdentifier Identifier => m_Identifier;
            public HashSet<Node> Dependencies => m_Dependencies;
            public HashSet<Node> DependentBy => m_DependentBy;
            public bool IsRoot { get; set; }

            public Node(TrackedAssetIdentifier identifier)
            {
                m_Identifier = identifier;
            }

            public override bool Equals(object obj)
            {
                return obj is Node node && Identifier.Equals(node.Identifier);
            }

            public override int GetHashCode()
            {
                return m_Identifier.GetHashCode();
            }
        }

        class TrackedIdentifierMap : IDictionary<TrackedAssetIdentifier, ImportedAssetInfo>
        {
            readonly Dictionary<TrackedAssetIdentifier, ImportedAssetInfo> m_ImportedAssetInfoLookup = new();
            readonly Dictionary<TrackedAssetIdentifier, HashSet<TrackedAssetIdentifier>> m_DependenciesMap = new();
            readonly Dictionary<TrackedAssetIdentifier, HashSet<TrackedAssetIdentifier>> m_DependentsMap = new();

            public IReadOnlyDictionary<TrackedAssetIdentifier, HashSet<TrackedAssetIdentifier>> DependentsMap => m_DependentsMap;

            public ImportedAssetInfo this[TrackedAssetIdentifier key]
            {
                get => m_ImportedAssetInfoLookup[key];
                set
                {
                    m_ImportedAssetInfoLookup[key] = value;
                    UpdateDependencies();
                }
            }

            public ICollection<TrackedAssetIdentifier> Keys => m_ImportedAssetInfoLookup.Keys;
            public ICollection<ImportedAssetInfo> Values => m_ImportedAssetInfoLookup.Values;
            public int Count => m_ImportedAssetInfoLookup.Count;
            public bool IsReadOnly => ((IDictionary<TrackedAssetIdentifier, ImportedAssetInfo>)m_ImportedAssetInfoLookup).IsReadOnly;

            public void Add(TrackedAssetIdentifier key, ImportedAssetInfo value)
            {
                m_ImportedAssetInfoLookup.Add(key, value);
                UpdateDependencies();
            }

            public void Add(KeyValuePair<TrackedAssetIdentifier, ImportedAssetInfo> item)
            {
                m_ImportedAssetInfoLookup.Add(item.Key, item.Value);
                UpdateDependencies();
            }

            public bool Remove(TrackedAssetIdentifier key)
            {
                var result = m_ImportedAssetInfoLookup.Remove(key);
                if (result)
                    RemoveFromDependencies(key);
                return result;
            }

            public bool Remove(KeyValuePair<TrackedAssetIdentifier, ImportedAssetInfo> item)
            {
                var result = m_ImportedAssetInfoLookup.Remove(item.Key);
                if (result)
                    RemoveFromDependencies(item.Key);
                return result;
            }

            public bool TryGetValue(TrackedAssetIdentifier key, out ImportedAssetInfo value) => m_ImportedAssetInfoLookup.TryGetValue(key, out value);
            public bool ContainsKey(TrackedAssetIdentifier key) => m_ImportedAssetInfoLookup.ContainsKey(key);
            public bool Contains(KeyValuePair<TrackedAssetIdentifier, ImportedAssetInfo> item) => m_ImportedAssetInfoLookup.Contains(item);

            public void CopyTo(KeyValuePair<TrackedAssetIdentifier, ImportedAssetInfo>[] array, int arrayIndex) => ((IDictionary<TrackedAssetIdentifier, ImportedAssetInfo>)m_ImportedAssetInfoLookup).CopyTo(array, arrayIndex);

            public void Clear()
            {
                m_ImportedAssetInfoLookup.Clear();
                ClearDependencyMaps();
            }

            void ClearDependencyMaps()
            {
                m_DependentsMap.Clear();
                m_DependenciesMap.Clear();
            }

            public IEnumerator<KeyValuePair<TrackedAssetIdentifier, ImportedAssetInfo>> GetEnumerator() => m_ImportedAssetInfoLookup.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => m_ImportedAssetInfoLookup.GetEnumerator();

            public HashSet<TrackedAssetIdentifier> GetDependencies(TrackedAssetIdentifier trackedAssetIdentifier)
                => m_DependenciesMap.TryGetValue(trackedAssetIdentifier, out var directDependencies) ? directDependencies : UpdateDependencies(trackedAssetIdentifier);

            void UpdateDependencies()
            {
                UpdateDependencyMap();
                UpdateDependentsMap();
            }

            void UpdateDependencyMap()
            {
                foreach (var id in m_ImportedAssetInfoLookup.Keys)
                    UpdateDependencies(id);
            }

            HashSet<TrackedAssetIdentifier> UpdateDependencies(TrackedAssetIdentifier id)
            {
                var dependencies = new HashSet<TrackedAssetIdentifier>();

                if(m_ImportedAssetInfoLookup.TryGetValue(id, out var importedAssetInfo))
                {
                    foreach (var dependencyId in importedAssetInfo.AssetData.Dependencies)
                    {
                        var trackedDependencyId = new TrackedAssetIdentifier(dependencyId);

                        // Only add the dependency if it's an imported asset
                        if (m_ImportedAssetInfoLookup.ContainsKey(trackedDependencyId))
                            dependencies.Add(new TrackedAssetIdentifier(dependencyId));
                    }
                }

                m_DependenciesMap[id] = dependencies;
                return dependencies;
            }

            void UpdateDependentsMap()
            {
                foreach (var id in m_ImportedAssetInfoLookup.Keys)
                    UpdateDependents(id);
            }

            void UpdateDependents(TrackedAssetIdentifier id)
            {
                if (!m_DependentsMap.ContainsKey(id))
                    m_DependentsMap[id] = new HashSet<TrackedAssetIdentifier>();

                foreach (var dependency in GetDependencies(id))
                {
                    if (!m_DependentsMap.ContainsKey(dependency))
                        m_DependentsMap[dependency] = new HashSet<TrackedAssetIdentifier>();

                    m_DependentsMap[dependency].Add(id);
                }
            }

            void RemoveFromDependencies(TrackedAssetIdentifier id)
            {
                RemoveFromDependencyMap(id);
                RemoveFromDependentsMap(id);
            }

            void RemoveFromDependencyMap(TrackedAssetIdentifier id) => m_DependenciesMap.Remove(id);

            void RemoveFromDependentsMap(TrackedAssetIdentifier id)
            {
                m_DependentsMap.Remove(id);
                foreach (var dependency in m_DependentsMap.Values)
                    dependency.Remove(id);
            }

        }

        [SerializeField]
        ImportedAssetInfo[] m_SerializedImportedAssetInfos = Array.Empty<ImportedAssetInfo>();

        [SerializeReference]
        BaseAssetData[] m_SerializedAssetData = Array.Empty<BaseAssetData>();

        [SerializeReference]
        IPermissionsManager m_PermissionsManager;

        [SerializeReference]
        IAssetDataCacheManager m_AssetDataCacheManager;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        readonly Dictionary<TrackedAssetIdentifier, BaseAssetData> m_AssetData = new();
        readonly Dictionary<string, List<ImportedAssetInfo>> m_FileGuidToImportedAssetInfosMap = new();
        readonly TrackedIdentifierMap m_TrackedIdentifierMap = new();

        // Track subscriptions to AssetData.AssetDataChanged events
        readonly Dictionary<TrackedAssetIdentifier, BaseAssetData.AssetDataChangedDelegate> m_AssetDataEventSubscriptions = new();

        public event Action<AssetChangeArgs> ImportedAssetInfoChanged = delegate { };
        public event Action<AssetChangeArgs> AssetDataChanged = delegate { };

        public IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos =>
            (IReadOnlyCollection<ImportedAssetInfo>) m_TrackedIdentifierMap.Values;

        [ServiceInjection]
        public void Inject(IPermissionsManager permissionsManager, IAssetDataCacheManager assetDataCacheManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PermissionsManager = permissionsManager;
            m_AssetDataCacheManager = assetDataCacheManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public override void OnEnable()
        {
            base.OnEnable();

            if (m_PermissionsManager != null)
            {
                m_PermissionsManager.AuthenticationStateChanged += OnAuthenticationStateChanged;
            }

            if (m_AssetDataCacheManager != null)
            {
                m_AssetDataCacheManager.CacheEntryRefreshed += OnCacheEntryRefreshed;
            }

            if (m_ProjectOrganizationProvider != null)
            {
                m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
                if (m_ProjectOrganizationProvider.SelectedOrganization != null)
                    QueueMissingCacheRefreshes();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();

            if (m_PermissionsManager != null)
            {
                m_PermissionsManager.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            }

            if (m_AssetDataCacheManager != null)
            {
                m_AssetDataCacheManager.CacheEntryRefreshed -= OnCacheEntryRefreshed;
            }

            if (m_ProjectOrganizationProvider != null)
            {
                m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            }
        }

        void OnCacheEntryRefreshed(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            var info = GetImportedAssetInfo(assetId);
            if (info?.AssetData is AssetData ad && m_AssetDataCacheManager != null && m_AssetDataCacheManager.PopulateFromCache(ad))
            {
                AssetDataChanged?.Invoke(new AssetChangeArgs { Updated = new[] { new TrackedAssetIdentifier(ad.Identifier) } });
            }
        }

        void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            if (organizationInfo == null)
                return;
            QueueMissingCacheRefreshes();
        }

        void OnAuthenticationStateChanged(AuthenticationState newState)
        {
            if (newState != AuthenticationState.LoggedIn)
            {
                m_AssetData.Clear();
            }
        }

        public void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos)
        {
            allImportedInfos ??= Array.Empty<ImportedAssetInfo>();
            var oldAssetIds = m_TrackedIdentifierMap.Keys.ToHashSet();
            m_FileGuidToImportedAssetInfosMap.Clear();
            m_TrackedIdentifierMap.Clear();

            var added = new HashSet<TrackedAssetIdentifier>();
            var updated = new HashSet<TrackedAssetIdentifier>();
            foreach (var info in allImportedInfos)
            {
                var id = new TrackedAssetIdentifier(info.Identifier);

                AddOrUpdateImportedAssetInfo(info);

                // Populate linked data from AssetDataCache (supplements tracking file data)
                m_AssetDataCacheManager?.PopulateFromCache(info.AssetData);

                if (oldAssetIds.Contains(id))
                {
                    updated.Add(id);
                }
                else
                {
                    added.Add(id);
                }
            }

            foreach (var newInfo in m_TrackedIdentifierMap.Values)
            {
                oldAssetIds.Remove(new TrackedAssetIdentifier(newInfo.Identifier));
            }

            if (added.Count + updated.Count + oldAssetIds.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs
                    { Added = added, Removed = oldAssetIds, Updated = updated });
            }
        }

        public void QueueMissingCacheRefreshes()
        {
            var entries = ImportedAssetInfos;
            if (entries == null || entries.Count == 0 || m_AssetDataCacheManager == null)
                return;

            var assetDatasToRefresh = new List<AssetData>();

            foreach (var entry in entries)
            {
                if (entry?.AssetData is not AssetData ad || ad.Identifier == null)
                    continue;

                if (!m_AssetDataCacheManager.HasEntry(ad.Identifier.AssetId))
                    assetDatasToRefresh.Add(ad);
            }

            if (assetDatasToRefresh.Count > 0)
            {
                Utilities.DevLog($"Queueing background cache refresh for {assetDatasToRefresh.Count} tracked asset(s) without cache data...", highlight: true);
                m_AssetDataCacheManager.QueueRefresh(assetDatasToRefresh);
            }
        }

        /// <summary>
        /// Adds or updates imported asset info and optionally updates the asset data cache.
        /// </summary>
        /// <param name="shouldUpdateCache">When true, write or update the asset data cache. When false (e.g. tracking file updated from FileWatcher), skip cache update so tracking-only data is not written; QueueRefresh will fill cache if needed.</param>
        public void AddOrUpdateGuidsToImportedAssetInfo(BaseAssetData assetData,
            IReadOnlyCollection<ImportedFileInfo> fileInfos,
            bool shouldUpdateCache = true)
        {
            if (assetData == null)
                return;

            var ad = assetData as AssetData;
            if (!shouldUpdateCache && ad != null)
                m_AssetDataCacheManager?.PopulateFromCache(ad);

            var added = new HashSet<TrackedAssetIdentifier>();
            var updated = new HashSet<TrackedAssetIdentifier>();

            var id = new TrackedAssetIdentifier(assetData.Identifier);
            var info = new ImportedAssetInfo(assetData, fileInfos ?? Array.Empty<ImportedFileInfo>());

            if (GetImportedAssetInfo(assetData.Identifier) == null)
            {
                added.Add(id);
            }
            else
            {
                updated.Add(id);
            }

            AddOrUpdateImportedAssetInfo(info);

            if (shouldUpdateCache && ad != null)
                m_AssetDataCacheManager?.WriteOrUpdateEntry(ad);

            if (added.Count + updated.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs {Added = added, Updated = updated});
            }
        }

        public void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove)
        {
            guidsToRemove ??= Array.Empty<string>();
            if (guidsToRemove.Count <= 0)
                return;

            var updated = new List<TrackedAssetIdentifier>();
            var removed = new List<TrackedAssetIdentifier>();
            foreach (var fileGuid in guidsToRemove)
            {
                var assetInfos = GetImportedAssetInfosFromFileGuid(fileGuid);
                if (assetInfos == null)
                    continue;

                m_FileGuidToImportedAssetInfosMap.Remove(fileGuid);
                foreach (var asset in assetInfos)
                {
                    var id = new TrackedAssetIdentifier(asset.Identifier);

                    asset.FileInfos.RemoveAll(i => i.Guid == fileGuid);
                    if (asset.FileInfos.Count > 0)
                    {
                        updated.Add(id);
                    }
                    else
                    {
                        updated.Remove(id);
                        removed.Add(id);
                        m_TrackedIdentifierMap.Remove(id);

                        // Remove AssetDataCache entry when asset has no more files
                        m_AssetDataCacheManager?.RemoveEntry(asset.Identifier.AssetId);
                    }
                }
            }

            if (updated.Count + removed.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { Removed = removed, Updated = updated });
            }
        }

        public void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<BaseAssetData> assetDatas)
        {
            var updated = new HashSet<TrackedAssetIdentifier>();
            var added = new HashSet<TrackedAssetIdentifier>();

            if (assetDatas == null || !assetDatas.Any())
                return;

            foreach (var assetData in assetDatas)
            {
                var id = new TrackedAssetIdentifier(assetData.Identifier);

                bool wasExisting = m_AssetData.ContainsKey(id);

                // Unsubscribe from old asset data if it exists
                if (wasExisting && m_AssetData.TryGetValue(id, out var oldAssetData))
                {
                    UnsubscribeFromAssetDataEvents(id, oldAssetData);
                }

                m_AssetData[id] = assetData;

                // Subscribe to new asset data events
                SubscribeToAssetDataEvents(id, assetData);

                if (wasExisting)
                {
                    updated.Add(id);
                }
                else
                {
                    added.Add(id);
                }
            }

            if (added.Count + updated.Count > 0)
            {
                AssetDataChanged?.Invoke(new AssetChangeArgs {Added = added, Updated = updated});
            }
        }

        public ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier assetIdentifier)
            => GetImportedAssetInfo(new TrackedAssetIdentifier(assetIdentifier));

        public ImportedAssetInfo GetImportedAssetInfo(TrackedAssetIdentifier assetIdentifier)
        {
            return assetIdentifier?.IsIdValid() == true &&
                   m_TrackedIdentifierMap.TryGetValue(assetIdentifier,
                       out var result) ?
                result :
                null;
        }

        // Retrieve the asset identifier using the assetId
        public ImportedAssetInfo GetImportedAssetInfo(string assetId)
            => GetImportedAssetInfo(m_TrackedIdentifierMap.Keys.FirstOrDefault(x => x.AssetId == assetId));

        public void RemoveImportedAssetInfo(IEnumerable<AssetIdentifier> assetIdentifiers)
        {
            var idsToRemove = new List<TrackedAssetIdentifier>();

            foreach (var assetIdentifier in assetIdentifiers)
            {
                // Skip null identifiers
                if (assetIdentifier == null)
                {
                    continue;
                }

                var id = new TrackedAssetIdentifier(assetIdentifier);
                idsToRemove.Add(id);

                if (m_TrackedIdentifierMap.Remove(id, out var importedAssetInfo))
                {
                    // Remove all file guids related to that imported asset too
                    if (importedAssetInfo?.FileInfos != null)
                    {
                        foreach (var fileInfo in importedAssetInfo.FileInfos)
                        {
                            if (fileInfo == null || string.IsNullOrEmpty(fileInfo.Guid))
                                continue;

                            if (m_FileGuidToImportedAssetInfosMap.TryGetValue(fileInfo.Guid, out var importedAssetInfos))
                            {
                                var entry = importedAssetInfos.Find(info => info?.Identifier != null && info.Identifier.Equals(assetIdentifier));

                                if (entry == null)
                                    continue;

                                importedAssetInfos.Remove(entry);

                                if (importedAssetInfos.Count == 0)
                                {
                                    m_FileGuidToImportedAssetInfosMap.Remove(fileInfo.Guid);
                                }
                            }
                        }
                    }
                }

                // Unsubscribe from asset data events (even if not in m_TrackedIdentifierMap)
                if (m_AssetData.TryGetValue(id, out var assetData))
                {
                    UnsubscribeFromAssetDataEvents(id, assetData);
                }

                // Remove from asset data dictionary
                m_AssetData.Remove(id);

                // Remove AssetDataCache entry when asset is removed
                if (!string.IsNullOrEmpty(assetIdentifier.AssetId))
                {
                    m_AssetDataCacheManager?.RemoveEntry(assetIdentifier.AssetId);
                }
            }

            if (idsToRemove.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs {Removed = idsToRemove});
            }
        }

        public List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string guid)
        {
            return m_FileGuidToImportedAssetInfosMap.GetValueOrDefault(guid);
        }

        public string GetImportedFileGuid(AssetIdentifier assetIdentifier, string path)
        {
            if (assetIdentifier == null)
                return null;

            var importedInfo = GetImportedAssetInfo(assetIdentifier);
            var importedFileInfo = importedInfo?.FileInfos?.Find(f => Utilities.ComparePaths(f.OriginalPath, path));

            return importedFileInfo?.Guid;
        }

        public BaseAssetData GetAssetData(AssetIdentifier assetIdentifier)
        {
            if (assetIdentifier?.IsIdValid() != true)
            {
                return null;
            }

            var id = new TrackedAssetIdentifier(assetIdentifier);

            if (m_TrackedIdentifierMap.TryGetValue(id, out var info))
            {
                return info?.AssetData;
            }

            return m_AssetData.GetValueOrDefault(id);
        }

        public List<BaseAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids)
        {
            var result = new List<BaseAssetData>();

            foreach (var id in ids)
            {
                var assetData = GetAssetData(id);
                if (assetData != null)
                {
                    result.Add(assetData);
                }
            }

            return result;
        }

        public async Task<BaseAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var assetData = GetAssetData(assetIdentifier);

            if (assetData != null)
            {
                return assetData;
            }

            try
            {
                var assetProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                assetData = await assetProvider.GetAssetAsync(assetIdentifier, token);
            }
            catch (ForbiddenException)
            {
                // Unavailable
            }
            catch (NotFoundException)
            {
                // Not found
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (assetData != null)
            {
                AddOrUpdateAssetDataFromCloudAsset(new[] { assetData });
            }

            return assetData;
        }

        public bool IsInProject(AssetIdentifier id)
        {
            Utilities.DevAssert(!id.IsLocal(), "Calling IsInProject on a local identifier doesn't make sense.");

            if (id.IsLocal())
            {
                return false;
            }

            return GetImportedAssetInfo(id) != null;
        }

        // Returns the assets that are self-contained and have no dependencies on other assets
        // Return value includes the input assets
        public HashSet<AssetIdentifier> FindExclusiveDependencies(IEnumerable<AssetIdentifier> assetIdentifiersToDelete)
        {
            if (assetIdentifiersToDelete == null || !assetIdentifiersToDelete.Any())
            {
                return new HashSet<AssetIdentifier>();
            }

            var graph = BuildDependenciesGraph();
            var assetsToDelete = new HashSet<TrackedAssetIdentifier>(assetIdentifiersToDelete.Where(IsInProject)
                .Select(id => new TrackedAssetIdentifier(id)));
            var finalAssetsToDelete = new HashSet<TrackedAssetIdentifier>();

            foreach (var id in assetsToDelete)
            {
                if (graph.TryGetValue(id, out var value))
                    finalAssetsToDelete.UnionWith(DeleteNodeAndOrphanedDependencies(graph, value));
            }

            return finalAssetsToDelete.Select(a => m_TrackedIdentifierMap[a].Identifier).ToHashSet();
        }

        public void OnBeforeSerialize()
        {
            m_SerializedImportedAssetInfos = m_TrackedIdentifierMap.Values.ToArray();
            m_SerializedAssetData = m_AssetData.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var info in m_SerializedImportedAssetInfos)
            {
                AddOrUpdateImportedAssetInfo(info);
            }

            foreach (var data in m_SerializedAssetData)
            {
                var id = new TrackedAssetIdentifier(data.Identifier);
                m_AssetData[id] = data;

                // Subscribe to deserialized asset data events
                SubscribeToAssetDataEvents(id, data);
            }
        }

        Dictionary<TrackedAssetIdentifier, Node> BuildDependenciesGraph()
        {
            var dependentsMap = m_TrackedIdentifierMap.DependentsMap;

            var graph = new Dictionary<TrackedAssetIdentifier, Node>();

            foreach (var id in m_TrackedIdentifierMap.Keys)
            {
                var node = new Node(id);
                graph[id] = node;
            }

            foreach (var id in m_TrackedIdentifierMap.Keys)
            {
                var node = graph[id];
                foreach (var dependency in m_TrackedIdentifierMap.GetDependencies(id))
                {
                    if (graph.TryGetValue(dependency, out var dependencyNode))
                    {
                        node.Dependencies.Add(dependencyNode);
                    }
                }

                foreach (var dependent in dependentsMap[id])
                {
                    if (graph.TryGetValue(dependent, out var dependentNode))
                    {
                        node.DependentBy.Add(dependentNode);
                    }
                }

                if(node.DependentBy.Count == 0)
                {
                    node.IsRoot = true;
                }
            }

            return graph;
        }

        IEnumerable<TrackedAssetIdentifier> DeleteNodeAndOrphanedDependencies(Dictionary<TrackedAssetIdentifier, Node> graph, Node nodeToDelete)
        {
            if (!graph.TryGetValue(nodeToDelete.Identifier, out _))
            {
                return new List<TrackedAssetIdentifier>();
            }

            var nodesToProcess = new HashSet<Node>(); // Track all nodes we need to evaluate
            var nodesToDelete = new HashSet<Node>(); // Track nodes that will be deleted
            var nodesToCheck = new HashSet<Node>(); // Track nodes that need to be checked for circular dependencies

            // Start with the initial node
            nodesToProcess.Add(nodeToDelete);
            nodesToDelete.Add(nodeToDelete);

            // First pass, remove all references to this node from nodes that depend on it
            foreach (var dependent in nodeToDelete.DependentBy)
            {
                dependent.Dependencies.Remove(nodeToDelete);
            }

            // Second pass: Collect all potentially orphaned nodes
            while (nodesToProcess.Count > 0)
            {
                var current = nodesToProcess.First();
                nodesToProcess.Remove(current);

                foreach (var dependency in current.Dependencies)
                {
                    // Remove the current node from dependency's reverse references
                    dependency.DependentBy.Remove(current);

                    // If this dependency has no other dependents, and we haven't processed it yet
                    if (dependency.DependentBy.Count == 0 && !nodesToDelete.Contains(dependency))
                    {
                        nodesToProcess.Add(dependency);
                        nodesToDelete.Add(dependency);
                    }
                    else
                    {
                        // Add to check for potential orphan loop later
                        nodesToCheck.Add(dependency);
                    }
                }
            }

            // Third pass: Check for orphan loops
            var nodesToCheckQueue = new Queue<Node>(nodesToCheck);
            while(nodesToCheckQueue.Count > 0)
            {
                var node = nodesToCheckQueue.Dequeue();
                var isRootFound = false;
                var visited = new HashSet<Node>();
                nodesToProcess.Add(node);

                while (nodesToProcess.Count > 0 && !isRootFound)
                {
                    var current = nodesToProcess.First();
                    nodesToProcess.Remove(current);
                    visited.Add(current);

                    foreach (var dependent in current.DependentBy)
                    {
                        if(dependent.IsRoot)
                        {
                            isRootFound = true;
                            break;
                        }

                        if(!visited.Contains(dependent))
                        {
                            nodesToProcess.Add(dependent);
                        }
                    }
                }

                // If it is not linked to a root node, it's an orphan node
                if(!isRootFound && !nodesToDelete.Contains(node))
                {
                    nodesToDelete.Add(node);

                    foreach (var dependency in node.Dependencies)
                    {
                        dependency.DependentBy.Remove(node);
                        nodesToCheckQueue.Enqueue(dependency);
                    }
                }
            }

            // Fourth pass: Clean up all references between nodes that will be deleted
            foreach (var node in nodesToDelete)
            {
                node.Dependencies.Clear();
                node.DependentBy.Clear();
            }

            foreach (var node in nodesToDelete)
            {
                graph.Remove(node.Identifier);
            }

            return nodesToDelete.Select(n => n.Identifier);
        }

        void AddOrUpdateImportedAssetInfo(ImportedAssetInfo info)
        {
            // This method updates both m_TrackedIdentifierMap and m_FileGuidToImportedAssetInfosMap.

            var trackId = new TrackedAssetIdentifier(info.Identifier);
            // Capture incoming files before merge so we only update the GUID map for them, not the full merged list.
            var filesToUpdateInGuidMap = info.FileInfos;

            // PersistenceV4 stores one file per tracking file, so file watcher events deliver
            // single-file ImportedAssetInfos. When an entry already exists for this asset,
            // merge the new file infos into it rather than replacing the entire entry.
            if (m_TrackedIdentifierMap.TryGetValue(trackId, out var existingInfo))
            {
                foreach (var newFileInfo in info.FileInfos)
                {
                    var existingIndex = existingInfo.FileInfos.FindIndex(f => f.Guid == newFileInfo.Guid);
                    if (existingIndex >= 0)
                        existingInfo.FileInfos[existingIndex] = newFileInfo;
                    else
                        existingInfo.FileInfos.Add(newFileInfo);
                }

                existingInfo.AssetData = info.AssetData;
                info = existingInfo;
            }

            // Update GUID map only for the incoming files (filesToUpdateInGuidMap), not the full merged list
            foreach (var fileInfo in filesToUpdateInGuidMap)
            {
                if (!m_FileGuidToImportedAssetInfosMap.TryGetValue(fileInfo.Guid, out var importedAssetInfos))
                {
                    m_FileGuidToImportedAssetInfosMap[fileInfo.Guid] = new List<ImportedAssetInfo> { info };
                }
                else
                {
                    var duplicate =
                        importedAssetInfos.Find(i => new TrackedAssetIdentifier(i.Identifier).Equals(trackId));
                    if (duplicate != null && !ReferenceEquals(duplicate, info))
                    {
                        importedAssetInfos.Remove(duplicate);
                    }

                    if (!importedAssetInfos.Contains(info))
                    {
                        importedAssetInfos.Add(info);
                    }
                }
            }

            m_TrackedIdentifierMap[trackId] = info;
        }

        /// <summary>
        /// Subscribes to AssetData.AssetDataChanged events for the given asset data.
        /// </summary>
        void SubscribeToAssetDataEvents(TrackedAssetIdentifier id, BaseAssetData assetData)
        {
            if (assetData == null || m_AssetDataCacheManager == null)
                return;

            // Unsubscribe if already subscribed
            if (m_AssetDataEventSubscriptions.TryGetValue(id, out var existingHandler))
            {
                assetData.AssetDataChanged -= existingHandler;
            }

            // Create new handler
            BaseAssetData.AssetDataChangedDelegate handler = (changedAssetData, eventType) =>
            {
                // Update cache only for tracked assets (those with imported asset info / tracking file)
                if (changedAssetData is AssetData assetDataTyped &&
                    GetImportedAssetInfo(assetDataTyped.Identifier) != null)
                {
                    m_AssetDataCacheManager.WriteEntryWithoutNotify(assetDataTyped);
                }
            };

            // Subscribe
            assetData.AssetDataChanged += handler;
            m_AssetDataEventSubscriptions[id] = handler;
        }

        /// <summary>
        /// Unsubscribes from AssetData.AssetDataChanged events for the given asset data.
        /// </summary>
        void UnsubscribeFromAssetDataEvents(TrackedAssetIdentifier id, BaseAssetData assetData)
        {
            if (assetData == null)
                return;

            if (m_AssetDataEventSubscriptions.TryGetValue(id, out var handler))
            {
                assetData.AssetDataChanged -= handler;
                m_AssetDataEventSubscriptions.Remove(id);
            }
        }
    }
}
