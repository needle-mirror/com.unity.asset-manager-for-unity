using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IAssetImportResolver
    {
        Task<IEnumerable<IAssetData>> Resolve(IEnumerable<IAssetData> assets, ImportOperation.ImportType importType,
            string importDestination, CancellationToken token);
    }

    class AssetImportResolver : IAssetImportResolver
    {
        readonly IAssetImportDecisionMaker m_DecisionMaker;

        internal AssetImportResolver()
        {
            m_DecisionMaker = new AssetImportDecisionMaker();
        }

        internal AssetImportResolver(IAssetImportDecisionMaker decisionMaker)
        {
            m_DecisionMaker = decisionMaker;
        }

        public async Task<IEnumerable<IAssetData>> Resolve(IEnumerable<IAssetData> assets,
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

                var dependencies = await FindDependencies(assetDataManager, assetsProvider, assets, importType, token);
                var assetsAndDependencies = assets.Union(dependencies).ToHashSet();

                if (!CheckIfAssetsAlreadyInProject(assetDataManager, assets, importDestination, assetsAndDependencies,
                        out var updatedAssetData))
                {
                    return assetsAndDependencies;
                }

                // Check if the assets have changes
                await updatedAssetData.CheckUpdatedAssetDataAsync(token);

                var resolutions = await m_DecisionMaker.ResolveConflicts(updatedAssetData);

                return resolutions?.Where(c => c.ResolutionSelection == ResolutionSelection.Replace)
                    .Select(c => c.AssetData);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        static bool CheckIfAssetsAlreadyInProject(IAssetDataManager assetDataManager, IEnumerable<IAssetData> assets,
            string importDestination, HashSet<IAssetData> assetsAndDependencies, out UpdatedAssetData updatedAssetData)
        {
            var settings = ServicesContainer.instance.Resolve<ISettingsManager>();

            updatedAssetData = new UpdatedAssetData();

            var isFoundAtLeastOne = false;
            foreach (var asset in assetsAndDependencies)
            {
                var isExisted = assetDataManager.IsInProject(asset.Identifier);

                var resolutionInfo = new AssetDataResolutionInfo {AssetData = asset, Existed = isExisted};

                if (!isExisted)
                {
                    foreach (var file in asset.SourceFiles.Where(f => FindByPath(importDestination, settings,asset, f)))
                    {
                        resolutionInfo.FileConflicts.Add(file);
                        isFoundAtLeastOne = true;
                    }
                }

                if (assets.Any(a => a.Identifier == asset.Identifier))
                {
                    updatedAssetData.Assets.Add(resolutionInfo);
                }
                else
                {
                    updatedAssetData.Dependants.Add(resolutionInfo);
                }

                isFoundAtLeastOne |= isExisted;
            }

            return isFoundAtLeastOne;
        }

        static bool FindByPath(string importDestination, ISettingsManager settings, IAssetData asset, IAssetDataFile file)
        {
            var destinationPath = importDestination;
            if (settings.IsSubfolderCreationEnabled)
            {
                var regex = new Regex(@"[\\\/:*?""<>|]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                destinationPath = Path.Combine(importDestination, $"{regex.Replace(asset.Name, "").Trim()}");
            }

            var filePath = Path.Combine(destinationPath, file.Path);
            return File.Exists(filePath);
        }

        static async Task<HashSet<IAssetData>> FindDependencies(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, IEnumerable<IAssetData> assetData, ImportOperation.ImportType importType,
            CancellationToken token)
        {
            var tasks = new List<Task<List<IAssetData>>>();

            foreach (var asset in assetData)
            {
                tasks.Add(SyncWithCloudAndGatherDependenciesAsync(assetDataManager, assetsProvider, asset, importType,
                    token));
            }

            await Task.WhenAll(tasks);

            var dependencies = new HashSet<IAssetData>();
            foreach (var task in tasks)
            {
                foreach (var asset in task.Result)
                {
                    dependencies.Add(asset);
                }
            }

            return dependencies;
        }

        static async Task<List<IAssetData>> SyncWithCloudAndGatherDependenciesAsync(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, IAssetData asset, ImportOperation.ImportType importType,
            CancellationToken token)
        {
            // Sync before fetching dependencies to ensure you have the latest dependencies
            switch (importType)
            {
                case ImportOperation.ImportType.Import:
                    await asset.SyncWithCloudAsync(null, token);
                    break;

                default:
                    await asset.SyncWithCloudLatestAsync(null, token);
                    break;
            }

            // Fetch dependencies
            return await GetDependenciesAsync(assetDataManager, assetsProvider, asset, token);
        }

        static async Task<List<IAssetData>> GetDependenciesAsync(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, IAssetData asset, CancellationToken token)
        {
            var totalDependencies = asset.Dependencies.Count();
            var loadedDependencies = 0;

            var loadDependenciesOperation = new LoadDependenciesOperation();
            loadDependenciesOperation.Start();

            token.Register(() =>
            {
                loadDependenciesOperation.Finish(OperationStatus.Cancelled);
            });

            var dependencies = new List<IAssetData>();
            var addedDependencies = new HashSet<AssetIdentifier>();

            await foreach (var dependencyAssetResult in LoadDependenciesRecursivelyAsync(assetDataManager,
                               assetsProvider, asset, addedDependencies, token))
            {
                if (dependencyAssetResult.AssetData != null)
                {
                    dependencies.Add(dependencyAssetResult.AssetData);
                }
                else
                {
                    Debug.LogError($"Failed to import asset dependency '{dependencyAssetResult.Identifier}'");
                }

                if (asset.Dependencies.Any(x => x.Equals(dependencyAssetResult.Identifier)))
                {
                    loadedDependencies++;
                }

                if (totalDependencies > 0)
                {
                    loadDependenciesOperation.Report(new LoadDependenciesProgress(string.Empty,
                        (float) loadedDependencies / totalDependencies));
                }
            }

            loadDependenciesOperation.Finish(OperationStatus.Success);

            return dependencies;
        }

        static async IAsyncEnumerable<DependencyAssetResult> LoadDependenciesRecursivelyAsync(
            IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IAssetData assetData,
            HashSet<AssetIdentifier> addedDependencies, [EnumeratorCancellation] CancellationToken token)
        {
            foreach (var dependency in assetData.Dependencies)
            {
                // Avoid circular dependencies
                if (!addedDependencies.Add(dependency))
                {
                    continue;
                }

                addedDependencies.Add(dependency);

                var dependencyAsset = await FindAssetAsync(dependency, assetDataManager, assetsProvider, token);

                if (dependencyAsset == null)
                {
                    continue;
                }

                await dependencyAsset.RefreshDependenciesAsync(token);

                var result = new DependencyAssetResult(dependencyAsset);
                yield return result;

                await foreach (var childDependency in LoadDependenciesRecursivelyAsync(assetDataManager, assetsProvider,
                                   dependencyAsset,
                                   addedDependencies, token))
                {
                    yield return childDependency;
                }
            }
        }

        static async Task<AssetData> FindAssetAsync(AssetIdentifier assetIdentifier, IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, CancellationToken token)
        {
            // Search for the asset in the local cache, the versions must match; otherwise fetch the asset from the cloud
            if (assetDataManager.GetAssetData(assetIdentifier) is AssetData assetData
                && assetData.Identifier.Version == assetIdentifier.Version)
            {
                return assetData;
            }

            try
            {
                // Try to fetch the asset from the current project
                return await assetsProvider.GetAssetAsync(assetIdentifier, token);
            }
            catch (Exception)
            {
                // If it fails, search for the asset in any project
                return await assetsProvider.FindAssetAsync(assetIdentifier.OrganizationId, assetIdentifier.AssetId,
                    assetIdentifier.Version, token);
            }
        }
    }
}
