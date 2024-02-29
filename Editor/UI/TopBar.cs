using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class TopBar : VisualElement
    {
        private const string k_SearchTerms = "Search";
        private const string k_TopBarAssetName = "TopBar";
        private const string k_SearchCancelUssName = "search-clear";
        private const string k_SearchFieldElementName = "inputSearch";

        private const string m_PlaceholderClass = k_TopBarAssetName + "__placeholder";

        private ToolbarSearchField m_ToolbarSearchField;
        private TextField m_SearchTextField;
        private VisualElement m_SearchPillsContainer;
        private Button m_ClearAllButton;
        private Button m_RefreshButton;

        private bool m_Focused;

        private readonly IPageManager m_PageManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        private readonly VisualElement m_TextInput;

        public TopBar(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            var windowContent = UIElementsUtils.LoadUXML(k_TopBarAssetName);
            windowContent.CloneTree(this);

            m_ToolbarSearchField = UIElementsUtils.SetupToolbarSearchField(k_SearchFieldElementName, OnSearchbarValueChanged, this);
            m_SearchTextField = m_ToolbarSearchField.Q<TextField>();
            m_SearchTextField.isDelayed = true;

            m_SearchTextField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            m_SearchTextField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            m_SearchTextField.RegisterCallback<FocusInEvent>(OnFocusIn);

            m_ClearAllButton = m_ToolbarSearchField.Q<Button>("unity-cancel");
            if (m_ClearAllButton != null)
            {
                m_ClearAllButton.name = k_SearchCancelUssName;
                m_ClearAllButton.clicked += OnSearchCancelClick;
            }

            m_SearchPillsContainer = new VisualElement();
            m_SearchPillsContainer.AddToClassList("search-pill-container");
            m_ToolbarSearchField.Insert(1, m_SearchPillsContainer);

            m_TextInput = m_ToolbarSearchField.Q<TextField>().Q("unity-text-input");

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_SearchTextField.AddToClassList(m_PlaceholderClass);
            ShowSearchTermsTextIfNeeded();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_PageManager.onSearchFiltersChanged += OnPageSearchFiltersChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;
            Refresh(m_PageManager.activePage);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_PageManager.onSearchFiltersChanged -= OnPageSearchFiltersChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;
        }

        void OnPageSearchFiltersChanged(IPage page, IEnumerable<string> searchFilters)
        {
            if (page.isActivePage)
                Refresh(page);
        }

        private void OnActivePageChanged(IPage page)
        {
            Refresh(page);
        }

        void OnFocusIn(FocusInEvent evt)
        {
            m_Focused = true;
            if (m_SearchTextField.text == k_SearchTerms)
            {
                m_SearchTextField.SetValueWithoutNotify("");
                m_SearchTextField.RemoveFromClassList(m_PlaceholderClass);
            }
        }

        void OnFocusOut(FocusOutEvent evt)
        {
            m_Focused = false;
            if (m_SearchPillsContainer.childCount == 0 && string.IsNullOrWhiteSpace(m_SearchTextField.text))
            {
                m_SearchTextField.AddToClassList(m_PlaceholderClass);
                ShowSearchTermsTextIfNeeded();
            }
        }

        private void Refresh(IPage page)
        {
            if (page == null)
                return;

            if (!string.IsNullOrWhiteSpace(m_ProjectOrganizationProvider.errorOrMessageHandlingData.message))
            {
                UIElementsUtils.Hide(m_RefreshButton);
                UIElementsUtils.Hide(m_ToolbarSearchField);
                return;
            }

            UIElementsUtils.Show(m_RefreshButton);
            UIElementsUtils.Show(m_ToolbarSearchField);
            m_SearchPillsContainer.Clear();
            m_ToolbarSearchField.SetValueWithoutNotify(string.Empty);
            foreach (var filter in page.pageFilters.searchFilters)
                m_SearchPillsContainer.Add(new SearchFilterPill(filter, DismissSearchFilter));

            ShowSearchTermsTextIfNeeded();
            m_ClearAllButton.visible = page.pageFilters.searchFilters.Any();
        }

        private void OnSearchCancelClick()
        {
            m_PageManager.activePage.pageFilters.ClearSearchFilters();
            ShowSearchTermsTextIfNeeded();
        }

        private void OnSearchbarValueChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue == k_SearchTerms)
                return;
            var searchFilters = evt.newValue.Split(" ").Where(s => !string.IsNullOrEmpty(s));
            if (searchFilters.Any())
            {
                AnalyticsSender.SendEvent(new SearchAttemptEvent(searchFilters.Count()));
                m_PageManager.activePage.pageFilters.AddSearchFilter(searchFilters);
            }

            if (m_Focused)
                SetKeyboardFocusOnSearchField();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    m_ToolbarSearchField.value = "";
                    m_PageManager.activePage.pageFilters.ClearSearchFilters();
                    SetKeyboardFocusOnSearchField();
                    return;
                case KeyCode.Backspace when string.IsNullOrWhiteSpace(m_SearchTextField.text):
                    PopSearchPill();
                    SetKeyboardFocusOnSearchField();
                    break;
            }
        }

        private void SetKeyboardFocusOnSearchField()
        {
            m_TextInput.Focus();
        }

        private void PopSearchPill()
        {
            var pageFilters = m_PageManager.activePage.pageFilters;

            if (!m_PageManager.activePage.pageFilters.searchFilters.Any())
                return;

            pageFilters.RemoveSearchFilter(pageFilters.searchFilters.Last());
            if (!pageFilters.searchFilters.Any())
                ShowSearchTermsTextIfNeeded();
        }

        private void DismissSearchFilter(string searchFilter)
        {
            var pageFilters = m_PageManager.activePage.pageFilters;

            if (!pageFilters.searchFilters.Contains(searchFilter))
                return;

            pageFilters.RemoveSearchFilter(searchFilter);
            SetKeyboardFocusOnSearchField();
        }

        private void ShowSearchTermsTextIfNeeded()
        {
            if (m_SearchPillsContainer.childCount == 0 && !m_Focused)
                m_SearchTextField.SetValueWithoutNotify(k_SearchTerms);
        }

        private void OrganizationChanged(OrganizationInfo organizationInfo)
        {
            Refresh(m_PageManager.activePage);
        }
    }
}
