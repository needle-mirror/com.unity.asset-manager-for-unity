using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class InProjectPage : BasePage
    {
        public override PageType pageType => PageType.InProject;
        public override string collectionPath => string.Empty;

        private IAssetsProvider m_AssetsProvider;
        
        public void ResolveDependencies(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider)
        {
            base.ResolveDependencies(assetDataManager);
            m_AssetsProvider = assetsProvider;
        }

        public InProjectPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider) : base(assetDataManager)
        {
            ResolveDependencies(assetDataManager, assetsProvider);
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
            Clear(true);
        }

        protected override async Task<IReadOnlyCollection<AssetIdentifier>> LoadMoreAssets(CancellationToken token)
        {
            var assetIds =  await m_AssetsProvider.SearchAsync(m_AssetDataManager.importedAssetInfos.Select(i => i.id).ToArray(), token);
            m_HasMoreItems = false;
            return assetIds;
        }

        protected override void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers)
        {
            SetErrorOrMessageData(!m_AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty, ErrorOrMessageRecommendedAction.None);
        }
    }
}
