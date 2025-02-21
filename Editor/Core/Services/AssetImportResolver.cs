using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IAssetImportDecisionMaker
    {
        Task<IEnumerable<ResolutionData>> ResolveConflicts(UpdatedAssetData data);
    }

    interface IAssetImportResolver : IService
    {
        Task<IEnumerable<BaseAssetData>> Resolve(IEnumerable<BaseAssetData> assets, ImportOperation.ImportType importType,
            string importDestination, CancellationToken token);

        void SetConflictResolver(IAssetImportDecisionMaker conflictResolver);
    }

    enum ResolutionSelection
    {
        Replace,
        Ignore
    }

    class ResolutionData
    {
        public BaseAssetData AssetData;
        public ResolutionSelection ResolutionSelection;
    }

    class AssetImportResolver : BaseService<IAssetImportResolver>, IAssetImportResolver
    {
        [Serializable]
        readonly struct DependencyNode
        {
            // Any node with a null m_AssetData should be trashed on Domain Reload.
            readonly BaseAssetData m_AssetData;

            // Not started => -1
            // Started => 0
            // Completed => 1
            readonly int m_DependenciesTraversalState;

            public BaseAssetData AssetData => m_AssetData;
            public bool IsDependencyTraversalStarted => m_DependenciesTraversalState >= 0;
            public bool IsDependencyTraversalCompleted => m_DependenciesTraversalState == 1;

            public DependencyNode(BaseAssetData assetData, int dependenciesTraversalState = -1)
            {
                m_AssetData = assetData;
                m_DependenciesTraversalState = dependenciesTraversalState;
            }
        }

        IAssetImportDecisionMaker m_ConflictResolver;

        public void SetConflictResolver(IAssetImportDecisionMaker conflictResolver)
        {
            m_ConflictResolver = conflictResolver;
        }

        public async Task<IEnumerable<BaseAssetData>> Resolve(IEnumerable<BaseAssetData> assets,
            ImportOperation.ImportType importType, string importDestination, CancellationToken token)
        {
            try
            {
                if (assets == null || !assets.Any())
                {
                    return null;
                }

                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

                var assetsAndDependencies = await GetUpdatedAssetDataAndDependenciesAsync(assetsProvider, assets, importType, token);

                if (!CheckIfAssetsAlreadyInProject(assetDataManager, assets, importDestination, assetsAndDependencies, out var updatedAssetData))
                {
                    return assetsAndDependencies;
                }

                // Check if the assets have changes
                await updatedAssetData.CheckUpdatedAssetDataAsync(token);

                Utilities.DevAssert(m_ConflictResolver != null);

                if (m_ConflictResolver == null)
                {
                    // In case there is no decision maker, we will just replace and reimport all the assets
                    return updatedAssetData.Assets.Select(c => c.AssetData);
                }

                var resolutions = await m_ConflictResolver.ResolveConflicts(updatedAssetData);
                return resolutions?.Where(c => c.ResolutionSelection == ResolutionSelection.Replace)
                    .Select(c => c.AssetData);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        static bool CheckIfAssetsAlreadyInProject(IAssetDataManager assetDataManager, IEnumerable<BaseAssetData> assets,
            string importDestination, HashSet<BaseAssetData> assetsAndDependencies, out UpdatedAssetData updatedAssetData)
        {
            var settings = ServicesContainer.instance.Resolve<ISettingsManager>();

            updatedAssetData = new UpdatedAssetData();

            var isFoundAtLeastOne = false;
            foreach (var asset in assetsAndDependencies)
            {
                var resolutionInfo = new AssetDataResolutionInfo(asset, assetDataManager);

                resolutionInfo.GatherFileConflicts(settings, importDestination);
                isFoundAtLeastOne |= resolutionInfo.HasConflicts;

                if (assets.Any(a => TrackedAssetIdentifier.IsFromSameAsset(a.Identifier, asset.Identifier)))
                {
                    updatedAssetData.Assets.Add(resolutionInfo);
                }
                else
                {
                    updatedAssetData.Dependants.Add(resolutionInfo);
                }

                isFoundAtLeastOne |= resolutionInfo.Existed;
            }

            return isFoundAtLeastOne;
        }

        static async Task<HashSet<BaseAssetData>> GetUpdatedAssetDataAndDependenciesAsync(
            IAssetsProvider assetsProvider, IEnumerable<BaseAssetData> assetDatas,
            ImportOperation.ImportType importType, CancellationToken token)
        {
            assetDatas = await GetUpdatedAssetDataAsync(assetsProvider, assetDatas.Select(x => x.Identifier),
                importType, token);

#if AM4U_DEV
            var t = new Stopwatch();
            t.Start();
#endif

            // Key = "project-Id/asset-Id"
            // Value = the AssetData with the highest SequenceNumber
            // DOMAIN_RELOAD : serializing this dictionary would allow recovery
            var dependencies = new Dictionary<string, DependencyNode>();
            foreach (var assetData in assetDatas)
            {
                dependencies[BuildDependencyKey(assetData)] = new DependencyNode(assetData);
            }

            var depTasks = new List<Task>();
            foreach (var asset in assetDatas)
            {
                depTasks.Add(GetDependenciesRecursivelyAsync(assetsProvider, importType, asset, dependencies, token));
            }

            await Task.WhenAll(depTasks);

#if AM4U_DEV
            t.Stop();
            Utilities.DevLog($"Took {t.ElapsedMilliseconds / 1000f:F2} s to gather dependencies");
#endif

            return dependencies.Select(x => x.Value.AssetData).ToHashSet();
        }

        static async Task GetDependenciesRecursivelyAsync(IAssetsProvider assetsProvider,
            ImportOperation.ImportType importType, BaseAssetData root, Dictionary<string, DependencyNode> assetDatas,
            CancellationToken token)
        {
            var key = BuildDependencyKey(root);

            // Check if the root asset data is already being traversed
            lock (assetDatas)
            {
                if (assetDatas.TryGetValue(key, out var rootNode))
                {
                    if (rootNode.IsDependencyTraversalStarted)
                        return;

                    root = ChooseLatest(rootNode.AssetData, root);
                }

                assetDatas[key] = new DependencyNode(root, 0);
            }

            // List dependencies, but only retain those that are not already in the dictionary
            var dependencyIdentifiers = new List<AssetIdentifier>();
            foreach (var assetIdentifier in root.Dependencies)
            {
                var dependencyKey = BuildDependencyKey(assetIdentifier);
                lock (assetDatas)
                {
                    // If the dependency is already in the dictionary, the node has been visited.
                    if (assetDatas.ContainsKey(dependencyKey))
                    {
                        Utilities.DevLog("Skipping dependency.");
                        continue;
                    }

                    assetDatas[dependencyKey] = new DependencyNode(null);
                }

                dependencyIdentifiers.Add(assetIdentifier);
            }

            if (dependencyIdentifiers.Count == 0)
                return;

            // Make sure those dependencies are the most up to date.
            var dependencies =
                (await GetUpdatedAssetDataAsync(assetsProvider, dependencyIdentifiers, importType, token)).ToArray();

            // Create new entries for each dependency
            for (var i = 0; i < dependencies.Length; ++i)
            {
                var temp = dependencies[i];

                var dependencyKey = BuildDependencyKey(temp);
                lock (assetDatas)
                {
                    if (assetDatas.TryGetValue(dependencyKey, out var dependencyNode))
                    {
                        if (dependencyNode.IsDependencyTraversalStarted)
                            continue;

                        dependencies[i] = ChooseLatest(dependencyNode.AssetData, temp);
                    }

                    assetDatas[key] = new DependencyNode(temp, 0);
                }
            }

            // DOMAIN_RELOAD : this is useful to track which nodes will need to be re-traversed
            // Once all dependency nodes are setup, update the root node
            lock (assetDatas)
            {
                assetDatas[key] = new DependencyNode(root, 1);
            }

            // Start tasks to traverse each dependency
            var tasks = new List<Task>();

            foreach (var dependency in dependencies)
            {
                tasks.Add(GetDependenciesRecursivelyAsync(assetsProvider, importType, dependency, assetDatas, token));
            }

            await Task.WhenAll(tasks);
        }

        static async Task<IEnumerable<BaseAssetData>> GetUpdatedAssetDataAsync(IAssetsProvider assetsProvider,
            IEnumerable<AssetIdentifier> assetIdentifiers, ImportOperation.ImportType importType, CancellationToken token)
        {
#if AM4U_DEV
            var t = new Stopwatch();
            t.Start();
#endif

            var assetDatas = await SearchUpdatedAssetDataAsync(assetsProvider, assetIdentifiers, importType, token);

#if AM4U_DEV
            Utilities.DevLog($"Took {t.ElapsedMilliseconds / 1000f:F2} s to update {assetDatas.Count()} assets.");
            t.Restart();
#endif

            var updateTasks = new List<Task>();
            foreach (var asset in assetDatas)
            {
                // Updates asset file list
                updateTasks.Add(asset.ResolveDatasetsAsync(token));

                // Updates dependency list
                updateTasks.Add(asset.RefreshDependenciesAsync(token));
            }

            await Task.WhenAll(updateTasks);

#if AM4U_DEV
            t.Stop();
            Utilities.DevLog($"Took {t.ElapsedMilliseconds / 1000f:F2} s for non-batchable update asset calls.");
#endif

            return assetDatas;
        }

        static async Task<IEnumerable<BaseAssetData>> SearchUpdatedAssetDataAsync(IAssetsProvider assetsProvider,
            IEnumerable<AssetIdentifier> assetIdentifiers, ImportOperation.ImportType importType, CancellationToken token)
        {
            // Split the searches by organization

            var assetsByOrg = new Dictionary<string, List<AssetIdentifier>>();
            foreach (var assetIdentifier in assetIdentifiers)
            {
                if (string.IsNullOrEmpty(assetIdentifier.OrganizationId))
                    continue;

                if (!assetsByOrg.ContainsKey(assetIdentifier.OrganizationId))
                {
                    assetsByOrg.Add(assetIdentifier.OrganizationId, new List<AssetIdentifier>());
                }

                assetsByOrg[assetIdentifier.OrganizationId].Add(assetIdentifier);
            }

            if (assetsByOrg.Count > 1)
            {
                Utilities.DevLog("Initiating search in multiple organizations.");
            }

            var tasks = assetsByOrg
                .Select(kvp => SearchUpdatedAssetDataAsync(assetsProvider, kvp.Key, kvp.Value, importType, token))
                .ToArray();

            await Task.WhenAll(tasks);

            var targetAssetDatas = new List<BaseAssetData>();

            foreach (var task in tasks)
            {
                targetAssetDatas.AddRange(task.Result);
            }

            return targetAssetDatas;
        }

        static async Task<IEnumerable<BaseAssetData>> SearchUpdatedAssetDataAsync(IAssetsProvider assetsProvider,
            string organizationId, List<AssetIdentifier> assetIdentifiers, ImportOperation.ImportType importType,
            CancellationToken token)
        {
            // If there is only 1 asset, fetch that asset info directly (search has more overhead than a direct fetch).
            if (assetIdentifiers.Count == 1)
            {
                var identifier = assetIdentifiers[0];
                var asset = importType switch
                {
                    ImportOperation.ImportType.Import =>
                        await assetsProvider.GetAssetAsync(identifier, token),
                    _ => await assetsProvider.GetLatestAssetVersionAsync(identifier, token)
                };

                return new[] {asset};
            }

            // Split the asset list into chunks for multiple searches.

            var tasks = new List<Task<IEnumerable<BaseAssetData>>>();
            var startIndex = 0;
            while (startIndex < assetIdentifiers.Count)
            {
                var maxCount = Math.Min(assetsProvider.DefaultSearchPageSize, assetIdentifiers.Count - startIndex);

                var assetIdentifierRange = assetIdentifiers.GetRange(startIndex, maxCount);
                var searchFilter = BuildSearchFilter(assetIdentifierRange, importType);
                tasks.Add(SearchUpdatedAssetDataAsync(assetsProvider, organizationId, searchFilter, assetIdentifierRange, token));

                startIndex += assetsProvider.DefaultSearchPageSize;
            }

            await Task.WhenAll(tasks);

            var targetAssetDatas = new List<BaseAssetData>();

            foreach (var task in tasks)
            {
                targetAssetDatas.AddRange(task.Result);
            }

            return targetAssetDatas;
        }

        static async Task<IEnumerable<BaseAssetData>> SearchUpdatedAssetDataAsync(IAssetsProvider assetsProvider,
            string organizationId, AssetSearchFilter assetSearchFilter, IEnumerable<AssetIdentifier> assetIdentifiers,
            CancellationToken token)
        {
            var validAssetIds = assetIdentifiers.Select(x => x.AssetId).ToHashSet();

            var query = assetsProvider.SearchAsync(organizationId, null, assetSearchFilter,
                SortField.Name, SortingOrder.Ascending, 0, 0, token);

            var assets = new List<BaseAssetData>();

            await foreach (var asset in query)
            {
                // Ignore any false positive result.
                if (!validAssetIds.Contains(asset.Identifier.AssetId))
                {
                    Utilities.DevLogWarning($"Skipping false positive search result {asset.Name}.");
                    continue;
                }

                assets.Add(asset);

                if (assets.Count > assetsProvider.DefaultSearchPageSize)
                {
                    Utilities.DevLogWarning("Exceeding the expected number of searched assets.");
                    break;
                }
            }

            return assets;
        }

        static AssetSearchFilter BuildSearchFilter(IEnumerable<AssetIdentifier> assetIdentifiers, ImportOperation.ImportType importType)
        {
            if (!assetIdentifiers.Any())
            {
                throw new ArgumentException("Search list cannot be empty.", nameof(assetIdentifiers));
            }

            var assetSearchFilter = new AssetSearchFilter();

            switch (importType)
            {
                // Search by version specifically
                case ImportOperation.ImportType.Import:
                    assetSearchFilter.AssetVersions = new List<string>(assetIdentifiers.Select(x => x.Version));
                    break;

                // Search by assetId
                case ImportOperation.ImportType.UpdateToLatest:
                    assetSearchFilter.AssetIds = new List<string>(assetIdentifiers.Select(x => x.AssetId));
                    break;
            }

            return assetSearchFilter;
        }

        static string BuildDependencyKey(BaseAssetData assetData)
        {
            return BuildDependencyKey(assetData.Identifier);
        }

        static string BuildDependencyKey(AssetIdentifier assetIdentifier)
        {
            return $"{assetIdentifier.ProjectId}/{assetIdentifier.AssetId}";
        }

        static BaseAssetData ChooseLatest(BaseAssetData a, BaseAssetData b)
        {
            if (a == null) return b;
            if (b == null) return a;

            if (a.SequenceNumber > b.SequenceNumber)
                return a;

            if (a.SequenceNumber < b.SequenceNumber)
                return b;

            return a.Updated > b.Updated ? a : b;
        }
    }
}
