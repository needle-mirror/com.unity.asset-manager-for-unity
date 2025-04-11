using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
#if UNITY_STANDALONE_OSX
using UnityEditor;
#endif

namespace Unity.AssetManager.UI.Editor
{
    class AssetImportDecisionMaker : IAssetImportDecisionMaker
    {
        readonly ISettingsManager m_SettingsManager;

        public AssetImportDecisionMaker(ISettingsManager settingsManager)
        {
            m_SettingsManager = settingsManager;
        }

        public Task<IEnumerable<ResolutionData>> ResolveConflicts(UpdatedAssetData data)
        {
            if (m_SettingsManager.IsReimportModalDisabled)
            {
                var allAssets = data.Assets.Union(data.Dependants);
                var resolutions = allAssets.Select(asset => new ResolutionData() { AssetData = asset.AssetData, ResolutionSelection = ResolutionSelection.Replace }).ToList();
                return Task.FromResult<IEnumerable<ResolutionData>>(resolutions);
            }

            TaskCompletionSource<IEnumerable<ResolutionData>> tcs = new();

            var assetOperationManager = ServicesContainer.instance.Resolve<IAssetOperationManager>();
            assetOperationManager.PauseAllOperations();

#if UNITY_STANDALONE_OSX
            var application = ServicesContainer.instance.Resolve<IApplicationProxy>();
            application.DelayCall += () =>
            {
#endif
            ReimportWindow.CreateModalWindow(data, resolutions =>
                {
                    assetOperationManager.ResumeAllOperations();
                    tcs.SetResult(resolutions);
                },
                () =>
                {
                    assetOperationManager.ResumeAllOperations();
                    tcs.SetResult(null);
                });
#if UNITY_STANDALONE_OSX
            };
#endif
            return tcs.Task;
        }
    }
}
