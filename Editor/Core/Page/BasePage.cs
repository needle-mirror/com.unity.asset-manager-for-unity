using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal abstract class BasePage : IPage
    {
        public event Action<bool> onLoadingStatusChanged;
        public event Action<AssetIdentifier> onSelectedAssetChanged;
        public event Action<IReadOnlyCollection<string>> onSearchFiltersChanged;
        public event Action<ErrorOrMessageHandlingData> onErrorOrMessageThrown;

        [SerializeField]
        private AsyncLoadOperation m_LoadMoreAssetsOperation = new ();
        public bool isLoading => m_LoadMoreAssetsOperation.isLoading;

        [SerializeField]
        protected bool m_HasMoreItems;
        public bool hasMoreItems => m_HasMoreItems;

        [SerializeField]
        protected int m_NextStartIndex;

        public abstract PageType pageType { get; }

        [SerializeField]
        private List<string> m_SearchFilters = new();
        public IReadOnlyCollection<string> searchFilters => m_SearchFilters;

        [SerializeReference]
        private List<LocalFilter> m_Filters = new();

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

        [SerializeReference]
        protected List<IAssetData> m_AssetList = new();
        public IReadOnlyCollection<IAssetData> assetList => m_AssetList;

        [SerializeField]
        ErrorOrMessageHandlingData m_ErrorOrMessageHandling = new();
        public ErrorOrMessageHandlingData errorOrMessageHandlingData { get => m_ErrorOrMessageHandling; }

        [SerializeField]
        private bool m_ReTriggerSearchAfterDomainReload = false;

        protected IAssetDataManager m_AssetDataManager;

        public void ResolveDependencies(IAssetDataManager assetDataManager)
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
            if (!m_ReTriggerSearchAfterDomainReload) 
                return;
            
            m_ReTriggerSearchAfterDomainReload = false;
            LoadMore();
        }

        public virtual void OnDisable()
        {
            if (!isLoading) 
                return;
            
            m_LoadMoreAssetsOperation.Cancel();
            m_ReTriggerSearchAfterDomainReload = true;
        }

        public void OnDestroy()
        {
            Clear(false);
            OnDeactivated();
            onLoadingStatusChanged = null;
            onSelectedAssetChanged = null;
            onSearchFiltersChanged = null;
            onErrorOrMessageThrown = null;
        }

        public virtual void LoadMore()
        {
            if (!hasMoreItems || isLoading)
                return;

            m_LoadMoreAssetsOperation.Start(LoadMoreAssets,
                onLoadingStartCallback: () =>
                {
                    errorOrMessageHandlingData.message = default;
                    errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    onLoadingStatusChanged?.Invoke(isLoading);
                },
                onCancelledCallback: null,
                onExceptionCallback: e =>
                {
                    Debug.LogException(e);
                    SetErrorOrMessageData("It seems there was an error while trying to retrieve assets.");
                    onLoadingStatusChanged?.Invoke(isLoading);
                },
                onSuccessCallback: results =>
                {
                    var assetDatas = results as IAssetData[] ?? results.ToArray();

                    m_AssetDataManager.AddOrUpdateAssetDataFromCloudAsset(assetDatas);
                    m_AssetList.AddRange(assetDatas);

                    OnLoadMoreSuccessCallBack(assetDatas.Select(assetData => assetData.identifier).ToList());
                    onLoadingStatusChanged?.Invoke(isLoading);
                });
        }

        protected void SetErrorOrMessageData(string errorMessage, ErrorOrMessageRecommendedAction actionType = ErrorOrMessageRecommendedAction.Retry)
        {
            errorOrMessageHandlingData.message = errorMessage;
            errorOrMessageHandlingData.errorOrMessageRecommendedAction = actionType;

            if (actionType != ErrorOrMessageRecommendedAction.None)
            {
                onErrorOrMessageThrown?.Invoke(errorOrMessageHandlingData);
            }
        }

        public void Clear(bool reloadImmediately, bool keepSelection = false)
        {
            m_LoadMoreAssetsOperation.Cancel();
            m_AssetList.Clear();
            m_HasMoreItems = true;
            m_NextStartIndex = 0;
            SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);

            if(!keepSelection)
                selectedAssetId = null;
            if (reloadImmediately)
                LoadMore();
        }

        protected abstract IAsyncEnumerable<IAssetData> LoadMoreAssets(CancellationToken token);
        protected abstract void OnLoadMoreSuccessCallBack(IReadOnlyCollection<AssetIdentifier> assetIdentifiers);
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

        public void AddLocalFilter(LocalFilter filter)
        {
            m_Filters.Add(filter);
        }

        public void RemoveLocalFilter(LocalFilter filter)
        {
            m_Filters.Remove(filter);
        }
        
        protected async Task<bool> IsDiscardedByLocalFilter(IAssetData assetData)
        {
            var discarded = false;
                
            foreach (var filter in m_Filters)
            {
                if (await filter.Contains(assetData))
                    continue;
                
                discarded = true;
                break;
            }

            return discarded;
        }
    }
}
