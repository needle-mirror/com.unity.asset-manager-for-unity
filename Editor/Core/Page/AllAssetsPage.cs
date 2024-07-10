using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    class AllAssetsPage : BasePage
    {
        public AllAssetsPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider, pageManager) { }

        public override bool DisplayBreadcrumbs => true;

        public override void OnActivated()
        {
            base.OnActivated();

            m_ProjectOrganizationProvider.SelectProject(string.Empty);
        }

        public override void LoadMore()
        {
            if (m_ProjectOrganizationProvider?.SelectedOrganization == null)
                return;

            base.LoadMore();
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(m_ProjectOrganizationProvider.SelectedOrganization, token))
            {
                yield return assetData;
            }
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (!m_AssetList.Any() && !PageFilters.SearchFilters.Any())
            {
                SetMessageData(L10n.Tr(Constants.EmptyAllAssetsText),
                    RecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (PageFilters.SearchFilters.Any() && !m_AssetList.Any())
            {
                SetMessageData(
                    L10n.Tr("No results found for \"" + string.Join(", ", PageFilters.SearchFilters) + "\""),
                    RecommendedAction.None);
            }
            else
            {
                PageFilters.EnableFilters();
                SetMessageData(string.Empty, RecommendedAction.None);
            }
        }
    }
}
