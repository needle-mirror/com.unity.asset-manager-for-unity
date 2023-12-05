using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class CollectionPage : BasePage
    {
        private const int k_PageSize = 25;

        public override PageType pageType => PageType.Collection;

        [SerializeField]
        private CollectionInfo m_CollectionInfo;
        public override string collectionPath => m_CollectionInfo.GetFullPath();

        private IAssetsProvider m_AssetsProvider;
        public void ResolveDependencies(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider)
        {
            base.ResolveDependencies(assetDataManager);
            m_AssetsProvider = assetsProvider;
        }

        public CollectionPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, CollectionInfo collectionInfo) : base(assetDataManager)
        {
            ResolveDependencies(assetDataManager, assetsProvider);

            m_CollectionInfo = collectionInfo;
        }

        protected override async Task<IReadOnlyCollection<AssetIdentifier>> LoadMoreAssets(CancellationToken token)
        {
            var assetIds = await m_AssetsProvider.SearchAsync(m_CollectionInfo, searchFilters, m_NextStartIndex, Constants.DefaultPageSize, token);
            m_HasMoreItems = assetIds.Count == k_PageSize;
            m_NextStartIndex += assetIds.Count;
            return assetIds;
        }

        protected override void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers)
        {
            if (string.IsNullOrEmpty(collectionPath) && !m_AssetList.Any())
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyAllAssetText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            else if (searchFilters.Any() && !m_AssetList.Any())
                SetErrorOrMessageData(L10n.Tr("No results found for \"" + string.Join(", ", searchFilters) + "\""), ErrorOrMessageRecommendedAction.None);
            else if (!m_AssetList.Any())
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyCollectionsText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            else
                SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);
        }
    }
}
