using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters : GridTool
    {
        const string k_SelfCenter = "self-center";
        const int k_ShowSearchBarThreshold = 10;

        readonly IApplicationProxy m_ApplicationProxy;
        readonly IPopupManager m_PopupManager;

        readonly Dictionary<VisualElement, BaseFilter> m_FilterPerChip = new();

        Button m_FilterButton;

        VisualElement m_ChipContainer;
        VisualElement m_CurrentChip;

        IPageFilterStrategy PageFilterStrategy => m_PageManager?.PageFilterStrategy;
        List<BaseFilter> SelectedFilters => PageFilterStrategy?.SelectedFilters ?? new List<BaseFilter>();
        protected override VisualElement Container => m_FilterButton;

        Task m_AddSelectionTask;
        CancellationTokenSource m_CancellationTokenSource;

        public Filters(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            IApplicationProxy applicationProxy, IPopupManager popupManager)
            : base(pageManager, projectOrganizationProvider)
        {
            m_ApplicationProxy = applicationProxy;
            m_PopupManager = popupManager;

            AddToClassList(UssStyle.k_Filter);

            InitializeUI();
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            if (PageFilterStrategy != null)
            {
                PageFilterStrategy.SavedFilterApplied += Refresh;
                PageFilterStrategy.EnableStatusChanged += OnEnableStatusChanged;
                PageFilterStrategy.FilterApplied += OnFilterApplied;
                PageFilterStrategy.FilterAdded += OnFilterAdded;
                PageFilterStrategy.FiltersCleared += Refresh;
            }

            m_PopupManager.Container.RegisterCallback<FocusOutEvent>(DeleteEmptyChip);
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            if (PageFilterStrategy != null)
            {
                PageFilterStrategy.SavedFilterApplied -= Refresh;
                PageFilterStrategy.EnableStatusChanged -= OnEnableStatusChanged;
                PageFilterStrategy.FilterApplied -= OnFilterApplied;
                PageFilterStrategy.FilterAdded -= OnFilterAdded;
                PageFilterStrategy.FiltersCleared -= Refresh;
            }

            m_PopupManager.Container.UnregisterCallback<FocusOutEvent>(DeleteEmptyChip);
        }

        protected override void OnActivePageChanged(IPage page)
        {
            Refresh();
            InitDisplay(page);
        }

        protected override void InitDisplay(IPage page)
        {
            var display = m_ProjectOrganizationProvider.SelectedOrganization != null &&
                          m_ProjectOrganizationProvider.SelectedOrganization.ProjectInfos.Any() &&
                          m_ProjectOrganizationProvider.SelectedAssetLibrary == null;

            UIElementsUtils.SetDisplay(Container, display);
        }

        protected override bool IsDisplayed(IPage page)
        {
            if (page is BasePage basePage)
            {
                return basePage.DisplayFilters;
            }

            return base.IsDisplayed(page);
        }

        void Refresh()
        {
            Clear();
            m_FilterPerChip.Clear();
            m_PopupManager.Clear();

            m_CancellationTokenSource?.Cancel();
            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = null;

            // The first call of InitializeUI is made into the constructor
            InitializeUI();
        }

        void InitializeUI()
        {
            m_FilterButton = new Button();
            m_FilterButton.AddToClassList(UssStyle.k_FilterItemButton);

            Add(m_FilterButton);

            var label = new TextElement();
            label.text = L10n.Tr("Add Filter");
            label.AddToClassList("unity-text-element");
            m_FilterButton.Add(label);

            var caret = new VisualElement();
            caret.AddToClassList(UssStyle.k_FilterItemButtonCaret);
            m_FilterButton.Add(caret);

            m_ChipContainer = new VisualElement();
            m_ChipContainer.AddToClassList(UssStyle.k_FilterItemChipContainer);
            Add(m_ChipContainer);

            foreach (var filter in SelectedFilters)
            {
                m_ChipContainer.Add(CreateChipButton(filter));
            }

            m_FilterButton.SetEnabled(PageFilterStrategy?.IsAvailableFilters() ?? false);
            m_FilterButton.clicked += OnFilterButtonClicked;

            InitDisplay(m_PageManager.ActivePage);
        }

        void OnFilterButtonClicked()
        {
            PageFilterStrategy.Cancel();

            var availablePrimaryMetadataFilters =
                PageFilterStrategy.GetAvailablePrimaryMetadataFilters() ?? new List<BaseFilter>();
            var availableCustomMetadataFilters =
                PageFilterStrategy.GetAvailableCustomMetadataFilters() ?? new List<CustomMetadataFilter>();

            var filterSelections = new ScrollView();

            if (availablePrimaryMetadataFilters.Count + availableCustomMetadataFilters.Count >= k_ShowSearchBarThreshold)
            {
                var searchBar = new ToolbarSearchField();
                searchBar.AddToClassList(UssStyle.k_FilterSelectionSearchBar);
                searchBar.RegisterValueChangedCallback(evt =>
                {
                    filterSelections.Clear();

                    var search = evt.newValue.ToLower();
                    if (string.IsNullOrEmpty(search))
                    {
                        BuildSelections(availablePrimaryMetadataFilters, availableCustomMetadataFilters, filterSelections);
                        return;
                    }

                    var filteredPrimaryMetadataFilters = availablePrimaryMetadataFilters
                        .Where(filter => filter.DisplayName.ToLower().Contains(search.ToLower())).ToList();
                    var filteredCustomMetadataFilters = availableCustomMetadataFilters
                        .Where(filter => filter.DisplayName.ToLower().Contains(search.ToLower())).ToList();

                    BuildSelections(filteredPrimaryMetadataFilters, filteredCustomMetadataFilters, filterSelections);
                });
                m_PopupManager.Container.Add(searchBar);
            }

            m_PopupManager.Container.Add(filterSelections);

            BuildSelections(availablePrimaryMetadataFilters, availableCustomMetadataFilters, filterSelections);

            m_PopupManager.Show(m_FilterButton, PopupContainer.PopupAlignment.BottomLeft);

            AnalyticsSender.SendEvent(new FilterDropdownEvent(m_PageManager.ActivePage.Title));
        }

        void BuildSelections(List<BaseFilter> availablePrimaryMetadataFilters, List<CustomMetadataFilter> availableCustomMetadataFilters, ScrollView filterSelections)
        {
            var showTitle = PageFilterStrategy.HasCustomMetadataFilters;

            if (showTitle && availablePrimaryMetadataFilters.Any())
            {
                var primaryLabel = new Label(L10n.Tr(Constants.PrimaryMetadata));
                primaryLabel.AddToClassList(UssStyle.k_FilterSectionLabel);
                filterSelections.Add(primaryLabel);
            }

            AddFilterSelections(availablePrimaryMetadataFilters, filterSelections);

            if (showTitle && availableCustomMetadataFilters.Any())
            {
                var customLabel = new Label(L10n.Tr(Constants.CustomMetadata));
                customLabel.AddToClassList(UssStyle.k_FilterSectionLabel);
                if (availablePrimaryMetadataFilters.Any())
                {
                    customLabel.AddToClassList(UssStyle.k_FilterSectionLabelOther);
                }
                filterSelections.Add(customLabel);
            }

            AddFilterSelections(availableCustomMetadataFilters, filterSelections);
        }

        void AddFilterSelections(IEnumerable<BaseFilter> availableFilters, ScrollView filterSelections)
        {
            foreach (var filter in availableFilters)
            {
                var filterSelection = new TextElement();
                filterSelection.AddToClassList(UssStyle.k_FilterItemSelection);
                filterSelection.text = filter.DisplayName;
                filterSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    AddFilter(filter);
                });

                filterSelections.Add(filterSelection);
            }
        }

        Button CreateChipButton(BaseFilter filter)
        {
            var chip = new Button();
            chip.AddToClassList(UssStyle.k_FilterItemChip);
            chip.clicked += () => OnChipClicked(chip, filter);
            m_ChipContainer.Add(chip);

            var label = new TextElement();
            label.name = "label";
            label.text = filter.DisplaySelectedFilters();
            if (filter.SelectedFilters != null && filter.SelectedFilters.Any())
            {
                chip.AddToClassList(UssStyle.k_FilterItemChipSet);
            }

            chip.Add(label);

            var delete = new Image();
            delete.AddToClassList(UssStyle.k_FilterItemChipDelete);
            delete.AddManipulator(new Clickable(() => OnChipDeleteClicked(chip, filter)));
            chip.Add(delete);

            m_FilterPerChip.TryAdd(chip, filter);

            return chip;
        }

        void AddFilter(BaseFilter filter)
        {
            m_PopupManager.Hide();

            PageFilterStrategy.AddFilter(filter, true);
            m_FilterButton.SetEnabled(PageFilterStrategy.IsAvailableFilters());
        }

        void WaitUntilChipIsPositioned(Button chip, BaseFilter filter)
        {
            if (UnityEngine.Mathf.Approximately(chip.resolvedStyle.top, 0))
            {
                m_ApplicationProxy.DelayCall += () => WaitUntilChipIsPositioned(chip, filter);
                return;
            }

            OnChipClicked(chip, filter);
        }

        void OnChipClicked(Button chip, BaseFilter filter)
        {
            m_CurrentChip = chip;

            m_PopupManager.Show(chip, PopupContainer.PopupAlignment.BottomLeft);

            var loadingLabel = new TextElement();
            loadingLabel.text = L10n.Tr(Constants.LoadingText);
            loadingLabel.AddToClassList(k_SelfCenter);
            m_PopupManager.Container.Add(loadingLabel);

            m_CancellationTokenSource?.Cancel();
            m_CancellationTokenSource?.Dispose();
            m_CancellationTokenSource = new CancellationTokenSource();
            m_AddSelectionTask = AddFilterSelectionItems(chip, filter, m_CancellationTokenSource.Token);

            TaskUtils.TrackException(m_AddSelectionTask);
        }

        void OnChipDeleteClicked(Button chip, BaseFilter filter)
        {
            m_FilterPerChip.Remove(chip);
            chip.RemoveFromHierarchy();
            PageFilterStrategy.RemoveFilter(filter);
            m_FilterButton.SetEnabled(PageFilterStrategy.IsAvailableFilters());

            ApplyFilter(filter, null);

            m_PopupManager.Hide();
        }

        async Task AddFilterSelectionItems(Button chip, BaseFilter filter, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            switch (filter.SelectionType)
            {
                case FilterSelectionType.SingleSelection:
                    await AddSingleSelectionItems(chip, filter, cancellationToken);
                    break;
                case FilterSelectionType.MultiSelection:
                    await AddMultiSelectionItems(chip, filter, cancellationToken);
                    break;
                case FilterSelectionType.AdvancedMultiSelection:
                    await AddAdvancedMultiSelection(chip, filter, cancellationToken);
                    break;
                case FilterSelectionType.Number:
                    AddNumberSelection(chip, filter);
                    break;
                case FilterSelectionType.NumberRange:
                    AddRangeNumberSelection(chip, filter);
                    break;
                case FilterSelectionType.Timestamp:
                    AddRangeTimestampSelection(chip, filter);
                    break;
                case FilterSelectionType.Text:
                    AddTextSelection(chip, filter);
                    break;
                case FilterSelectionType.Url:
                    AddUrlSelection(chip, filter);
                    break;
                case FilterSelectionType.MultiText:
                    AddMultiTextSelection(chip, filter);
                    break;
                case FilterSelectionType.DateSelection:
                    AddDateSelection(chip, filter);
                    break;
                default:
                    Utilities.DevLogError($"{filter.SelectionType} is not supported");
                    break;
            }
        }

        (Button clearButton, Button applyButton) AddButtons(Button chip, BaseFilter filter, Func<List<string>> applySelection, Action clearAction)
        {
            var line = new VisualElement();
            line.AddToClassList(UssStyle.k_FilterSeparatorLine);
            m_PopupManager.Container.Add(line);

            var buttonContainer = new VisualElement();
            buttonContainer.AddToClassList(UssStyle.k_FilterSelectionContainer);
            m_PopupManager.Container.Add(buttonContainer);

            var clearButton = new Button();
            clearButton.AddToClassList(UssStyle.k_FilterSelectionButton);
            clearButton.text = L10n.Tr(Constants.Clear);
            clearButton.clicked += clearAction;
            buttonContainer.Add(clearButton);

            var applyButton = new Button();
            applyButton.AddToClassList(UssStyle.k_FilterSelectionButton);
            applyButton.text = L10n.Tr(Constants.Apply);
            applyButton.clicked += () =>
            {
                var selectedFilters = applySelection();
                if(selectedFilters == null || !selectedFilters.Any())
                {
                    return;
                }

                chip.AddToClassList(UssStyle.k_FilterItemChipSet);
                m_PopupManager.Hide();

                ApplyFilter(filter, selectedFilters);
            };
            buttonContainer.Add(applyButton);

            return (clearButton, applyButton);
        }

        void ApplyFilter(BaseFilter filter, List<string> selectedFilters)
        {
            if (selectedFilters != null)
            {
                AnalyticsSender.SendEvent(new FilterSearchEvent(filter.DisplayName, selectedFilters, m_PageManager.ActivePage.Title));
                m_PageManager.ActivePage.LoadingStatusChanged += OnActivePageLoadingStatusChanged;
            }

            PageFilterStrategy.ApplyFilter(filter, selectedFilters);
        }

        void DeleteEmptyChip(FocusOutEvent evt)
        {
            if (evt.relatedTarget != null && (evt.relatedTarget == m_PopupManager.Container ||
                    m_PopupManager.Container.Contains((VisualElement)evt.relatedTarget)))
                return;

            if (m_CurrentChip != null)
            {
                m_CancellationTokenSource?.Cancel();
                m_CancellationTokenSource?.Dispose();
                m_CancellationTokenSource = null;

                if (m_FilterPerChip.TryGetValue(m_CurrentChip, out var filter) &&
                    (filter.SelectedFilters == null || !filter.SelectedFilters.Any()))
                {
                    m_FilterPerChip.Remove(m_CurrentChip);
                    m_CurrentChip.RemoveFromHierarchy();
                    PageFilterStrategy.RemoveFilter(filter);
                    m_FilterButton.SetEnabled(PageFilterStrategy.IsAvailableFilters());
                }

                m_CurrentChip = null;
            }
        }

        void OnActivePageLoadingStatusChanged(bool isLoading)
        {
            if (!isLoading)
            {
                m_PageManager.ActivePage.LoadingStatusChanged -= OnActivePageLoadingStatusChanged;
                var filters = new List<FilterSearchResultEvent.FilterData>();
                foreach (var filter in SelectedFilters)
                {
                    filters.Add(new FilterSearchResultEvent.FilterData
                        { FilterName = filter.DisplayName, FilterValue = filter.SelectedFilters != null ? string.Join(",", filter.SelectedFilters) : null });
                }

                AnalyticsSender.SendEvent(new FilterSearchResultEvent(filters, m_PageManager.ActivePage.AssetList.Count, m_PageManager.ActivePage.Title));
            }
        }

        void OnEnableStatusChanged(bool isEnabled)
        {
            m_FilterButton?.SetEnabled(isEnabled);
        }

        void OnFilterAdded(BaseFilter filter, bool showSelection)
        {
            var chip = CreateChipButton(filter);

            if (showSelection)
            {
                WaitUntilChipIsPositioned(chip, filter);
            }
        }

        void OnFilterApplied(BaseFilter filter)
        {
            foreach (var keyValuePair in m_FilterPerChip)
            {
                if (keyValuePair.Value.GetType() == filter.GetType())
                {
                    keyValuePair.Key.Q<TextElement>("label").text = filter.DisplaySelectedFilters();
                }
            }
        }
    }
}
