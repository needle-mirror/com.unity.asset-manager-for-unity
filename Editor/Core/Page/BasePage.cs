using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    abstract class BasePage : IPage
    {
        [SerializeField]
        AsyncLoadOperation m_LoadMoreAssetsOperation = new();

        [SerializeField]
        protected bool m_CanLoadMoreItems;

        [SerializeField]
        protected int m_NextStartIndex;

        [SerializeField]
        PageFilters m_PageFilters;

        [SerializeField]
        bool m_IsActive;

        [SerializeField]
        List<AssetIdentifier> m_SelectedAssets = new();

        [SerializeField]
        ErrorOrMessageHandlingData m_ErrorOrMessageHandling = new();

        [SerializeField]
        bool m_ReTriggerSearchAfterDomainReload;

        [SerializeReference]
        protected IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        protected List<IAssetData> m_AssetList = new();

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        protected IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public virtual string Title => null;
        public virtual bool DisplayTopBar => true; // TODO Use enum flags for visibility toggles
        public virtual bool DisplayBreadcrumbs => false;
        public virtual bool DisplaySideBar => true;

        public event Action<bool> LoadingStatusChanged;
        public event Action<List<AssetIdentifier>> SelectedAssetsChanged;
        public event Action<IEnumerable<string>> SearchFiltersChanged;
        public event Action<ErrorOrMessageHandlingData> ErrorOrMessageThrown;

        public bool IsLoading => m_LoadMoreAssetsOperation.isLoading;
        public bool CanLoadMoreItems => m_CanLoadMoreItems;
        public PageFilters PageFilters => m_PageFilters;
        public bool IsActivePage => m_IsActive;
        public List<AssetIdentifier> SelectedAssets => m_SelectedAssets;
        public AssetIdentifier LastSelectedAssetId => m_SelectedAssets.LastOrDefault();
        public IReadOnlyCollection<IAssetData> AssetList => m_AssetList;
        public ErrorOrMessageHandlingData ErrorOrMessageHandlingData => m_ErrorOrMessageHandling;

        protected BasePage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetsProvider = assetsProvider;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_CanLoadMoreItems = true;

            m_PageFilters = new PageFilters(this, InitFilters());
        }

        public void SelectAsset(AssetIdentifier asset, bool additive)
        {
            if (LastSelectedAssetId?.IsIdValid() != true && !asset.IsIdValid())
                return;

            if (!additive)
            {
                m_SelectedAssets.Clear();
            }

            if (m_SelectedAssets.Contains(asset))
            {
                m_SelectedAssets.Remove(asset);
            }
            else
            {
                m_SelectedAssets.Add(asset);
            }

            SelectedAssetsChanged?.Invoke(SelectedAssets);
        }

        public void ClearSelection()
        {
            m_SelectedAssets.Clear();
            SelectedAssetsChanged?.Invoke(SelectedAssets);
        }

        public void SelectAssets(List<AssetIdentifier> assets)
        {
            SelectedAssets.AddRange(assets);
            m_SelectedAssets = SelectedAssets.Distinct().ToList();
            SelectedAssetsChanged?.Invoke(SelectedAssets);
        }

        public virtual void ToggleAsset(IAssetData assetData, bool checkState)
        {
            SelectAsset(assetData.Identifier, checkState);
        }

        public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            GroupableField groupBy, CancellationToken token)
        {
            return await m_AssetsProvider.GetFilterSelectionsAsync(organizationId, projectIds,
                m_PageFilters.AssetFilter,
                groupBy, token);
        }

        public virtual void OnActivated()
        {
            m_IsActive = true;
            m_PageFilters.EnableFilters(false);

            AnalyticsSender.SendEvent(new PageSelectedEvent(GetPageName()));
        }

        public virtual void OnDeactivated()
        {
            m_IsActive = false;

            m_PageFilters.ClearSearchFilters();
            Clear(false);
        }

        public virtual void OnEnable()
        {
            m_PageFilters.SearchFiltersChanged += OnSearchFiltersChanged;

            if (!m_ReTriggerSearchAfterDomainReload)
                return;

            m_ReTriggerSearchAfterDomainReload = false;
            LoadMore();
        }

        public virtual void OnDisable()
        {
            m_PageFilters.SearchFiltersChanged -= OnSearchFiltersChanged;

            if (!IsLoading)
                return;

            m_LoadMoreAssetsOperation.Cancel();
            m_ReTriggerSearchAfterDomainReload = true;
        }

        public void OnDestroy()
        {
            Clear(false);
            OnDeactivated();
            LoadingStatusChanged = null;
            SelectedAssetsChanged = null;
            SearchFiltersChanged = null;
            ErrorOrMessageThrown = null;
        }

        public virtual void LoadMore()
        {
            if (!CanLoadMoreItems || IsLoading)
                return;

            _ = m_LoadMoreAssetsOperation.Start(LoadMoreAssets,
                () =>
                {
                    ErrorOrMessageHandlingData.Message = default;
                    ErrorOrMessageHandlingData.ErrorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    LoadingStatusChanged?.Invoke(IsLoading);
                },
                cancelledCallback: null,
                exceptionCallback: e =>
                {
                    Debug.LogException(e);
                    SetErrorOrMessageData("It seems there was an error while trying to retrieve assets.");
                    LoadingStatusChanged?.Invoke(IsLoading);
                },
                successCallback: results =>
                {
                    var assetDatas = results as IAssetData[] ?? results.ToArray();

                    m_AssetDataManager.AddOrUpdateAssetDataFromCloudAsset(assetDatas);

                    foreach (var assetData in assetDatas)
                    {
                        var asset = m_AssetList.Find(asset => asset.Identifier.Equals(assetData.Identifier));
                        if (asset != null)
                        {
                            if (asset.IsTheSame(assetData))
                                continue;

                            m_AssetList[m_AssetList.IndexOf(asset)] = assetData;
                        }
                        else
                        {
                            m_AssetList.Add(assetData);
                        }
                    }

                    OnLoadMoreSuccessCallBack();
                    LoadingStatusChanged?.Invoke(IsLoading);
                });
        }

        public void Clear(bool reloadImmediately, bool keepSelection = false)
        {
            m_LoadMoreAssetsOperation.Cancel();
            m_AssetList.Clear();
            m_CanLoadMoreItems = true;
            m_NextStartIndex = 0;
            SetErrorOrMessageData(string.Empty, ErrorOrMessageRecommendedAction.None);

            if (!keepSelection)
            {
                ClearSelection();
            }

            if (reloadImmediately)
            {
                LoadMore();
            }
        }

        protected virtual List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
                new StatusFilter(this, m_ProjectOrganizationProvider),
                new UnityTypeFilter(this, m_ProjectOrganizationProvider),
                new CreatedByFilter(this, m_ProjectOrganizationProvider),
                new UpdatedByFilter(this, m_ProjectOrganizationProvider)
            };
        }

        protected void SetErrorOrMessageData(string errorMessage,
            ErrorOrMessageRecommendedAction actionType = ErrorOrMessageRecommendedAction.Retry)
        {
            ErrorOrMessageHandlingData.Message = errorMessage;
            ErrorOrMessageHandlingData.ErrorOrMessageRecommendedAction = actionType;

            if (actionType != ErrorOrMessageRecommendedAction.None)
            {
                ErrorOrMessageThrown?.Invoke(ErrorOrMessageHandlingData);
            }
        }

        protected internal abstract IAsyncEnumerable<IAssetData> LoadMoreAssets(CancellationToken token);
        protected abstract void OnLoadMoreSuccessCallBack();

        protected async IAsyncEnumerable<IAssetData> LoadMoreAssets(CollectionInfo collectionInfo,
            [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(collectionInfo.OrganizationId,
                               new List<string> { collectionInfo.ProjectId }, collectionInfo.GetFullPath(), token))
            {
                yield return assetData;
            }
        }

        protected async IAsyncEnumerable<IAssetData> LoadMoreAssets(OrganizationInfo organizationInfo,
            [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(organizationInfo.Id,
                               organizationInfo.ProjectInfos.Select(p => p.Id), null, token))
            {
                yield return assetData;
            }
        }

        async IAsyncEnumerable<IAssetData> LoadMoreAssets(string organizationId, IEnumerable<string> projectIds,
            string collectionPath, [EnumeratorCancellation] CancellationToken token)
        {
            var assetSearchFilter = m_PageFilters.AssetFilter;
            UpdateSearchFilter(assetSearchFilter, collectionPath, m_PageFilters.SearchFilters);

            var count = 0;
            await foreach (var asset in m_AssetsProvider.SearchAsync(organizationId, projectIds, assetSearchFilter,
                               m_NextStartIndex, Constants.DefaultPageSize, token))
            {
                ++count;

                // If an asset was imported, we need to display it's state and not the one from the cloud
                var importedAssetInfo = m_AssetDataManager.GetImportedAssetInfo(new AssetIdentifier(asset.Descriptor));
                var assetData = importedAssetInfo != null ? importedAssetInfo.AssetData : new AssetData(asset);

                if (await IsDiscardedByLocalFilter(assetData))
                    continue;

                yield return assetData;
            }

            m_CanLoadMoreItems = count == Constants.DefaultPageSize;
            m_NextStartIndex += count;
        }

        void OnSearchFiltersChanged(IEnumerable<string> searchFilters)
        {
            Clear(true);
            SearchFiltersChanged?.Invoke(searchFilters);
        }

        void UpdateSearchFilter(AssetSearchFilter assetFilter, string collectionPath, IEnumerable<string> searchStrings)
        {
            if (!string.IsNullOrEmpty(collectionPath))
            {
                assetFilter.Collections.WhereContains(new CollectionPath(collectionPath));
            }
            else
            {
                assetFilter.Collections.WhereContains(new List<CollectionPath>());
            }

            if (searchStrings != null && searchStrings.Any())
            {
                var searchFilterString = string.Concat("*", string.Join('*', searchStrings), "*");

                assetFilter.Any().Name.WithValue(searchFilterString);
                assetFilter.Any().Description.WithValue(searchFilterString);
                assetFilter.Any().Tags.WithValue(searchFilterString);
            }
            else
            {
                assetFilter.Any().Name.Clear();
                assetFilter.Any().Description.Clear();
                assetFilter.Any().Tags.Clear();
            }
        }

        protected async Task<bool> IsDiscardedByLocalFilter(IAssetData assetData)
        {
            foreach (var filter in m_PageFilters.SelectedLocalFilters)
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

        protected virtual string GetPageName()
        {
            var name = GetType().Name;
            return name.EndsWith("Page") ? name.Substring(0, name.Length - 4) : name;
        }
    }
}
