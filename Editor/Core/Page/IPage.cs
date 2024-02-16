using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    internal interface IPage
    {
        event Action<bool> onLoadingStatusChanged;
        event Action<AssetIdentifier> onSelectedAssetChanged;
        event Action<IReadOnlyCollection<string>> onSearchFiltersChanged;
        event Action<ErrorOrMessageHandlingData> onErrorOrMessageThrown;

        bool isLoading { get; }
        bool hasMoreItems { get; }
        PageType pageType { get; }
        IReadOnlyCollection<string> searchFilters { get; }
        void AddLocalFilter(LocalFilter filter);
        void RemoveLocalFilter(LocalFilter filter);
        bool isActivePage { get; }

        AssetIdentifier selectedAssetId { get; set; }
        IReadOnlyCollection<IAssetData> assetList { get; }

        ErrorOrMessageHandlingData errorOrMessageHandlingData { get; }

        void LoadMore();

        void Clear(bool reloadImmediately, bool keepSelection = false);

        void OnEnable();
        void OnDisable();

        void OnDestroy();

        // Called when a page got activated (when it became the current visible page)
        void OnActivated();
        // Called when a page got deactivated (when it went from the current page to the previous page)
        void OnDeactivated();
        void AddSearchFilter(IEnumerable<string> searchFilter, bool reloadImmediately);
        void RemoveSearchFilter(string searchFilter, bool reloadImmediately);
        void ClearSearchFilters(bool reloadImmediately);
    }
}
