using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class Filters : VisualElement
    {
        const string k_UssClassName = "unity-filters";
        const string k_ItemButtonClassName = k_UssClassName + "-button";
        const string k_ItemButtonCaretClassName = k_ItemButtonClassName + "-caret";
        const string k_ItemPopupClassName = k_UssClassName + "-popup";
        const string k_ItemFilterSelectionClassName = k_ItemPopupClassName + "-filter-selection";
        const string k_ItemChipContainerClassName = k_UssClassName + "-chip-container";
        const string k_ItemChipClassName = k_UssClassName + "-chip";
        const string k_ItemChipSetClassName = k_ItemChipClassName + "--set";
        const string k_ItemChipDeleteClassName = k_ItemChipClassName + "-delete";
        const string k_SelfCenterClassName = "self-center";

        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly Dictionary<VisualElement, BaseFilter> m_FilterPerChip = new();

        Button m_FilterButton;
        PageFilters m_OldPageFilters;

        VisualElement m_ChipContainer;
        VisualElement m_PopupContainer;
        VisualElement m_CurrentChip;

        PageFilters PageFilters => m_PageManager?.ActivePage?.PageFilters;
        List<BaseFilter> SelectedFilters => PageFilters?.SelectedFilters ?? new List<BaseFilter>();
        VisualElement PopupContainer => m_PopupContainer ?? CreatePopupContainer();

        public Filters(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            AddToClassList(k_UssClassName);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            InitializeUI();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            if (PageFilters != null)
            {
                PageFilters.EnableStatusChanged += OnEnableStatusChanged;
                PageFilters.FilterApplied += OnFilterApplied;
                PageFilters.FilterAdded += OnFilterAdded;
            }
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;

            if (PageFilters != null)
            {
                PageFilters.EnableStatusChanged -= OnEnableStatusChanged;
                PageFilters.FilterApplied -= OnFilterApplied;
                PageFilters.FilterAdded -= OnFilterAdded;
            }
        }

        void OnActivePageChanged(IPage page)
        {
            if (m_OldPageFilters != null)
            {
                m_OldPageFilters.EnableStatusChanged -= OnEnableStatusChanged;
                m_OldPageFilters.FilterApplied -= OnFilterApplied;
                m_OldPageFilters.FilterAdded -= OnFilterAdded;
            }

            PageFilters.EnableStatusChanged += OnEnableStatusChanged;
            PageFilters.FilterApplied += OnFilterApplied;
            PageFilters.FilterAdded += OnFilterAdded;
            Refresh();
        }

        void Refresh()
        {
            Clear();
            PageFilters?.ClearFilters();
            m_FilterPerChip?.Clear();

            InitializeUI();
        }

        void InitializeUI()
        {
            if (!string.IsNullOrWhiteSpace(m_ProjectOrganizationProvider.ErrorOrMessageHandlingData.Message))
                return;

            m_FilterButton = new Button();
            m_FilterButton.AddToClassList(k_ItemButtonClassName);
            m_FilterButton.clicked += OnFilterButtonClicked;
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
        }

        VisualElement CreatePopupContainer()
        {
            m_PopupContainer = new VisualElement();
            m_PopupContainer.focusable = true;
            m_PopupContainer.AddToClassList(k_ItemPopupClassName);
            UIElementsUtils.Hide(m_PopupContainer);
            parent.Add(m_PopupContainer);

            m_PopupContainer.RegisterCallback<FocusOutEvent>(e =>
            {
                UIElementsUtils.Hide(m_PopupContainer);
                DeleteEmptyChip();
            });

            return m_PopupContainer;
        }

        void OnFilterButtonClicked()
        {
            foreach (var selectedFilter in SelectedFilters)
            {
                selectedFilter.Cancel();
            }

            PopupContainer.Clear();

            SetPopupPosition(m_FilterButton);

            var availableFilters = PageFilters.GetAvailableFilters() ?? new List<BaseFilter>();

            foreach (var filter in availableFilters)
            {
                var filterSelection = new TextElement();
                filterSelection.style.paddingLeft = 24; // Add padding to match the checkmark icon
                filterSelection.AddToClassList(k_ItemFilterSelectionClassName);
                filterSelection.text = filter.DisplayName;
                filterSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    AddFilter(filter);
                });

                PopupContainer.Add(filterSelection);
            }

            UIElementsUtils.Show(PopupContainer);
            PopupContainer.Focus();

            AnalyticsSender.SendEvent(new FilterDropdownEvent());
        }

        void SetPopupPosition(VisualElement item)
        {
            var worldPos = item.LocalToWorld(Vector2.zero);
            var localPos = PopupContainer.parent.WorldToLocal(worldPos);

            PopupContainer.style.left = localPos.x;
            PopupContainer.style.top = localPos.y + item.resolvedStyle.height;
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
            UIElementsUtils.Hide(PopupContainer);

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

            UIElementsUtils.Show(PopupContainer);
            PopupContainer.Focus();
            SetPopupPosition(Chip);
            PopupContainer.Clear();

            var loadingLabel = new TextElement();
            loadingLabel.text = L10n.Tr("Loading...");
            loadingLabel.AddToClassList(k_SelfCenterClassName);
            PopupContainer.Add(loadingLabel);

            _ = AddTextFilterSelectionItems(Chip, filter);
        }

        void OnChipDeleteClicked(Button Chip, BaseFilter filter)
        {
            m_FilterPerChip.Remove(Chip);
            Chip.RemoveFromHierarchy();
            PageFilters.RemoveFilter(filter);
            m_FilterButton.SetEnabled(PageFilters.IsAvailableFilters());

            ApplyFilter(filter, null);

            UIElementsUtils.Hide(PopupContainer);
        }

        async Task AddTextFilterSelectionItems(Button Chip, BaseFilter filter)
        {
            var selections = await filter.GetSelections();
            if (selections == null)
                return;

            PopupContainer.Clear();

            foreach (var selection in selections)
            {
                var filterSelection = new VisualElement();
                filterSelection.AddToClassList(k_ItemFilterSelectionClassName);

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
                    UIElementsUtils.Hide(PopupContainer);

                    ApplyFilter(filter, selection);
                });

                PopupContainer.Add(filterSelection);
            }
        }

        void ApplyFilter(BaseFilter filter, string selection)
        {
            if (selection != null)
            {
                AnalyticsSender.SendEvent(new FilterSearchEvent(filter.DisplayName, selection));
                m_PageManager.ActivePage.LoadingStatusChanged += OnLoadingStatusChanged;
            }

            PageFilters.ApplyFilter(filter, selection);
        }

        void DeleteEmptyChip()
        {
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

        void OnLoadingStatusChanged(bool isLoading)
        {
            if (!isLoading)
            {
                m_PageManager.ActivePage.LoadingStatusChanged -= OnLoadingStatusChanged;
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
