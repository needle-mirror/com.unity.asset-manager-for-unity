using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.Core.Editor
{
    class UpdatedAssetData
    {
        readonly List<AssetDataResolutionInfo> m_Assets = new();
        readonly List<AssetDataResolutionInfo> m_Dependants = new();
        readonly List<BaseAssetData> m_UpwardDependencies = new();

        public List<AssetDataResolutionInfo> Assets => m_Assets;
        public List<AssetDataResolutionInfo> Dependants => m_Dependants;
        public List<BaseAssetData> UpwardDependencies => m_UpwardDependencies;

        public async Task CheckUpdatedAssetDataAsync(CancellationToken token)
        {
            if (m_Assets.Count == 0 && m_Dependants.Count == 0)
                return;

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var assetAndDependantInfos = m_Assets.Union(m_Dependants).ToList();

            var tasks = new List<Task>();
            foreach (var assetDataInfo in assetAndDependantInfos)
            {
                tasks.Add(assetDataInfo.GatherFileConflictsAsync(assetDataManager, token));
            }

            tasks.Add(CheckUpdatedAssetUpwardDependenciesAsync(token));

            await Task.WhenAll(tasks);
        }

        Task CheckUpdatedAssetUpwardDependenciesAsync(CancellationToken token)
        {
            // TODO: Complete when the dependency system is implemented properly in the cloud backend
            m_UpwardDependencies.Clear();
            return Task.CompletedTask;
        }
    }
}
