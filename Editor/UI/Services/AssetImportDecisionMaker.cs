using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
#if UNITY_STANDALONE_OSX
using UnityEditor;
#endif

namespace Unity.AssetManager.UI.Editor
{
    class AssetImportDecisionMaker : IAssetImportDecisionMaker
    {
        public Task<IEnumerable<ResolutionData>> ResolveConflicts(UpdatedAssetData data)
        {
            TaskCompletionSource<IEnumerable<ResolutionData>> tcs = new();

            var assetOperationManager = ServicesContainer.instance.Resolve<IAssetOperationManager>();
            assetOperationManager.PauseAllOperations();

#if UNITY_STANDALONE_OSX
            EditorApplication.delayCall += () =>
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
