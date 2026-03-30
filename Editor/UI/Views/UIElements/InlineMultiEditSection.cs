using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Fetches metadata for multi-selected assets that don't have it cached locally.
    /// Notifies when all fetches complete so the UI can display editable fields.
    /// </summary>
    class MetadataLoadingTracker : IDisposable
    {
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetDataCacheManager m_CacheManager;
        readonly HashSet<string> m_FetchedAssetIds = new();
        CancellationTokenSource m_Cts;

        /// <summary>
        /// Fired once when all metadata fetches for the current selection have completed.
        /// </summary>
        public event Action AllFetchesCompleted;

        /// <summary>
        /// True while metadata is being fetched for any assets in the current selection.
        /// </summary>
        public bool IsFetching { get; private set; }

        public MetadataLoadingTracker(IAssetDataManager assetDataManager, IAssetDataCacheManager cacheManager = null)
        {
            m_AssetDataManager = Guard.AgainstNull(assetDataManager, nameof(assetDataManager));
            m_CacheManager = cacheManager;
        }

        /// <summary>
        /// Fetches metadata for any assets in the collection that don't already have it.
        /// Sets IsFetching=true while fetches are in progress, then fires AllFetchesCompleted when done.
        /// </summary>
        public void FetchMissingMetadata(IReadOnlyCollection<BaseAssetData> assets)
        {
            if (assets == null || assets.Count == 0)
            {
                Reset();
                return;
            }

            // Check if this is the same selection we already handled
            var assetIds = new HashSet<string>(assets.Where(a => a?.Identifier != null).Select(a => a.Identifier.AssetId));
            if (assetIds.SetEquals(m_FetchedAssetIds))
                return;

            // New selection - cancel any in-flight operations and reset
            Reset();

            // Remember this selection to detect duplicate calls
            foreach (var id in assetIds)
                m_FetchedAssetIds.Add(id);

            // Find assets that need fetching (don't have metadata and not in cache)
            var assetsNeedingFetch = new List<BaseAssetData>();
            foreach (var asset in assets)
            {
                if (NeedsMetadataFetch(asset))
                    assetsNeedingFetch.Add(asset);
            }

            if (assetsNeedingFetch.Count > 0)
            {
                IsFetching = true;
                _ = FetchAllAsync(assetsNeedingFetch, m_Cts.Token);
            }
        }

        void Reset()
        {
            m_Cts?.Cancel();
            m_Cts?.Dispose();
            m_Cts = new CancellationTokenSource();
            IsFetching = false;
            m_FetchedAssetIds.Clear();
        }

        bool NeedsMetadataFetch(BaseAssetData asset)
        {
            if (asset?.Identifier == null)
                return false;

            // Already has metadata - no fetch needed
            if (asset.Metadata?.Any() == true)
                return false;

            // For imported assets, try to populate from cache first
            if (m_CacheManager?.HasEntry(asset.Identifier.AssetId) == true)
            {
                m_CacheManager.PopulateFromCache(asset);
                if (asset.Metadata?.Any() == true)
                    return false;
            }

            // Need to fetch from cloud
            return true;
        }

        async Task FetchAllAsync(List<BaseAssetData> assets, CancellationToken token)
        {
            try
            {
                var tasks = assets.Select(asset => FetchOneAsync(asset, token));
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // Unexpected error - still complete the loading state
            }

            if (token.IsCancellationRequested)
                return;

            IsFetching = false;
            // Use EditorApplication.delayCall to ensure callback runs on main thread
            EditorApplication.delayCall += () => AllFetchesCompleted?.Invoke();
        }

        async Task FetchOneAsync(BaseAssetData asset, CancellationToken token)
        {
            try
            {
                await m_AssetDataManager.GetAssetAsync(asset.Identifier, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Errors are logged internally by GetAssetAsync
            }
        }

        public void Dispose()
        {
            Reset();
            m_Cts = null;
        }
    }

    /// <summary>
    /// Container that manages the inline multi-edit entries (Description, Status, Tags, Custom Metadata)
    /// for the MultiAssetDetailsPage on Assets and In Project tabs.
    /// </summary>
    class InlineMultiEditSection : VisualElement
    {
        const string k_AssetMetadataFoldoutName = "multi-selection-asset-metadata-foldout";
        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";

        readonly IInlineEditService m_InlineEditService;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IStateManager m_StateManager;
        readonly MetadataLoadingTracker m_MetadataLoadingTracker;

        MultiEditTextEntry m_DescriptionEntry;
        MultiEditStatusEntry m_StatusEntry;
        MultiEditTagsEntry m_TagsEntry;
        MultiEditCustomMetadataFoldout m_CustomMetadataFoldout;

        VisualElement m_EntriesContainer;
        EditingMode m_EditingMode;
        IReadOnlyCollection<BaseAssetData> m_SelectedAssets;

        public InlineMultiEditSection(
            IInlineEditService inlineEditService,
            IProjectOrganizationProvider projectOrganizationProvider,
            IStateManager stateManager,
            IAssetDataManager assetDataManager,
            IAssetDataCacheManager cacheManager = null)
        {
            m_InlineEditService = Guard.AgainstNull(inlineEditService, nameof(inlineEditService));
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_StateManager = stateManager;

            m_MetadataLoadingTracker = new MetadataLoadingTracker(assetDataManager, cacheManager);
            m_MetadataLoadingTracker.AllFetchesCompleted += OnAllMetadataFetchesCompleted;

            m_EntriesContainer = new VisualElement();
            m_EntriesContainer.AddToClassList(UssStyle.DetailsPageEntriesContainer);
            Add(m_EntriesContainer);

            UIElementsUtils.Hide(this);
        }

        public void ConfigureEditing(EditingMode mode, string disabledReason = null)
        {
            m_EditingMode = mode;
            tooltip = mode == EditingMode.ReadOnly ? disabledReason : null;

            if (mode == EditingMode.Inline && m_SelectedAssets is { Count: > 1 })
            {
                UIElementsUtils.Show(this);
                RefreshUI();
            }
            else
            {
                UIElementsUtils.Hide(this);
            }
        }

        public void UpdateSelection(IReadOnlyCollection<BaseAssetData> assets)
        {
            // Only save pending edits if the selection actually changed (different assets)
            // This prevents data refresh events from committing in-progress edits
            var selectionChanged = !IsSameSelection(m_SelectedAssets, assets);
            if (selectionChanged)
                SavePendingEdits();

            m_SelectedAssets = assets;

            // Start tracking metadata loading for the new selection
            m_MetadataLoadingTracker.FetchMissingMetadata(assets);

            if (m_EditingMode == EditingMode.Inline && m_SelectedAssets is { Count: > 1 })
            {
                UIElementsUtils.Show(this);
                if (selectionChanged)
                    RefreshUI();
            }
            else
            {
                UIElementsUtils.Hide(this);
            }
        }

        void OnAllMetadataFetchesCompleted()
        {
            if (m_EditingMode == EditingMode.Inline && m_SelectedAssets is { Count: > 1 })
                RefreshUI();
		}

        static bool IsSameSelection(IReadOnlyCollection<BaseAssetData> oldSelection, IReadOnlyCollection<BaseAssetData> newSelection)
        {
            if (oldSelection == null && newSelection == null)
                return true;
            if (oldSelection == null || newSelection == null)
                return false;
            if (oldSelection.Count != newSelection.Count)
                return false;

            var oldIds = new HashSet<string>(oldSelection.Select(a => a.Identifier.AssetId));
            return newSelection.All(a => oldIds.Contains(a.Identifier.AssetId));
        }

        void SavePendingEdits()
        {
            m_DescriptionEntry?.SavePendingEdits();
            m_StatusEntry?.SavePendingEdits();
            m_TagsEntry?.SavePendingEdits();
        }

        static void RefreshAssetMetadataFoldoutExpandedClass(Foldout foldout, bool expanded)
        {
            if (expanded)
                foldout.AddToClassList(k_MultiSelectionFoldoutExpandedClassName);
            else
                foldout.RemoveFromClassList(k_MultiSelectionFoldoutExpandedClassName);
        }

        static void RequestParentScrollViewRefresh(VisualElement from)
        {
            for (var p = from.parent; p != null; p = p.parent)
            {
                if (p is ScrollView sv)
                {
                    sv.schedule.Execute(_ => { sv.verticalScrollerVisibility = ScrollerVisibility.Auto; }).StartingIn(25);
                    break;
                }
            }
        }

        void RefreshUI()
        {
            DisposeEntries();
            m_EntriesContainer.Clear();

            if (m_SelectedAssets == null || m_SelectedAssets.Count < 2)
                return;

            var foldout = new Foldout
            {
                name = k_AssetMetadataFoldoutName,
                viewDataKey = k_AssetMetadataFoldoutName,
                text = L10n.Tr("Asset Metadata")
            };
            foldout.AddToClassList("multi-selection-foldout");
            MultiSelectionFoldout.ApplyMultiSelectionFoldoutToggleLayout(foldout.Q<Toggle>());

            foldout.value = m_StateManager?.MultiEditAssetMetadataFoldoutValue ?? true;
            RefreshAssetMetadataFoldoutExpandedClass(foldout, foldout.value);

            if (m_StateManager != null)
            {
                foldout.RegisterValueChangedCallback(evt =>
                {
                    m_StateManager.MultiEditAssetMetadataFoldoutValue = evt.newValue;
                    RefreshAssetMetadataFoldoutExpandedClass(foldout, evt.newValue);
                    RequestParentScrollViewRefresh(foldout);
                });
            }
            else
            {
                foldout.RegisterValueChangedCallback(evt =>
                {
                    RefreshAssetMetadataFoldoutExpandedClass(foldout, evt.newValue);
                    RequestParentScrollViewRefresh(foldout);
                });
            }

            var foldoutContent = new VisualElement();
            foldoutContent.AddToClassList(UssStyle.DetailsPageEntriesContainer);
            foldout.Add(foldoutContent);
            m_EntriesContainer.Add(foldout);

            m_DescriptionEntry = new MultiEditTextEntry(
                Constants.DescriptionText, m_SelectedAssets, m_InlineEditService, EditField.Description);
            m_DescriptionEntry.SetConfirmationPopupParent(m_EntriesContainer);
            m_DescriptionEntry.ConfigureEditing(EditingMode.Inline);
            foldoutContent.Add(m_DescriptionEntry);

            m_StatusEntry = new MultiEditStatusEntry(
                Constants.StatusText, m_SelectedAssets, m_InlineEditService, EditField.Status);
            m_StatusEntry.SetConfirmationPopupParent(m_EntriesContainer);
            m_StatusEntry.ConfigureEditing(EditingMode.Inline);
            foldoutContent.Add(m_StatusEntry);

            m_TagsEntry = new MultiEditTagsEntry(
                Constants.TagsText, m_SelectedAssets, m_InlineEditService);
            m_TagsEntry.SetConfirmationPopupParent(m_EntriesContainer);
            m_TagsEntry.ConfigureEditing(EditingMode.Inline);
            foldoutContent.Add(m_TagsEntry);

            // Custom metadata stays a sibling foldout (same level as Asset Metadata), not nested inside it.
            m_CustomMetadataFoldout = new MultiEditCustomMetadataFoldout(
                m_EntriesContainer, m_EntriesContainer, m_InlineEditService,
                m_ProjectOrganizationProvider, m_StateManager,
                refreshCallback: () => RefreshUI());

            m_CustomMetadataFoldout.RefreshUI(m_SelectedAssets, EditingMode.Inline, m_MetadataLoadingTracker.IsFetching);
        }

        void DisposeEntries()
        {
            m_DescriptionEntry?.Dispose();
            m_DescriptionEntry = null;
            m_StatusEntry?.Dispose();
            m_StatusEntry = null;
            m_TagsEntry?.Dispose();
            m_TagsEntry = null;
            m_CustomMetadataFoldout?.Dispose();
            m_CustomMetadataFoldout = null;
        }

        /// <summary>
        /// Exposed for testing to verify loading state.
        /// </summary>
        internal bool IsLoadingMetadata => m_MetadataLoadingTracker.IsFetching;
    }
}
