using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    internal interface IPage
    {
        event Action<bool> onLoadingStatusChanged;
        event Action<AssetIdentifier> onSelectedAssetChanged;
        event Action<IEnumerable<string>> onSearchFiltersChanged;
        event Action<ErrorOrMessageHandlingData> onErrorOrMessageThrown;
        bool isLoading { get; }
        bool hasMoreItems { get; }

        PageFilters pageFilters { get; }

        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, string criterion, CancellationToken token);
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
    }
}
