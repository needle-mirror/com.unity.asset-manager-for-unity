using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal class Filters : VisualElement
{
    const string k_UssClassName = "unity-filters";
    const string k_ItemButtonClassName = k_UssClassName + "-button";
    const string k_ItemButtonCaretClassName = k_ItemButtonClassName + "-caret";
    const string k_ItemPopupClassName = k_UssClassName + "-popup";
    const string k_ItemFilterSelectionClassName = k_ItemPopupClassName + "-filter-selection";
    const string k_ItemPillContainerClassName = k_UssClassName + "-pill-container";
    const string k_ItemPillClassName = k_UssClassName + "-pill";
    const string k_ItemPillSetClassName = k_ItemPillClassName + "--set";
    const string k_ItemPillDeleteClassName = k_ItemPillClassName + "-delete";
    const string k_SelfCenterClassName = "self-center";
    const string k_CaretDownFillImage = "Caret-Down-Fill.png";

    readonly IPageManager m_PageManager;
    readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

    VisualElement m_PillContainer;
    VisualElement m_PopupContainer;
    Button m_FilterButton;

    PageFilters pageFilters => m_PageManager?.activePage?.pageFilters;
    List<BaseFilter> selectedFilters => pageFilters?.selectedFilters ?? new List<BaseFilter>();
    VisualElement popupContainer => m_PopupContainer ?? CreatePopupContainer();

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
        m_PageManager.onActivePageChanged += OnActivePageChanged;
    }

    void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        m_PageManager.onActivePageChanged -= OnActivePageChanged;
    }

    void OnActivePageChanged(IPage page)
    {
        Refresh();
    }

    void Refresh()
    {
        Clear();
        pageFilters?.ClearFilters();

        InitializeUI();
    }

    void InitializeUI()
    {
        if (!string.IsNullOrWhiteSpace(m_ProjectOrganizationProvider.errorOrMessageHandlingData.message))
        {
            return;
        }

        m_FilterButton = new Button();
        m_FilterButton.AddToClassList(k_ItemButtonClassName);
        m_FilterButton.clicked += OnFilterButtonClicked;
        Add(m_FilterButton);

        var label = new TextElement();
        label.text = L10n.Tr("Add Filter");
        label.AddToClassList("unity-text-element");
        m_FilterButton.Add(label);

        var caret = new Image();
        caret.image = UIElementsUtils.GetCategoryIcon(k_CaretDownFillImage);
        caret.AddToClassList(k_ItemButtonCaretClassName);
        m_FilterButton.Add(caret);

        m_PillContainer = new VisualElement();
        m_PillContainer.AddToClassList(k_ItemPillContainerClassName);
        Add(m_PillContainer);

        foreach (var filter in selectedFilters)
        {
            m_PillContainer.Add(CreatePillButton(filter, filter.SelectedFilter));
        }

        m_FilterButton.SetEnabled(pageFilters?.IsAvailableFilters() ?? false);
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
        });

        return m_PopupContainer;
    }

    void OnFilterButtonClicked()
    {
        foreach (var selectedFilter in selectedFilters)
        {
            selectedFilter.Cancel();
        }

        popupContainer.Clear();

        SetPopupPosition(m_FilterButton);

        var availableFilters = pageFilters.GetAvailableFilters() ?? new List<BaseFilter>();

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

            popupContainer.Add(filterSelection);
        }

        UIElementsUtils.Show(popupContainer);
        popupContainer.Focus();

        AnalyticsSender.SendEvent(new FilterDropdownEvent());
    }

    void SetPopupPosition(VisualElement item)
    {
        var worldPos = item.LocalToWorld(Vector2.zero);
        var localPos = popupContainer.parent.WorldToLocal(worldPos);

        popupContainer.style.left = localPos.x;
        popupContainer.style.top = localPos.y + item.resolvedStyle.height;
    }

    Button CreatePillButton(BaseFilter filter, string selection = null)
    {
        var pill = new Button();
        pill.AddToClassList(k_ItemPillClassName);
        pill.clicked += () => OnPillClicked(pill, filter);
        m_PillContainer.Add(pill);

        var label = new TextElement();
        label.name = "label";
        if (string.IsNullOrEmpty(selection))
        {
            label.text = filter.DisplayName;
        }
        else
        {
            label.text = $"{filter.DisplayName}  |  {selection}";
            pill.AddToClassList(k_ItemPillSetClassName);
        }

        pill.Add(label);

        var delete = new Image();
        delete.image = UIElementsUtils.GetCategoryIcon("Close.png");
        delete.AddToClassList(k_ItemPillDeleteClassName);
        delete.AddManipulator(new Clickable(() => OnPillDeleteClicked(pill, filter)));
        pill.Add(delete);

        return pill;
    }

    void AddFilter(BaseFilter filter)
    {
        UIElementsUtils.Hide(popupContainer);

        pageFilters.AddFilter(filter);
        m_FilterButton.SetEnabled(pageFilters.IsAvailableFilters());

        var pill = CreatePillButton(filter);
        WaitUntilPillIsPositioned(pill, filter);
    }

    void WaitUntilPillIsPositioned(Button pill, BaseFilter filter)
    {
        if (pill.resolvedStyle.top == 0)
        {
            EditorApplication.delayCall += () => WaitUntilPillIsPositioned(pill, filter);
            return;
        }

        OnPillClicked(pill, filter);
    }

    void OnPillClicked(Button pill, BaseFilter filter)
    {
        UIElementsUtils.Show(popupContainer);
        popupContainer.Focus();
        SetPopupPosition(pill);
        popupContainer.Clear();

        var loadingLabel = new TextElement();
        loadingLabel.text = L10n.Tr("Loading...");
        loadingLabel.AddToClassList(k_SelfCenterClassName);
        popupContainer.Add(loadingLabel);

        _ = AddTextFilterSelectionItems(pill, filter);
    }

    void OnPillDeleteClicked(Button pill, BaseFilter filter)
    {
        pill.RemoveFromHierarchy();
        pageFilters.RemoveFilter(filter);
        m_FilterButton.SetEnabled(pageFilters.IsAvailableFilters());

        ApplyFilter(filter, null);

        UIElementsUtils.Hide(popupContainer);
    }

    async Task AddTextFilterSelectionItems(Button pill, BaseFilter filter)
    {
        List<string> selections = await filter.GetSelections();
        if (selections == null)
            return;

        popupContainer.Clear();
        foreach (var selection in selections)
        {
            var filterSelection = new VisualElement();
            filterSelection.AddToClassList(k_ItemFilterSelectionClassName);

            var checkmark = new Image();
            checkmark.image = UIElementsUtils.GetCategoryIcon("check.png");
            filterSelection.Add(checkmark);

            var label = new TextElement();
            label.text = selection;
            filterSelection.Add(label);

            checkmark.visible = filter.SelectedFilter == selection;

            filterSelection.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                pill.Q<TextElement>("label").text = $"{filter.DisplayName}  |  {selection}";
                pill.AddToClassList(k_ItemPillSetClassName);
                UIElementsUtils.Hide(popupContainer);

                ApplyFilter(filter, selection);
            });

            popupContainer.Add(filterSelection);
        }
    }

    void ApplyFilter(BaseFilter filter, string selection)
    {
        if (selection != null)
        {
            AnalyticsSender.SendEvent(new FilterSearchEvent(filter.DisplayName, selection));
            m_PageManager.activePage.onLoadingStatusChanged += OnLoadingStatusChanged;
        }

        pageFilters.ApplyFilter(filter, selection);
    }

    void OnLoadingStatusChanged(bool isLoading)
    {
        if (!isLoading)
        {
            m_PageManager.activePage.onLoadingStatusChanged -= OnLoadingStatusChanged;
            var filters = new List<FilterSearchResultEvent.FilterData>();
            foreach (var filter in selectedFilters)
            {
                filters.Add(new FilterSearchResultEvent.FilterData{ FilterName = filter.DisplayName, FilterValue = filter.SelectedFilter});
            }
            AnalyticsSender.SendEvent(new FilterSearchResultEvent(filters, m_PageManager.activePage.assetList.Count));
        }
    }
}
