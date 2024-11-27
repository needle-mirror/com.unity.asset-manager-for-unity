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

                var assetsAndDependencies = await SyncWithCloudAndGatherDependenciesAsync(assetDataManager, assetsProvider, assets, importType, token);

                if (!CheckIfAssetsAlreadyInProject(assetDataManager, assets, importDestination, assetsAndDependencies,
                        out var updatedAssetData))
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

                if (assets.Any(a => TrackedAssetIdentifier.IsFromSameAsset(a.Identifier, asset.Identifier)))
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

        static bool FindByPath(string importDestination, ISettingsManager settings, BaseAssetData asset, BaseAssetDataFile file)
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

        static async Task<HashSet<BaseAssetData>> SyncWithCloudAndGatherDependenciesAsync(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, IEnumerable<BaseAssetData> assetData, ImportOperation.ImportType importType,
            CancellationToken token)
        {
            var tasks = new List<Task<List<BaseAssetData>>>();

            foreach (var asset in assetData)
            {
                tasks.Add(SyncWithCloudAndGatherDependenciesAsync(assetDataManager, assetsProvider, asset, importType,
                    token));
            }

            await Task.WhenAll(tasks);

            var dependencies = new HashSet<BaseAssetData>();
            foreach (var task in tasks)
            {
                foreach (var asset in task.Result)
                {
                    var same = dependencies.FirstOrDefault(a => TrackedAssetIdentifier.IsFromSameAsset(a.Identifier, asset.Identifier));
                    if (same != null)
                    {
                        if (same.SequenceNumber >= asset.SequenceNumber)
                            continue;

                        dependencies.Remove(same);
                    }

                    dependencies.Add(asset);
                }
            }

            return dependencies;
        }

        static async Task<List<BaseAssetData>> SyncWithCloudAndGatherDependenciesAsync(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, BaseAssetData asset, ImportOperation.ImportType importType,
            CancellationToken token)
        {
            asset = await SyncWithCloudAsync(assetsProvider, asset, importType, token);

            return await GetDependenciesAsync(assetDataManager, assetsProvider, asset, token);
        }

        static async Task<BaseAssetData> SyncWithCloudAsync(IAssetsProvider assetsProvider, BaseAssetData asset,
            ImportOperation.ImportType importType, CancellationToken token)
        {
            // Populate the latest asset data
            asset = importType switch
            {
                ImportOperation.ImportType.Import => await assetsProvider.GetAssetAsync(asset.Identifier, token),
                _ => await assetsProvider.GetLatestAssetVersionAsync(asset.Identifier, token)
            };

            var tasks = new List<Task>
            {
                asset.ResolvePrimaryExtensionAsync(null, token),
                asset.RefreshDependenciesAsync(token)
            };
            await Task.WhenAll(tasks);

            return asset;
        }

        static async Task<List<BaseAssetData>> GetDependenciesAsync(IAssetDataManager assetDataManager,
            IAssetsProvider assetsProvider, BaseAssetData asset, CancellationToken token)
        {
            var totalDependencies = asset.Dependencies.Count();
            var loadedDependencies = 0;

            var loadDependenciesOperation = new LoadDependenciesOperation();
            loadDependenciesOperation.Start();

            token.Register(() =>
            {
                loadDependenciesOperation.Finish(OperationStatus.Cancelled);
            });

            // Add the asset iteself to the list.
            var dependencies = new List<BaseAssetData> {asset};
            var addedDependencies = new HashSet<AssetIdentifier> {asset.Identifier};

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
            IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, BaseAssetData assetData,
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
