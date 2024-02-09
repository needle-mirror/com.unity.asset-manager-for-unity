using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal class AllAssetsPage : BasePage
    {
        public AllAssetsPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager)
        {
            ResolveDependencies(assetDataManager, assetsProvider, projectOrganizationProvider);
        }

        public override PageType pageType => PageType.AllAssets;

        IAssetsProvider m_AssetsProvider;
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        protected override async IAsyncEnumerable<IAsset> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            if (m_ProjectOrganizationProvider.selectedProject?.id != ProjectInfo.AllAssetsProjectInfo.id)
                yield return null;

            var count = 0;
            await foreach(var asset in m_AssetsProvider.SearchAsync(m_ProjectOrganizationProvider.organization, searchFilters, m_NextStartIndex, Constants.DefaultPageSize, token))
            {
                yield return asset;
                ++count;
            }
            m_HasMoreItems = count == Constants.DefaultPageSize;
            m_NextStartIndex += count;
        }

        protected override void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers)
        {
            if (!m_AssetList.Any() && !searchFilters.Any())
            {
                SetErrorOrMessageData(L10n.Tr(Constants.EmptyAllAssetsText), ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink);
            }
            else if (searchFilters.Any() && !m_AssetList.Any())
            {
                SetErrorOrMessageData(L10n.Tr("No results found for \"" + string.Join(", ", searchFilters) + "\""), ErrorOrMessageRecommendedAction.None);
            }
            else
            {
                SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);
            }
        }

        public void ResolveDependencies(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
        {
            base.ResolveDependencies(assetDataManager);
            m_AssetsProvider = assetsProvider;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }
    }
}
