using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class CollectionPage : BasePage
    {
        public override bool DisplayBreadcrumbs => true;

        [SerializeField]
        private CollectionInfo m_CollectionInfo;

        public string collectionPath => m_CollectionInfo?.GetFullPath();

        public CollectionPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
            m_CollectionInfo = m_ProjectOrganizationProvider.SelectedCollection;
        }

        protected override async IAsyncEnumerable<IAssetData> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.SelectedProject?.id != m_CollectionInfo.projectId)
                yield return null;

            await foreach (var assetData in LoadMoreAssets(m_CollectionInfo, token))
            {
                yield return assetData;
            }
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (string.IsNullOrEmpty(collectionPath) && !m_AssetList.Any() && !pageFilters.searchFilters.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyProjectText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (pageFilters.searchFilters.Any() && !m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr("No results found for \"" + string.Join(", ", pageFilters.searchFilters) + "\""), ErrorOrMessageRecommendedAction.None);
            }
            else if (!m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyCollectionsText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else
            {
                SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);
            }
        }

        protected override string GetPageName()
        {
            if(m_CollectionInfo == null || string.IsNullOrEmpty(m_CollectionInfo.name))
            {
                return "Project";
            }

            return "Collection";
        }
    }
}
