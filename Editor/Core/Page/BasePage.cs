using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    abstract class BasePage : IPage
    {
        [SerializeField]
        List<AsyncLoadOperation> m_LoadMoreAssetsOperations = new();

        [SerializeField]
        protected bool m_CanLoadMoreItems;

        [SerializeField]
        protected int m_NextStartIndex;

        [SerializeField]
        PageFilters m_PageFilters;

        [SerializeField]
        List<AssetIdentifier> m_SelectedAssets = new();

        [FormerlySerializedAs("m_ErrorOrMessageHandling")] [SerializeField]
        MessageData m_MessageData = new();

        [SerializeField]
        bool m_ReTriggerSearchAfterDomainReload;

        [SerializeReference]
        protected IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        protected List<IAssetData> m_AssetList = new();

        [SerializeReference]
        protected IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        protected IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        protected IPageManager m_PageManager;

        public virtual bool DisplaySearchBar => true;
        public virtual bool DisplayBreadcrumbs => false;
        public virtual bool DisplaySideBar => true;
        public virtual bool DisplayFilters => true;
        public virtual bool DisplaySettings => false;

        public event Action<bool> LoadingStatusChanged;
        public event Action<List<AssetIdentifier>> SelectedAssetsChanged;
        public event Action<IEnumerable<string>> SearchFiltersChanged;
        public event Action<MessageData> MessageThrown;

        public bool IsLoading => m_LoadMoreAssetsOperations.Exists(op => op.IsLoading);
        public bool CanLoadMoreItems => m_CanLoadMoreItems;
        public PageFilters PageFilters => m_PageFilters;
        public List<AssetIdentifier> SelectedAssets => m_SelectedAssets;
        public AssetIdentifier LastSelectedAssetId => m_SelectedAssets.LastOrDefault();
        public IReadOnlyCollection<IAssetData> AssetList => m_AssetList;
        public MessageData MessageData => m_MessageData;

        public static readonly MessageData MissingSelectedProjectErrorData = new()
        {
            Message = L10n.Tr("Select a project to upload to"),
            RecommendedAction = RecommendedAction.None,
            IsPageScope = true
        };

        public static readonly MessageData LoadMoreAssetsOperationsErrorData = new()
        {
            Message = L10n.Tr("It seems there was an error while trying to retrieve assets."),
            RecommendedAction = RecommendedAction.Retry,
            IsPageScope = true
        };

        protected BasePage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetsProvider = assetsProvider;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;

            m_CanLoadMoreItems = true;

            m_PageFilters = new PageFilters(this, InitFilters());
        }

        public virtual void OnActivated()
        {
            m_PageFilters.EnableFilters(false);
            AnalyticsSender.SendEvent(new PageSelectedEvent(GetPageName()));
        }

        public virtual void OnDeactivated()
        {
            m_PageFilters.ClearSearchFilters();
            Clear(false);
        }

        public virtual void OnEnable()
        {
            m_PageFilters.SearchFiltersChanged += OnSearchFiltersChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;

            if (!m_ReTriggerSearchAfterDomainReload)
                return;

            m_ReTriggerSearchAfterDomainReload = false;
            LoadMore();
        }

        public virtual void OnDisable()
        {
            m_PageFilters.SearchFiltersChanged -= OnSearchFiltersChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;

            if (!IsLoading)
                return;

            CancelAndClearLoadMoreOperations();
            m_ReTriggerSearchAfterDomainReload = true;
        }

        public virtual void OpenSettings(VisualElement target) { }

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

        public void SelectAssets(IEnumerable<AssetIdentifier> assets)
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

        public virtual void LoadMore()
        {
            if (!CanLoadMoreItems || IsLoading)
                return;

            var loadMoreAssetsOperation = new AsyncLoadOperation();
            m_LoadMoreAssetsOperations.Add(loadMoreAssetsOperation);
            _ = loadMoreAssetsOperation.Start(LoadMoreAssets,
                () =>
                {
                    SetMessageData(default, RecommendedAction.None);
                    LoadingStatusChanged?.Invoke(IsLoading);
                },
                cancelledCallback: null,
                exceptionCallback: e =>
                {
                    Debug.LogException(e);
                    SetMessageData(LoadMoreAssetsOperationsErrorData);
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
                },
                finallyCallback: () => m_LoadMoreAssetsOperations.Remove(loadMoreAssetsOperation)
            );
        }

        public void Clear(bool reloadImmediately, bool keepSelection = false)
        {
            CancelAndClearLoadMoreOperations();

            m_AssetList.Clear();
            m_CanLoadMoreItems = true;
            m_NextStartIndex = 0;
            SetMessageData(string.Empty, RecommendedAction.None);

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
            }.OrderBy(f => f.DisplayName).ToList();
        }

        protected void SetMessageData(string errorMessage, RecommendedAction actionType = RecommendedAction.Retry, bool isPageScope = true)
        {
            MessageData.Message = errorMessage;
            MessageData.RecommendedAction = actionType;
            MessageData.IsPageScope = isPageScope;
            // Only invoke MessageThrown if not default message
            if (errorMessage != default)
            {
                MessageThrown?.Invoke(MessageData);
            }
        }

        protected void SetMessageData(MessageData messageData)
        {
            SetMessageData(messageData.Message,
                messageData.RecommendedAction,
                messageData.IsPageScope);
        }

        protected internal abstract IAsyncEnumerable<IAssetData> LoadMoreAssets(CancellationToken token);
        protected abstract void OnLoadMoreSuccessCallBack();

        protected virtual void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            if (projectInfo == null)
                return;

            m_PageManager.SetActivePage<CollectionPage>();
        }

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
            await foreach (var cloudAssetData in m_AssetsProvider.SearchAsync(organizationId, projectIds, assetSearchFilter,
                               m_NextStartIndex, Constants.DefaultPageSize, token))
            {
                ++count;

                // If an asset was imported, we need to display it's state and not the one from the cloud
                var importedAssetInfo = m_AssetDataManager.GetImportedAssetInfo(cloudAssetData.Identifier);
                var assetData = importedAssetInfo != null ? importedAssetInfo.AssetData : cloudAssetData;

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
            return name.EndsWith("Page") ? name[..^4] : name;
        }

        void CancelAndClearLoadMoreOperations()
        {
            foreach (var operation in m_LoadMoreAssetsOperations.Where(op => op.IsLoading))
            {
                operation.Cancel();
            }

            m_LoadMoreAssetsOperations.Clear();
        }
    }
}
