using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class CollectionPage : BasePage
    {
        [SerializeField]
        private CollectionInfo m_CollectionInfo;
        public string collectionPath => m_CollectionInfo.GetFullPath();

		public override PageType pageType => PageType.Collection;

        private IAssetsProvider m_AssetsProvider;
        private IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public void ResolveDependencies(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
        {
            base.ResolveDependencies(assetDataManager);
            m_AssetsProvider = assetsProvider;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public CollectionPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider, CollectionInfo collectionInfo) : base(assetDataManager)
        {
            ResolveDependencies(assetDataManager, assetsProvider, projectOrganizationProvider);

            m_CollectionInfo = collectionInfo;
        }

        protected override async IAsyncEnumerable<IAsset> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.selectedProject?.id != m_CollectionInfo.projectId)
                yield return null;

            var count = 0;
            await foreach (var cloudAsset in m_AssetsProvider.SearchAsync(m_CollectionInfo, searchFilters, m_NextStartIndex, Constants.DefaultPageSize, token))
            {
                ++count;
                yield return cloudAsset;
            }
            m_HasMoreItems = count == Constants.DefaultPageSize;
            m_NextStartIndex += count;
        }

        protected override void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers)
        {
            if (string.IsNullOrEmpty(collectionPath) && !m_AssetList.Any() && !searchFilters.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyProjectText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (searchFilters.Any() && !m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr("No results found for \"" + string.Join(", ", searchFilters) + "\""), ErrorOrMessageRecommendedAction.None);
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
    }
}
