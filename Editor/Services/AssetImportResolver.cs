using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IAssetImportResolver
    {
        Task<IEnumerable<IAssetData>> Resolve(IEnumerable<IAssetData> assets, ImportOperation.ImportType importType, string importDestination, CancellationToken token);
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

        public async Task<IEnumerable<IAssetData>> Resolve(IEnumerable<IAssetData> assets, ImportOperation.ImportType importType, string importDestination, CancellationToken token)
        {
            try
            {
                if (assets == null || !assets.Any())
                {
                    return null;
                }

                var dependencies = await FindDependencies(assets, importType, token);
                var assetsAndDependencies = assets.Union(dependencies).ToHashSet();

                if (!CheckIfAssetsAlreadyInProject(assets, importDestination, assetsAndDependencies, out var updatedAssetData))
                {
                    return assetsAndDependencies;
                }

                // Check if the assets have changes
                var hasChanges = await updatedAssetData.CheckUpdatedAssetDataAsync(token);

                // TODO: Change to this when the conflict detection is implemented
                // if (!hasChanges)
                if (!hasChanges && !updatedAssetData.Assets.Exists(a => a.Existed))
                {
                    return assetsAndDependencies;
                }

                var resolutions = await m_DecisionMaker.ResolveConflicts(updatedAssetData);

                return resolutions?.Where(c => c.ResolutionSelection == ResolutionSelection.Replace).Select(c => c.AssetData);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        static bool CheckIfAssetsAlreadyInProject(IEnumerable<IAssetData> assets, string importDestination, HashSet<IAssetData> assetsAndDependencies, out UpdatedAssetData updatedAssetData)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var settings = ServicesContainer.instance.Resolve<ISettingsManager>();

            updatedAssetData = new UpdatedAssetData();

            var isFoundAtLeastOne = false;
            foreach (var asset in assetsAndDependencies)
            {
                var isExisted = assetDataManager.IsInProject(asset.Identifier);

                // TODO: Review with the team if we should remove this
                // If not found by Guid, try to find by path/filename
                // if (!isExisted)
                // {
                //     isExisted = FindByPath(importDestination, settings, asset);
                // }

                var resolutionInfo = new AssetDataResolutionInfo { AssetData = asset, Existed = isExisted };
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

        static bool FindByPath(string importDestination, ISettingsManager settings, IAssetData asset)
        {
            var isExisted = false;
            var destinationPath = importDestination;
            if (settings.IsSubfolderCreationEnabled)
            {
                var regex = new Regex(@"[\\\/:*?""<>|]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                destinationPath = Path.Combine(importDestination, $"{regex.Replace(asset.Name, "").Trim()}");
            }

            foreach (var file in asset.SourceFiles)
            {
                var filePath = Path.Combine(destinationPath, file.Path);
                if (File.Exists(filePath))
                {
                    isExisted = true;
                    break;
                }
            }

            return isExisted;
        }

        static async Task<HashSet<IAssetData>> FindDependencies(IEnumerable<IAssetData> assetData, ImportOperation.ImportType importType, CancellationToken token)
        {
            var tasks = new List<Task<List<IAssetData>>>();

            foreach (var asset in assetData)
            {
                tasks.Add(SyncWithCloudAndGatherDependencies(asset, importType, token));
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

        static async Task<List<IAssetData>> SyncWithCloudAndGatherDependencies(IAssetData asset, ImportOperation.ImportType importType, CancellationToken token)
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
            return await GetDependenciesAsync(asset);
        }

        static async Task<List<IAssetData>> GetDependenciesAsync(IAssetData asset)
        {
            var dependencies = new List<IAssetData>();

            await foreach (var dependencyAssetData in AssetDataDependencyHelper.LoadDependenciesAsync(asset,
                               true, CancellationToken.None))
            {
                if (dependencyAssetData.AssetData != null)
                {
                    dependencies.Add(dependencyAssetData.AssetData);
                }
                else
                {
                    Debug.LogError($"Failed to import asset dependency '{dependencyAssetData.Identifier}'");
                }
            }

            return dependencies;
        }
    }
}
