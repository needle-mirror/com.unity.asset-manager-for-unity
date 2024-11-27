using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SearchBar : VisualElement
    {
        const string k_SearchTerms = "Search";
        const string k_SearchBarAssetName = "SearchBar";
        const string k_SearchCancelUssName = "search-clear";
        const string k_SearchFieldElementName = "inputSearch";
        const string k_PlaceholderClass = k_SearchBarAssetName + "__placeholder";
        const int k_MaxLength = 1024;

        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly VisualElement m_TextInput;

        bool m_Focused;

        Button m_ClearAllButton;
        Button m_RefreshButton;
        VisualElement m_SearchChipsContainer;
        TextField m_SearchTextField;
        ToolbarSearchField m_ToolbarSearchField;

        public SearchBar(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            var windowContent = UIElementsUtils.LoadUXML(k_SearchBarAssetName);
            windowContent.CloneTree(this);

            m_ToolbarSearchField = UIElementsUtils.SetupToolbarSearchField(k_SearchFieldElementName, OnSearchbarValueChanged, this);
            m_SearchTextField = m_ToolbarSearchField.Q<TextField>();
            m_SearchTextField.maxLength = k_MaxLength;
            m_SearchTextField.isDelayed = true;

            m_ClearAllButton = m_ToolbarSearchField.Q<Button>("unity-cancel");
            if (m_ClearAllButton != null)
            {
                m_ClearAllButton.name = k_SearchCancelUssName;
            }

            m_SearchChipsContainer = new VisualElement();
            m_SearchChipsContainer.AddToClassList("search-chip-container");
            m_ToolbarSearchField.Insert(1, m_SearchChipsContainer);

            m_TextInput = m_ToolbarSearchField.Q<TextField>().Q("unity-text-input");

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_SearchTextField.AddToClassList(k_PlaceholderClass);
            ShowSearchTermsTextIfNeeded();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_SearchTextField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            m_SearchTextField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            m_SearchTextField.RegisterCallback<FocusInEvent>(OnFocusIn);

            if (m_ClearAllButton != null)
            {
                m_ClearAllButton.clicked += OnSearchCancelClick;
            }

            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_PageManager.SearchFiltersChanged += OnPageSearchFiltersChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            Refresh(m_PageManager.ActivePage);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_SearchTextField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            m_SearchTextField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            m_SearchTextField.UnregisterCallback<FocusInEvent>(OnFocusIn);

            if (m_ClearAllButton != null)
            {
                m_ClearAllButton.clicked -= OnSearchCancelClick;
            }

            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_PageManager.SearchFiltersChanged -= OnPageSearchFiltersChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
        }

        void OnPageSearchFiltersChanged(IPage page, IEnumerable<string> searchFilters)
        {
            if (m_PageManager.IsActivePage(page))
            {
                Refresh(page);
            }
        }

        void OnActivePageChanged(IPage page)
        {
            Refresh(page);
        }

        void OnFocusIn(FocusInEvent evt)
        {
            m_Focused = true;
            if (m_SearchTextField.text == k_SearchTerms)
            {
                m_SearchTextField.SetValueWithoutNotify("");
                m_SearchTextField.RemoveFromClassList(k_PlaceholderClass);
            }
        }

        void OnFocusOut(FocusOutEvent evt)
        {
            m_Focused = false;
            if (m_SearchChipsContainer.childCount == 0 && string.IsNullOrWhiteSpace(m_SearchTextField.text))
            {
                m_SearchTextField.AddToClassList(k_PlaceholderClass);
                ShowSearchTermsTextIfNeeded();
            }
        }

        void Refresh(IPage page)
        {
            if (page == null)
                return;

            if (!string.IsNullOrWhiteSpace(m_ProjectOrganizationProvider.MessageData.Message))
            {
                UIElementsUtils.Hide(m_RefreshButton);
                UIElementsUtils.Hide(m_ToolbarSearchField);
                return;
            }

            UIElementsUtils.Show(m_RefreshButton);
            UIElementsUtils.Show(m_ToolbarSearchField);
            m_SearchChipsContainer.Clear();
            m_ToolbarSearchField.SetValueWithoutNotify(string.Empty);
            foreach (var filter in page.PageFilters.SearchFilters)
            {
                m_SearchChipsContainer.Add(new SearchFilterChip(filter, DismissSearchFilter));
            }

            ShowSearchTermsTextIfNeeded();
            m_ClearAllButton.visible = page.PageFilters.SearchFilters.Any();
        }

        void OnSearchCancelClick()
        {
            m_PageManager.ActivePage.PageFilters.ClearSearchFilters();
            ShowSearchTermsTextIfNeeded();
        }

        void OnSearchbarValueChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue == k_SearchTerms)
                return;

            var searchFilters = evt.newValue.Split(" ").Where(s => !string.IsNullOrEmpty(s));
            if (searchFilters.Any())
            {
                AnalyticsSender.SendEvent(new SearchAttemptEvent(searchFilters.Count()));
                m_PageManager.ActivePage.PageFilters.AddSearchFilter(searchFilters);
            }

            if (m_Focused)
            {
                SetKeyboardFocusOnSearchField();
            }
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    m_ToolbarSearchField.value = "";
                    m_PageManager.ActivePage.PageFilters.ClearSearchFilters();
                    SetKeyboardFocusOnSearchField();
                    return;
                case KeyCode.Backspace when string.IsNullOrWhiteSpace(m_SearchTextField.text):
                    PopSearchChip();
                    SetKeyboardFocusOnSearchField();
                    break;
            }
        }

        void SetKeyboardFocusOnSearchField()
        {
            m_TextInput.Focus();
        }

        void PopSearchChip()
        {
            var pageFilters = m_PageManager.ActivePage.PageFilters;

            if (!m_PageManager.ActivePage.PageFilters.SearchFilters.Any())
                return;

            pageFilters.RemoveSearchFilter(pageFilters.SearchFilters.Last());
            if (!pageFilters.SearchFilters.Any())
            {
                ShowSearchTermsTextIfNeeded();
            }
        }

        void DismissSearchFilter(string searchFilter)
        {
            var pageFilters = m_PageManager.ActivePage.PageFilters;

            if (!pageFilters.SearchFilters.Contains(searchFilter))
                return;

            pageFilters.RemoveSearchFilter(searchFilter);
            SetKeyboardFocusOnSearchField();
        }

        void ShowSearchTermsTextIfNeeded()
        {
            if (m_SearchChipsContainer.childCount == 0 && !m_Focused)
            {
                m_SearchTextField.SetValueWithoutNotify(k_SearchTerms);
            }
        }

        void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            Refresh(m_PageManager.ActivePage);
        }
    }
}
