using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal abstract class BasePage : IPage
    {
        public virtual string Title => null;
        public virtual bool DisplayTopBar => true; // TODO Use enum flags for visibility toggles
        public virtual bool DisplayBreadcrumbs => false;
        public virtual bool DisplaySideBar => true;

        public event Action<bool> onLoadingStatusChanged;
        public event Action<AssetIdentifier> onSelectedAssetChanged;
        public event Action<IEnumerable<string>> onSearchFiltersChanged;
        public event Action<ErrorOrMessageHandlingData> onErrorOrMessageThrown;

        [SerializeField]
        private AsyncLoadOperation m_LoadMoreAssetsOperation = new();
        public bool isLoading => m_LoadMoreAssetsOperation.isLoading;

        [SerializeField]
        protected bool m_HasMoreItems;
        public bool hasMoreItems => m_HasMoreItems;

        [SerializeField]
        protected int m_NextStartIndex;

        [SerializeField]
        PageFilters m_PageFilters;

        public PageFilters pageFilters => m_PageFilters;

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

        public ErrorOrMessageHandlingData errorOrMessageHandlingData
        {
            get => m_ErrorOrMessageHandling;
        }

        [SerializeField]
        private bool m_ReTriggerSearchAfterDomainReload;

        [SerializeReference]
        protected IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        protected IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        protected BasePage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetsProvider = assetsProvider;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_HasMoreItems = true;
            m_SelectedAssetId = null;

            m_PageFilters = new PageFilters(this, projectOrganizationProvider, InitFilters());
        }

        protected virtual List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
                new StatusFilter(this, m_ProjectOrganizationProvider),
                new UnityTypeFilter(this)
            };
        }

        public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, string criterion, CancellationToken token)
        {
            return await m_AssetsProvider.GetFilterSelectionsAsync(organizationId, projectIds, m_PageFilters.assetFilter, criterion, token);
        }

        public virtual void OnActivated()
        {
            m_IsActive = true;
        }

        public virtual void OnDeactivated()
        {
            m_IsActive = false;

            m_PageFilters.ClearSearchFilters();
            Clear(false);
        }

        public virtual void OnEnable()
        {
            m_PageFilters.onSearchFiltersChanged += OnSearchFiltersChanged;

            if (!m_ReTriggerSearchAfterDomainReload)
                return;

            m_ReTriggerSearchAfterDomainReload = false;
            LoadMore();
        }

        public virtual void OnDisable()
        {
            m_PageFilters.onSearchFiltersChanged -= OnSearchFiltersChanged;

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

                    OnLoadMoreSuccessCallBack();
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

            if (!keepSelection)
            {
                selectedAssetId = null;
            }

            if (reloadImmediately)
            {
                LoadMore();
            }
        }

        protected abstract IAsyncEnumerable<IAssetData> LoadMoreAssets(CancellationToken token);
        protected abstract void OnLoadMoreSuccessCallBack();

        protected async IAsyncEnumerable<IAssetData> LoadMoreAssets(CollectionInfo collectionInfo, [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(collectionInfo.organizationId, new List<string> { collectionInfo.projectId }, collectionInfo.GetFullPath(), token))
            {
                yield return assetData;
            }
        }

        protected async IAsyncEnumerable<IAssetData> LoadMoreAssets(OrganizationInfo organizationInfo, [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(organizationInfo.id, organizationInfo.projectInfos.Select(p => p.id), null, token))
            {
                yield return assetData;
            }
        }

        async IAsyncEnumerable<IAssetData> LoadMoreAssets(string organizationId, IEnumerable<string> projectIds, string collectionPath, [EnumeratorCancellation] CancellationToken token)
        {
            var assetSearchFilter = m_PageFilters.assetFilter;
            UpdateSearchFilter(assetSearchFilter, m_PageFilters.searchFilters);

            if (!string.IsNullOrEmpty(collectionPath))
            {
                assetSearchFilter.Collections.Add(new CollectionPath(collectionPath));
            }

            var count = 0;
            await foreach (var asset in m_AssetsProvider.SearchAsync(organizationId, projectIds, assetSearchFilter, m_NextStartIndex, Constants.DefaultPageSize, token))
            {
                ++count;

                var importedAssetInfo = m_AssetDataManager.GetImportedAssetInfo(new AssetIdentifier(asset.Descriptor));
                var assetData = importedAssetInfo != null ? importedAssetInfo.assetData : new AssetData(asset);

                if (await IsDiscardedByLocalFilter(assetData))
                    continue;

                yield return assetData;
            }

            m_HasMoreItems = count == Constants.DefaultPageSize;
            m_NextStartIndex += count;
        }

        void OnSearchFiltersChanged(IEnumerable<string> searchFilters)
        {
            Clear(true);
            onSearchFiltersChanged?.Invoke(searchFilters);
        }

        private void UpdateSearchFilter(AssetSearchFilter assetFilter, IEnumerable<string> searchStrings)
        {
            assetFilter.Collections.Clear();

            if (searchStrings != null && searchStrings.Any())
            {
                var searchFilterString = string.Join(" ", searchStrings);

                assetFilter.Name.ForAny(searchFilterString);
                assetFilter.Description.ForAny(searchFilterString);
                assetFilter.Tags.ForAny(searchFilterString);
            }
            else
            {
                assetFilter.Name.Clear();
                assetFilter.Description.Clear();
                assetFilter.Tags.Clear();
            }
        }

        protected async Task<bool> IsDiscardedByLocalFilter(IAssetData assetData)
        {
            foreach (var filter in m_PageFilters.selectedLocalFilters)
            {
                if (await filter.Contains(assetData))
                    continue;

                return true;
            }

            return false;
        }

        public virtual VisualElement CreateCustomUISection()
        {
            return null;
        }
    }
}
