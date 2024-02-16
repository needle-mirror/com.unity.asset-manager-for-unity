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

    readonly List<BaseFilter> m_FilterTypes;
    readonly List<BaseFilter> m_SelectedFilters = new();

    readonly IPageManager m_PageManager;
    readonly IAssetsProvider m_AssetsProvider;
    readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
    readonly IAnalyticsEngine m_AnalyticsEngine;

    string m_LastSelectedFilter;
    string m_LastSelectedFilterSelection;

    VisualElement m_PillContainer;
    VisualElement m_PopupContainer;
    Button m_FilterButton;

    public Filters(IPageManager pageManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider, IAnalyticsEngine analyticsEngine)
    {
        m_PageManager = pageManager;
        m_AssetsProvider = assetsProvider;
        m_ProjectOrganizationProvider = projectOrganizationProvider;
        m_AnalyticsEngine = analyticsEngine;

        m_FilterTypes = new List<BaseFilter>
        {
            new StatusFilter(m_ProjectOrganizationProvider, m_AssetsProvider),
            new UnityTypeFilter(m_PageManager)
        };

        AddToClassList(k_UssClassName);

        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        Refresh();
    }

    void OnAttachToPanel(AttachToPanelEvent evt)
    {
        m_ProjectOrganizationProvider.onProjectSelectionChanged += OnProjectSelectionChanged;
        m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
        m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged += OnProjectInfoOrLoadingChanged;
        m_PageManager.onActivePageChanged += OnActivePageChanged;
        m_PageManager.onSearchFiltersChanged += OnSearchFiltersChanged;
    }

    void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        m_ProjectOrganizationProvider.onProjectSelectionChanged -= OnProjectSelectionChanged;
        m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;
        m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged -= OnProjectInfoOrLoadingChanged;
        m_PageManager.onActivePageChanged -= OnActivePageChanged;
        m_PageManager.onSearchFiltersChanged -= OnSearchFiltersChanged;
    }

    void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
    {
        Refresh();
    }

    void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
    {
        Refresh();
    }

    void OnProjectSelectionChanged(ProjectInfo projectInfo)
    {
        Refresh();
    }

    void OnActivePageChanged(IPage page)
    {
        Refresh();
    }

    void OnSearchFiltersChanged(IPage page, IReadOnlyCollection<string> data)
    {
        // Clear the filter selection caches
        foreach (var filterType in m_SelectedFilters)
        {
            filterType.IsDirty = true;
        }
    }

    void Refresh()
    {
        Clear();
        ClearAllFilters();

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
    }

    void ClearAllFilters()
    {
        bool hasFilters = m_AssetsProvider.AssetFilter.AllCriteria.Any();
        foreach (var filter in m_SelectedFilters)
        {
            filter.ApplyFilter(null);
            filter.Clear();
            filter.IsDirty = true;
        }

        m_SelectedFilters.Clear();
        m_PageManager.activePage?.Clear(hasFilters);
    }

    void CreatePopupContainer()
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
    }

    void OnFilterButtonClicked()
    {
        foreach (var filter in m_SelectedFilters)
        {
            filter.Cancel();
        }

        if (m_PopupContainer == null)
        {
            CreatePopupContainer();
        }
        else
        {
            m_PopupContainer.Clear();
        }

        SetPopupPosition(m_FilterButton);

        foreach (var filterType in m_FilterTypes)
        {
            if (!m_SelectedFilters.Contains(filterType))
            {
                var filterSelection = new TextElement();
                filterSelection.style.paddingLeft = 24; // Add padding to match the checkmark icon
                filterSelection.AddToClassList(k_ItemFilterSelectionClassName);
                filterSelection.text = filterType.DisplayName;
                filterSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    AddFilter(filterType);
                });

                m_PopupContainer?.Add(filterSelection);
            }
        }

        UIElementsUtils.Show(m_PopupContainer);
        m_PopupContainer?.Focus();

        m_AnalyticsEngine.SendFilterDropdownEvent();
    }

    void SetPopupPosition(VisualElement item)
    {
        var worldPos = item.LocalToWorld(Vector2.zero);
        var localPos = m_PopupContainer.parent.WorldToLocal(worldPos);

        m_PopupContainer.style.left = localPos.x;
        m_PopupContainer.style.top = localPos.y + item.resolvedStyle.height;
    }

    void AddFilter(BaseFilter filterType)
    {
        UIElementsUtils.Hide(m_PopupContainer);

        m_SelectedFilters.Add(filterType);
        m_FilterButton.SetEnabled(m_SelectedFilters.Count < m_FilterTypes.Count);

        var pill = new Button();
        pill.AddToClassList(k_ItemPillClassName);
        pill.clicked += () => OnPillClicked(pill, filterType);
        m_PillContainer.Add(pill);

        var label = new TextElement();
        label.name = "label";
        label.text = filterType.DisplayName;
        pill.Add(label);

        var delete = new Image();
        delete.image = UIElementsUtils.GetCategoryIcon("Close.png");
        delete.AddToClassList(k_ItemPillDeleteClassName);
        delete.AddManipulator(new Clickable(() => OnPillDeleteClicked(pill, filterType)));
        pill.Add(delete);

        filterType.IsDirty = true;

        WaitUntilPillIsPositioned(pill, filterType);
    }

    void WaitUntilPillIsPositioned(Button pill, BaseFilter filterType)
    {
        if (pill.resolvedStyle.top == 0)
        {
            EditorApplication.delayCall += () => WaitUntilPillIsPositioned(pill, filterType);
            return;
        }

        OnPillClicked(pill, filterType);
    }

    void OnPillClicked(Button pill, BaseFilter filterType)
    {
        UIElementsUtils.Show(m_PopupContainer);
        m_PopupContainer.Focus();
        SetPopupPosition(pill);
        m_PopupContainer.Clear();
        var loadingLabel = new TextElement();
        loadingLabel.text = L10n.Tr("Loading...");
        loadingLabel.AddToClassList(k_SelfCenterClassName);
        m_PopupContainer.Add(loadingLabel);

        _ = AddTextFilterSelectionItems(pill, filterType);
    }

    void OnPillDeleteClicked(Button pill, BaseFilter filterType)
    {
        pill.RemoveFromHierarchy();
        m_SelectedFilters.Remove(filterType);
        m_FilterButton.SetEnabled(m_SelectedFilters.Count < m_FilterTypes.Count);

        ApplyFilter(filterType, null);

        UIElementsUtils.Hide(m_PopupContainer);
    }

    async Task AddTextFilterSelectionItems(Button pill, BaseFilter filterType)
    {
        List<string> selections = await filterType.GetSelections();
        if (selections == null)
            return;

        m_PopupContainer.Clear();
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

            checkmark.visible = filterType.SelectedFilter == selection;

            filterSelection.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                pill.Q<TextElement>("label").text = $"{filterType.DisplayName}  |  {selection}";
                pill.AddToClassList(k_ItemPillSetClassName);
                UIElementsUtils.Hide(m_PopupContainer);

                ApplyFilter(filterType, selection);
            });

            m_PopupContainer.Add(filterSelection);
        }
    }

    void ApplyFilter(BaseFilter filterType, string selection)
    {
        var reload = filterType.ApplyFilter(selection);
        if (reload)
        {
            foreach (var filter in m_SelectedFilters)
            {
                filter.IsDirty = filter != filterType;
            }
        }

        if (selection != null)
        {
            m_AnalyticsEngine.SendFilterSearchEvent(filterType.DisplayName, selection);
            m_LastSelectedFilter = filterType.DisplayName;
            m_LastSelectedFilterSelection = selection;
            m_PageManager.activePage.onLoadingStatusChanged += OnLoadingStatusChanged;
        }

        m_PageManager.activePage.Clear(reload);
    }

    void OnLoadingStatusChanged(bool isLoading)
    {
        if (!isLoading)
        {
            m_PageManager.activePage.onLoadingStatusChanged -= OnLoadingStatusChanged;
            m_AnalyticsEngine.SendFilterSearchResultEvent(m_LastSelectedFilter, m_LastSelectedFilterSelection, m_PageManager.activePage.assetList.Count);
        }
    }
}
