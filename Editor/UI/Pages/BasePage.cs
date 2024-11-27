using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
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

        [SerializeField]
        MessageData m_MessageData = new();

        [SerializeField]
        bool m_ReTriggerSearchAfterDomainReload;

        [SerializeReference]
        protected IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        protected IAssetsProvider m_AssetsProvider;

        [SerializeField]
        AssetDataCollection<BaseAssetData> m_AssetList = new();

        [SerializeReference]
        protected IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        protected IPageManager m_PageManager;

        protected VisualElement m_CustomUISection;
        protected Dictionary<string, SortField> m_SortField;

        public virtual bool DisplaySearchBar => true;
        public virtual bool DisplayBreadcrumbs => false;
        public virtual bool DisplaySideBar => true;
        public virtual bool DisplayFilters => true;
        public virtual bool DisplayFooter => true;
        public virtual bool DisplaySort => true;

        public Dictionary<string, SortField> SortOptions
        {
            get
            {
                if(m_SortField == null)
                {
                    m_SortField = CreateSortField();
                }

                return m_SortField;
            }
        }

        public event Action<bool> LoadingStatusChanged;
        public event Action<IReadOnlyCollection<AssetIdentifier>> SelectedAssetsChanged;
        public event Action<IReadOnlyCollection<string>> SearchFiltersChanged;
        public event Action<MessageData> MessageThrown;

        public bool IsLoading => m_LoadMoreAssetsOperations.Exists(op => op.IsLoading);
        public bool CanLoadMoreItems => m_CanLoadMoreItems;
        public PageFilters PageFilters => m_PageFilters;
        public IReadOnlyCollection<AssetIdentifier> SelectedAssets => m_SelectedAssets;
        public bool IsAssetSelected(AssetIdentifier asset) => m_SelectedAssets.Contains(asset);
        public AssetIdentifier LastSelectedAssetId => m_SelectedAssets.LastOrDefault();
        public IReadOnlyCollection<BaseAssetData> AssetList => m_AssetList;
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
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;

            m_AssetList.RebuildAssetList(m_AssetDataManager);

            if (m_ReTriggerSearchAfterDomainReload)
            {
                m_ReTriggerSearchAfterDomainReload = false;
                LoadMore();
            }
        }

        public virtual void OnDisable()
        {
            m_PageFilters.SearchFiltersChanged -= OnSearchFiltersChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;

            if (!IsLoading)
                return;

            CancelAndClearLoadMoreOperations();
            m_ReTriggerSearchAfterDomainReload = true;
        }

        protected virtual Dictionary<string,SortField> CreateSortField()
        {
            return new()
            {
                { "Name", SortField.Name },
                { "Last Modified", SortField.Updated },
                { "Upload Date", SortField.Created },
                { "Description", SortField.Description },
                { "Status", SortField.Status }
            };
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

            SelectedAssetsChanged?.Invoke(m_SelectedAssets);
        }

        public void ClearSelection()
        {
            m_SelectedAssets.Clear();
            SelectedAssetsChanged?.Invoke(m_SelectedAssets);
        }

        public void SelectAssets(IEnumerable<AssetIdentifier> assets)
        {
            m_SelectedAssets.AddRange(assets);
            m_SelectedAssets = m_SelectedAssets.Distinct().ToList();
            SelectedAssetsChanged?.Invoke(m_SelectedAssets);
        }

        public virtual void ToggleAsset(AssetIdentifier assetIdentifier, bool checkState)
        {
            SelectAsset(assetIdentifier, checkState);
        }

        public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchGroupBy groupBy, CancellationToken token)
        {
            return await m_AssetsProvider.GetFilterSelectionsAsync(organizationId, projectIds,
                m_PageFilters.AssetSearchFilter,
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
                    var assetDatas = results as BaseAssetData[] ?? results.ToArray();

                    m_AssetDataManager.AddOrUpdateAssetDataFromCloudAsset(assetDatas);

                    foreach (var assetData in assetDatas)
                    {
                        var asset = m_AssetList.Find(asset => asset.Identifier.Equals(assetData.Identifier));
                        if (asset != null)
                        {
                            if (asset.IsTheSame(assetData))
                                continue;

                            m_AssetList.Replace(asset, assetData);
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

        public void Clear(bool reloadImmediately, bool clearSelection = true)
        {
            CancelAndClearLoadMoreOperations();

            m_AssetList.Clear();
            m_CanLoadMoreItems = true;
            m_NextStartIndex = 0;
            SetMessageData(string.Empty, RecommendedAction.None);

            if (clearSelection)
            {
                ClearSelection();
            }

            if (reloadImmediately)
            {
                LoadMore();
            }
        }

        public VisualElement GetCustomUISection()
        {
            return m_CustomUISection ??= CreateCustomUISection();
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

        public void SetMessageData(string errorMessage, RecommendedAction actionType = RecommendedAction.Retry,
            bool isPageScope = true, HelpBoxMessageType messageType = HelpBoxMessageType.Info)
        {
            MessageData.Message = errorMessage;
            MessageData.RecommendedAction = actionType;
            MessageData.IsPageScope = isPageScope;
            MessageData.MessageType = messageType;

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

        protected internal abstract IAsyncEnumerable<BaseAssetData> LoadMoreAssets(CancellationToken token);
        protected abstract void OnLoadMoreSuccessCallBack();

        protected virtual void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            if (projectInfo == null)
                return;

            m_PageManager.SetActivePage<CollectionPage>();
        }

        private void OnOrganizationChanged(OrganizationInfo obj)
        {
            m_PageManager.ActivePage.ClearSelection();
        }

        protected async IAsyncEnumerable<BaseAssetData> LoadMoreAssets(CollectionInfo collectionInfo,
            [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(collectionInfo.OrganizationId,
                               new List<string> { collectionInfo.ProjectId }, collectionInfo.GetFullPath(), token))
            {
                yield return assetData;
            }
        }

        protected async IAsyncEnumerable<BaseAssetData> LoadMoreAssets(OrganizationInfo organizationInfo,
            [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var assetData in LoadMoreAssets(organizationInfo.Id,
                               organizationInfo.ProjectInfos.Select(p => p.Id), null, token))
            {
                yield return assetData;
            }
        }

        async IAsyncEnumerable<BaseAssetData> LoadMoreAssets(string organizationId, IEnumerable<string> projectIds,
            string collectionPath, [EnumeratorCancellation] CancellationToken token)
        {
            var assetSearchFilter = m_PageFilters.AssetSearchFilter;
            UpdateSearchFilter(assetSearchFilter, collectionPath, m_PageFilters.SearchFilters);

            var count = 0;
            await foreach (var cloudAssetData in m_AssetsProvider.SearchAsync(organizationId, projectIds, assetSearchFilter,
                               m_PageManager.SortField, m_PageManager.SortingOrder, m_NextStartIndex, Constants.DefaultPageSize, token))
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

        void OnSearchFiltersChanged(IReadOnlyCollection<string> searchFilters)
        {
            Clear(true);
            SearchFiltersChanged?.Invoke(searchFilters);
        }

        void UpdateSearchFilter(AssetSearchFilter assetSearchFilter, string collectionPath, IEnumerable<string> searchStrings)
        {
            if (!string.IsNullOrEmpty(collectionPath))
            {
                assetSearchFilter.Collection = collectionPath;
            }
            else
            {
                assetSearchFilter.Collection = null;
            }

            if (searchStrings != null && searchStrings.Any())
            {
                assetSearchFilter.Searches = searchStrings.ToList();
            }
            else
            {
                assetSearchFilter.Searches = null;
            }
        }

        protected async Task<bool> IsDiscardedByLocalFilter(BaseAssetData assetData)
        {
            var tasks = new List<Task<bool>>();
            foreach (var filter in m_PageFilters.SelectedLocalFilters)
            {
                tasks.Add(filter.Contains(assetData));
            }

            await Task.WhenAll(tasks);

            return tasks.Exists(t => !t.Result);
        }

        protected virtual VisualElement CreateCustomUISection()
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
