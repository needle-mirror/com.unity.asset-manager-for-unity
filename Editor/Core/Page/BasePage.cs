using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal abstract class BasePage : IPage
    {
        public event Action<bool> onLoadingStatusChanged;
        public event Action<AssetIdentifier> onSelectedAssetChanged;
        public event Action<IReadOnlyCollection<string>> onSearchFiltersChanged;
        public event Action<ErrorHandlingData> onErrorThrown;

        [SerializeField]
        private AsyncLoadOperation m_LoadMoreAssetsOperation = new ();
        public bool isLoading => m_LoadMoreAssetsOperation.isLoading;

        [SerializeField]
        protected bool m_HasMoreItems;
        public bool hasMoreItems => m_HasMoreItems;

        [SerializeField]
        protected int m_NextStartIndex;

        public abstract PageType pageType { get; }
        public abstract string collectionPath { get; }

        [SerializeField]
        private List<string> m_SearchFilters = new();
        public IReadOnlyCollection<string> searchFilters => m_SearchFilters;

        [SerializeField]
        private bool m_IsActive;
        public bool isActivePage => m_IsActive;

        [SerializeField]
        private AssetIdentifier m_SelectedAssetId;
        public AssetIdentifier selectedAssetId
        {
            get => m_SelectedAssetId?.IsValid() == true ? m_SelectedAssetId : null;
            set
            {
                if ((m_SelectedAssetId?.IsValid() != true && value?.IsValid() != true) || value?.Equals(m_SelectedAssetId) == true)
                    return;
                m_SelectedAssetId = value;
                onSelectedAssetChanged?.Invoke(m_SelectedAssetId);
            }
        }

        [SerializeField]
        protected List<AssetIdentifier> m_AssetList = new();
        public IReadOnlyCollection<AssetIdentifier> assetList => m_AssetList;

        [SerializeField]
        ErrorHandlingData m_ErrorHandling = new();
        public ErrorHandlingData errorHandlingData { get => m_ErrorHandling; private set => m_ErrorHandling = value; }

        protected IAssetDataManager m_AssetDataManager;
        protected void ResolveDependencies(IAssetDataManager assetDataManager)
        {
            m_AssetDataManager = assetDataManager;
        }

        protected BasePage(IAssetDataManager assetDataManager)
        {
            ResolveDependencies(assetDataManager);

            m_HasMoreItems = true;
            m_SelectedAssetId = null;
        }

        public void OnActivated()
        {
            m_IsActive = true;
        }

        public void OnDeactivated()
        {
            m_IsActive = false;

            if (m_SearchFilters.Any())
                ClearSearchFilters(false);
        }

        public virtual void OnEnable()
        {
        }

        public virtual void OnDisable()
        {
        }

        public void OnDestroy()
        {
            Clear(false);
            OnDeactivated();
            onLoadingStatusChanged = null;
            onSelectedAssetChanged = null;
            onSearchFiltersChanged = null;
            onErrorThrown = null;
        }

        public virtual void LoadMore()
        {
            if (!hasMoreItems || isLoading)
                return;

            m_LoadMoreAssetsOperation.Start(LoadMoreAssets,
                onLoadingStartCallback: () =>
                {
                    errorHandlingData.errorMessage = default;
                    errorHandlingData.errorRecommendedAction = ErrorRecommendedAction.None;
                    onLoadingStatusChanged?.Invoke(isLoading);
                },
                onCancelledCallback: () => onLoadingStatusChanged?.Invoke(isLoading),
                onExceptionCallback: e =>
                {
                    Debug.LogException(e);
                    SetError("It seems there was an error while trying to retrieve assets.");
                    onLoadingStatusChanged?.Invoke(isLoading);
                },
                onSuccessCallback: result =>
                {
                    if (result != null)
                        m_AssetList.AddRange(result);
                    onLoadingStatusChanged?.Invoke(isLoading);
                });
        }

        private void SetError(string errorMessage, ErrorRecommendedAction actionType = ErrorRecommendedAction.None)
        {
            errorHandlingData.errorMessage = errorMessage;
            errorHandlingData.errorRecommendedAction = actionType;
            onErrorThrown?.Invoke(errorHandlingData);
        }

        public void Clear(bool reloadImmediately)
        {
            m_LoadMoreAssetsOperation.Cancel();
            m_AssetList.Clear();
            m_HasMoreItems = true;
            m_NextStartIndex = 0;
            selectedAssetId = null;

            if (reloadImmediately)
                LoadMore();
        }

        protected abstract Task<IReadOnlyCollection<AssetIdentifier>> LoadMoreAssets(CancellationToken token);

        public void AddSearchFilter(IEnumerable<string> searchFiltersArg, bool reloadImmediately)
        {
            var searchFilterAdded = false;
            foreach (var searchFilter in searchFiltersArg)
            {
                if (m_SearchFilters.Contains(searchFilter))
                    continue;
                m_SearchFilters.Add(searchFilter);
                searchFilterAdded = true;
            }

            if (!searchFilterAdded) return;
            Clear(reloadImmediately);
            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void RemoveSearchFilter(string searchFilter, bool reloadImmediately)
        {
            if (!m_SearchFilters.Contains(searchFilter))
                return;
            m_SearchFilters.Remove(searchFilter);
            Clear(reloadImmediately);
            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void ClearSearchFilters(bool reloadImmediately)
        {
            if (m_SearchFilters.Count == 0)
                return;
            m_SearchFilters.Clear();
            Clear(reloadImmediately);
            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }
    }
}
