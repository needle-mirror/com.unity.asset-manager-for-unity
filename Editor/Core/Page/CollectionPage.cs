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
    class CollectionPage : BasePage
    {
        [SerializeField]
        CollectionInfo m_CollectionInfo;

        public override bool DisplayBreadcrumbs => true;
        public string CollectionPath => m_CollectionInfo?.GetFullPath();

        public CollectionPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
            m_CollectionInfo = m_ProjectOrganizationProvider.SelectedCollection;
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.SelectedProject?.Id != m_CollectionInfo.ProjectId)
            {
                yield return null;
            }

            await foreach (var assetData in LoadMoreAssets(m_CollectionInfo, token))
            {
                yield return assetData;
            }
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (string.IsNullOrEmpty(CollectionPath) && !m_AssetList.Any() && !PageFilters.SearchFilters.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyProjectText),
                    ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (PageFilters.SearchFilters.Any() && !m_AssetList.Any())
            {
                SetErrorOrMessageData(
                    L10n.Tr("No results found for \"" + string.Join(", ", PageFilters.SearchFilters) + "\""),
                    ErrorOrMessageRecommendedAction.None);
            }
            else if (!m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyCollectionsText),
                    ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else
            {
                PageFilters.EnableFilters();
                SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);
            }
        }

        protected override string GetPageName()
        {
            if (m_CollectionInfo == null || string.IsNullOrEmpty(m_CollectionInfo.Name))
            {
                return "Project";
            }

            return "Collection";
        }
    }
}
