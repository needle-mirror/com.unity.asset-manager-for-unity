using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    class UpdatedAssetData
    {
        readonly List<AssetDataResolutionInfo> m_Assets = new();
        readonly List<AssetDataResolutionInfo> m_Dependants = new();
        readonly List<IAssetData> m_UpwardDependencies = new();

        public List<AssetDataResolutionInfo> Assets => m_Assets;
        public List<AssetDataResolutionInfo> Dependants => m_Dependants;
        public List<IAssetData> UpwardDependencies => m_UpwardDependencies;

        public async Task<bool> CheckUpdatedAssetDataAsync(CancellationToken token)
        {
            if (m_Assets.Count == 0)
                return false;

            var tasks = new List<Task<bool>>();
            var assetAndDependantInfos = m_Assets.Union(m_Dependants).ToList();
            foreach (var assetDataInfo in assetAndDependantInfos)
            {
                tasks.Add(assetDataInfo.CheckUpdatedAssetDataUpToDateAsync(token));
                tasks.Add(assetDataInfo.CheckUpdatedAssetDataConflictsAsync(token));
            }

            tasks.Add(CheckUpdatedAssetUpwardDependenciesAsync(token));

            await Task.WhenAll(tasks);

            return tasks.Exists(t => t.Result);
        }

        Task<bool> CheckUpdatedAssetUpwardDependenciesAsync(CancellationToken token)
        {
            // TODO: Complete when the dependency system is implemented properly in the cloud backend
            return Task.FromResult(false);
        }
    }
}
