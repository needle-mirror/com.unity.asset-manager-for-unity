using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class CollectionPage : BasePage
    {
        [SerializeField]
        CollectionInfo m_CollectionInfo;

        public override bool DisplayBreadcrumbs => true;
        public string CollectionPath => m_CollectionInfo?.GetFullPath();

        public CollectionPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider, pageManager)
        {
            m_CollectionInfo = m_ProjectOrganizationProvider.SelectedCollection;
        }

        protected internal override async IAsyncEnumerable<BaseAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.SelectedProject?.Id != m_CollectionInfo.ProjectId || string.IsNullOrEmpty(m_CollectionInfo.ProjectId))
            {
                yield break;
            }

            await foreach (var assetData in LoadMoreAssets(m_CollectionInfo, token))
            {
                yield return assetData;
            }
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (!AssetList.Any())
            {
                if (PageFilters.SelectedFilters.Any())
                {
                    SetMessageData(
                        L10n.Tr(Constants.NoResultsText),
                        RecommendedAction.None);
                }
                else if (PageFilters.SearchFilters.Any())
                {
                    SetMessageData(
                        $"{L10n.Tr(Constants.NoResultsForText)} \"{string.Join(", ", PageFilters.SearchFilters)}\"",
                        RecommendedAction.None);
                }
                else if (string.IsNullOrEmpty(CollectionPath))
                {
                    SetMessageData(L10n.Tr(Constants.EmptyProjectText),
                        RecommendedAction.OpenAssetManagerDashboardLink);
                }
                else
                {
                    SetMessageData(L10n.Tr(Constants.EmptyCollectionsText),
                        RecommendedAction.OpenAssetManagerDashboardLink);
                }
            }
            else
            {
                PageFilters.EnableFilters();
                SetMessageData(string.Empty, RecommendedAction.None); // This will trigger the GridView Refresh
            }
        }

        protected override void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            m_PageManager.SetActivePage<CollectionPage>(true);
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
