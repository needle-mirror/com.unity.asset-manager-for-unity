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
        public override bool DisplayTopBar => false;

        public override string Title => L10n.Tr("In Project");

        public InProjectPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
        }

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
                new LocalStatusFilter(this, m_ProjectOrganizationProvider),
                new UnityTypeFilter(this)
            };
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
            Utilities.DevLog($"Retrieving import data for {m_AssetDataManager.importedAssetInfos.Count} asset(s)...");

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

        protected override void OnLoadMoreSuccessCallBack()
        {
            pageFilters.EnableFilters(m_AssetList.Any());
            SetErrorOrMessageData(!m_AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty, ErrorOrMessageRecommendedAction.None);
        }
    }
}
