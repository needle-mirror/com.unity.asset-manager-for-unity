using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    internal class AllAssetsPage : BasePage
    {
        public override bool DisplayBreadcrumbs => true;

        public AllAssetsPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
        }

        public override void LoadMore()
        {
            if(m_ProjectOrganizationProvider?.SelectedOrganization == null)
                return;

            base.LoadMore();
        }

        protected override async IAsyncEnumerable<IAssetData> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.SelectedProject?.id != ProjectInfo.AllAssetsProjectInfo.id)
                yield return null;

            await foreach(var assetData in LoadMoreAssets(m_ProjectOrganizationProvider.SelectedOrganization, token))
            {
                yield return assetData;
            }
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (!m_AssetList.Any() && !pageFilters.searchFilters.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyAllAssetsText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (pageFilters.searchFilters.Any() && !m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr("No results found for \"" + string.Join(", ", pageFilters.searchFilters) + "\""), ErrorOrMessageRecommendedAction.None);
            }
            else
            {
                SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);
            }
        }
    }
}
