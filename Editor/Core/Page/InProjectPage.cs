using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class InProjectPage : BasePage
    {
        public override PageType pageType => PageType.InProject;

        public InProjectPage(IAssetDataManager assetDataManager) : base(assetDataManager)
        {
            ResolveDependencies(assetDataManager);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!isActivePage)
                return;

            var keepSelection = !args.removed.Any(a => a.Equals(selectedAssetId));
            Clear(true, keepSelection);
        }

        protected override async IAsyncEnumerable<IAssetData> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            if (EditorPrefs.GetBool("DeveloperMode", false))
            {
                Debug.Log($"Retrieving import data for {m_AssetDataManager.importedAssetInfos.Count} asset(s)...");
            }
            
            foreach (var importedAsset in m_AssetDataManager.importedAssetInfos)
            {		  
                var assetData = importedAsset.assetData;
                if (assetData == null) // Can happen with corrupted serialization
                    continue;

                if (await IsDiscardedByLocalFilter(assetData))
                    continue;
                
                yield return assetData;
            }
            
            m_HasMoreItems = false;

            await Task.CompletedTask; // Remove warning about async
        }

        protected override void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers)
        {
            SetErrorOrMessageData(!m_AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty, ErrorOrMessageRecommendedAction.None);
        }
    }
}
