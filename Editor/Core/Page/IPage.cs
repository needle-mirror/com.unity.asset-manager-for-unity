using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    interface IPage
    {
        bool IsLoading { get; }
        bool CanLoadMoreItems { get; }
        PageFilters PageFilters { get; }
        AssetIdentifier LastSelectedAssetId { get; }
        IReadOnlyCollection<IAssetData> AssetList { get; }
        MessageData MessageData { get; }
        IReadOnlyCollection<AssetIdentifier> SelectedAssets { get; }

        event Action<bool> LoadingStatusChanged;
        event Action<IReadOnlyCollection<AssetIdentifier>> SelectedAssetsChanged;
        event Action<IReadOnlyCollection<string>> SearchFiltersChanged;
        event Action<AssetIdentifier, bool> ToggleAssetChanged;
        event Action<MessageData> MessageThrown;

        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchGroupBy groupBy, CancellationToken token);

        void SelectAsset(AssetIdentifier asset, bool additive);
        void SelectAssets(IEnumerable<AssetIdentifier> assets);
        public void ToggleAsset(IAssetData assetData, bool checkState);
        void LoadMore();
        void Clear(bool reloadImmediately, bool clearSelection = true);
        void ClearSelection();

        // Called after the page is created, and after a domain reload
        void OnEnable();

        // Called when the window is closed, and before a domain reload
        void OnDisable();

        // Called when a page got activated (when it became the current visible page)
        // Not called after a domain reload
        void OnActivated();

        // Called when a page got deactivated (when it went from the current page to the previous page)
        void OnDeactivated();
    }
}
