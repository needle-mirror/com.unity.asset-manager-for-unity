using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class Filters : GridTool
    {
        const string k_UssClassName = "unity-filters";
        const string k_ItemButtonClassName = k_UssClassName + "-button";
        const string k_ItemButtonCaretClassName = k_ItemButtonClassName + "-caret";
        const string k_ItemPopupClassName = k_UssClassName + "-popup";
        const string k_ItemFilterSelectionClassName = k_ItemPopupClassName + "-filter-selection";
        const string k_ItemFilterNoSelectionClassName = k_ItemPopupClassName + "-filter-no-selection";
        const string k_ItemChipContainerClassName = k_UssClassName + "-chip-container";
        const string k_ItemChipClassName = k_UssClassName + "-chip";
        const string k_ItemChipSetClassName = k_ItemChipClassName + "--set";
        const string k_ItemChipDeleteClassName = k_ItemChipClassName + "-delete";
        const string k_SelfCenterClassName = "self-center";

        readonly IPopupManager m_PopupManager;
        readonly Dictionary<VisualElement, BaseFilter> m_FilterPerChip = new();

        Button m_FilterButton;

        VisualElement m_ChipContainer;
        VisualElement m_CurrentChip;

        PageFilters PageFilters => m_PageManager?.ActivePage?.PageFilters;
        List<BaseFilter> SelectedFilters => PageFilters?.SelectedFilters ?? new List<BaseFilter>();
        protected override VisualElement Container => m_FilterButton;

        public Filters(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            IPopupManager popupManager): base(pageManager, projectOrganizationProvider)
        {
            m_PopupManager = popupManager;

            AddToClassList(k_UssClassName);

            InitializeUI();
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            if (PageFilters != null)
            {
                PageFilters.EnableStatusChanged += OnEnableStatusChanged;
                PageFilters.FilterApplied += OnFilterApplied;
                PageFilters.FilterAdded += OnFilterAdded;
            }

            m_PopupManager.Container.RegisterCallback<FocusOutEvent>(DeleteEmptyChip);
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            if (PageFilters != null)
            {
                PageFilters.EnableStatusChanged -= OnEnableStatusChanged;
                PageFilters.FilterApplied -= OnFilterApplied;
                PageFilters.FilterAdded -= OnFilterAdded;
            }

            m_PopupManager.Container.UnregisterCallback<FocusOutEvent>(DeleteEmptyChip);
        }

        protected override void OnActivePageChanged(IPage page)
        {
            PageFilters.EnableStatusChanged += OnEnableStatusChanged;
            PageFilters.FilterApplied += OnFilterApplied;
            PageFilters.FilterAdded += OnFilterAdded;
            Refresh();

            base.OnActivePageChanged(page);
        }

        protected override void InitDisplay(IPage page)
        {
            UIElementsUtils.SetDisplay(Container,
                SelectedFilters.Any() || (page?.AssetList?.Any() ?? false));
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
            PageFilters?.ClearFilters();
            m_FilterPerChip?.Clear();

            // The first call of InitializeUI is made into the constructor
            InitializeUI();
        }

        void InitializeUI()
        {
            if (!string.IsNullOrWhiteSpace(m_ProjectOrganizationProvider.MessageData.Message))
                return;

            m_FilterButton = new Button();
            m_FilterButton.AddToClassList(k_ItemButtonClassName);

            Add(m_FilterButton);

            var label = new TextElement();
            label.text = L10n.Tr("Add Filter");
            label.AddToClassList("unity-text-element");
            m_FilterButton.Add(label);

            var caret = new VisualElement();
            caret.AddToClassList(k_ItemButtonCaretClassName);
            m_FilterButton.Add(caret);

            m_ChipContainer = new VisualElement();
            m_ChipContainer.AddToClassList(k_ItemChipContainerClassName);
            Add(m_ChipContainer);

            foreach (var filter in SelectedFilters)
            {
                m_ChipContainer.Add(CreateChipButton(filter, filter.SelectedFilter));
            }

            m_FilterButton.SetEnabled(PageFilters?.IsAvailableFilters() ?? false);
            m_FilterButton.clicked += OnFilterButtonClicked;

            UIElementsUtils.SetDisplay(m_FilterButton, m_PageManager?.ActivePage?.AssetList?.Any() ?? false);
        }

        void OnFilterButtonClicked()
        {
            foreach (var selectedFilter in SelectedFilters)
            {
                selectedFilter.Cancel();
            }

            var availableFilters = PageFilters.GetAvailableFilters() ?? new List<BaseFilter>();

            foreach (var filter in availableFilters)
            {
                var filterSelection = new TextElement();
                filterSelection.AddToClassList(k_ItemFilterSelectionClassName);
                filterSelection.text = filter.DisplayName;
                filterSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    AddFilter(filter);
                });

                m_PopupManager.Container.Add(filterSelection);
            }

            m_PopupManager.Show(m_FilterButton, PopupContainer.PopupAlignment.BottomLeft);

            AnalyticsSender.SendEvent(new FilterDropdownEvent());
        }

        Button CreateChipButton(BaseFilter filter, string selection = null)
        {
            var Chip = new Button();
            Chip.AddToClassList(k_ItemChipClassName);
            Chip.clicked += () => OnChipClicked(Chip, filter);
            m_ChipContainer.Add(Chip);

            var label = new TextElement();
            label.name = "label";
            if (string.IsNullOrEmpty(selection))
            {
                label.text = filter.DisplayName;
            }
            else
            {
                label.text = $"{filter.DisplayName}  |  {selection}";
                Chip.AddToClassList(k_ItemChipSetClassName);
            }

            Chip.Add(label);

            var delete = new Image();
            delete.AddToClassList(k_ItemChipDeleteClassName);
            delete.AddManipulator(new Clickable(() => OnChipDeleteClicked(Chip, filter)));
            Chip.Add(delete);

            m_FilterPerChip.TryAdd(Chip, filter);

            return Chip;
        }

        void AddFilter(BaseFilter filter)
        {
            m_PopupManager.Hide();

            PageFilters.AddFilter(filter, true);
            m_FilterButton.SetEnabled(PageFilters.IsAvailableFilters());
        }

        void WaitUntilChipIsPositioned(Button Chip, BaseFilter filter)
        {
            if (Chip.resolvedStyle.top == 0)
            {
                EditorApplication.delayCall += () => WaitUntilChipIsPositioned(Chip, filter);
                return;
            }

            OnChipClicked(Chip, filter);
        }

        void OnChipClicked(Button Chip, BaseFilter filter)
        {
            m_CurrentChip = Chip;

            m_PopupManager.Show(Chip, PopupContainer.PopupAlignment.BottomLeft);

            var loadingLabel = new TextElement();
            loadingLabel.text = L10n.Tr(Constants.LoadingText);
            loadingLabel.AddToClassList(k_SelfCenterClassName);
            m_PopupManager.Container.Add(loadingLabel);

            TaskUtils.TrackException(AddTextFilterSelectionItems(Chip, filter));
        }

        void OnChipDeleteClicked(Button Chip, BaseFilter filter)
        {
            m_FilterPerChip.Remove(Chip);
            Chip.RemoveFromHierarchy();
            PageFilters.RemoveFilter(filter);
            m_FilterButton.SetEnabled(PageFilters.IsAvailableFilters());

            ApplyFilter(filter, null);

            m_PopupManager.Hide();
        }

        async Task AddTextFilterSelectionItems(Button Chip, BaseFilter filter)
        {
            var selections = await filter.GetSelections();
            if (selections == null)
                return;

            m_PopupManager.Clear();

            if (selections.Any())
            {
                var scrollView = new ScrollView();
                m_PopupManager.Container.Add(scrollView);

                foreach (var selection in selections)
                {
                    var filterSelection = new VisualElement();
                    filterSelection.AddToClassList(k_ItemFilterSelectionClassName);
                    filterSelection.style.paddingLeft = 0;

                    var checkmark = new Image();
                    filterSelection.Add(checkmark);

                    var label = new TextElement();
                    label.text = selection;
                    filterSelection.Add(label);

                    checkmark.visible = filter.SelectedFilter == selection;

                    filterSelection.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();

                        Chip.AddToClassList(k_ItemChipSetClassName);
                        m_PopupManager.Hide();

                        ApplyFilter(filter, selection);
                    });

                    scrollView.Add(filterSelection);
                }
            }
            else
            {
                var noSelection = new TextElement();
                noSelection.AddToClassList(k_ItemFilterNoSelectionClassName);
                noSelection.text = L10n.Tr(Constants.NoSelectionsText);
                m_PopupManager.Container.Add(noSelection);
            }
        }

        void ApplyFilter(BaseFilter filter, string selection)
        {
            if (selection != null)
            {
                AnalyticsSender.SendEvent(new FilterSearchEvent(filter.DisplayName, selection));
                m_PageManager.ActivePage.LoadingStatusChanged += OnActivePageLoadingStatusChanged;
            }

            PageFilters.ApplyFilter(filter, selection);
        }

        void DeleteEmptyChip(FocusOutEvent evt)
        {
            if (evt.relatedTarget != null && (evt.relatedTarget == m_PopupManager.Container ||
                    m_PopupManager.Container.Contains((VisualElement)evt.relatedTarget)))
                return;

            if (m_CurrentChip != null)
            {
                if (m_FilterPerChip.TryGetValue(m_CurrentChip, out var filter))
                {
                    if (string.IsNullOrEmpty(filter.SelectedFilter))
                    {
                        m_FilterPerChip.Remove(m_CurrentChip);
                        m_CurrentChip.RemoveFromHierarchy();
                        PageFilters.RemoveFilter(filter);
                        m_FilterButton.SetEnabled(PageFilters.IsAvailableFilters());
                    }
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
                        { FilterName = filter.DisplayName, FilterValue = filter.SelectedFilter });
                }

                AnalyticsSender.SendEvent(
                    new FilterSearchResultEvent(filters, m_PageManager.ActivePage.AssetList.Count));
            }
        }

        void OnEnableStatusChanged(bool isEnabled)
        {
            m_FilterButton?.SetEnabled(isEnabled);
        }

        void OnFilterAdded(BaseFilter filter, bool showSelection)
        {
            var Chip = CreateChipButton(filter);

            if (showSelection)
            {
                WaitUntilChipIsPositioned(Chip, filter);
            }
        }

        void OnFilterApplied(BaseFilter filter)
        {
            foreach (var keyValuePair in m_FilterPerChip)
            {
                if (keyValuePair.Value.GetType() == filter.GetType())
                {
                    keyValuePair.Key.Q<TextElement>("label").text = $"{filter.DisplayName}  |  {filter.SelectedFilter}";
                }
            }
        }
    }
}
