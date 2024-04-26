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
    class InProjectPage : BasePage
    {
        public override bool DisplayTopBar => false;

        public override string Title => L10n.Tr("In Project");

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
                new LocalStatusFilter(this, m_ProjectOrganizationProvider),
                new LocalUnityTypeFilter(this)
            };
        }

        public InProjectPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider) { }


        public override void OnEnable()
        {
            base.OnEnable();
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!IsActivePage)
                return;

            var keepSelection = !args.Removed.Any(a => a.Equals(LastSelectedAssetId));
            Clear(true, keepSelection);
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog($"Retrieving import data for {m_AssetDataManager.ImportedAssetInfos.Count} asset(s)...");

            foreach (var importedAsset in m_AssetDataManager.ImportedAssetInfos)
            {
                var assetData = importedAsset.AssetData;
                if (assetData == null) // Can happen with corrupted serialization
                    continue;

                if (await IsDiscardedByLocalFilter(assetData))
                    continue;

                yield return assetData;
            }

            m_CanLoadMoreItems = false;

            await Task.CompletedTask; // Remove warning about async
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            PageFilters.EnableFilters(m_AssetList.Any());
            SetErrorOrMessageData(!m_AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty,
                ErrorOrMessageRecommendedAction.None);
        }
    }
}
