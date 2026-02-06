using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarSavedViewFoldout : Foldout
    {
        readonly SidebarSavedViewsFoldoutViewModel m_ViewModel;

        Dictionary<string, SidebarSavedViewItem> m_SidebarSavedViewItems = new ();

        Button m_SaveCurrentFilterButton;
        Toggle m_SavedViewsToggle;

        public SidebarSavedViewFoldout(SidebarSavedViewsFoldoutViewModel viewModel)
        {
            m_ViewModel = viewModel;

            m_SavedViewsToggle = this.Q<Toggle>();
            m_SavedViewsToggle.text = Constants.SidebarSavedViewsText;
            m_SavedViewsToggle.AddToClassList("SidebarContentTitle");
            m_SavedViewsToggle.focusable = false;

            m_SaveCurrentFilterButton = new Button(m_ViewModel.OnSaveCurrentFilterClicked)
            {
                text = L10n.Tr("Save Current Filter"),
                name = "SaveCurrentFilterButton"
            };
            Add(m_SaveCurrentFilterButton);
            UIElementsUtils.Hide(m_SaveCurrentFilterButton);

            RebuildSavedViewItemsList();

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent _)
        {
            m_ViewModel.BindEvents();
            m_ViewModel.FilterSelected += OnFilterSelected;
            m_ViewModel.FilterAdded += OnFilterAdded;
            m_ViewModel.FilterDeleted += OnFilterDeleted;
        }

        void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            m_ViewModel.UnbindEvents();
            m_ViewModel.FilterSelected -= OnFilterSelected;
            m_ViewModel.FilterAdded -= OnFilterAdded;
            m_ViewModel.FilterDeleted -= OnFilterDeleted;
        }

        public void Refresh()
        {
            RebuildSavedViewItemsList();
        }

        public void SetDisplay(bool isDisplayed)
        {
            UIElementsUtils.SetDisplay(this, isDisplayed);
        }

        public void Unselect()
        {
            m_ViewModel.ClearSelectedFilter();
            foreach (var savedViewItem in m_SidebarSavedViewItems.Values)
            {
                savedViewItem.SetSelected(false);
            }
        }

        void OnFilterSelected(SavedAssetSearchFilter filter, bool _)
        {
            foreach (var savedViewItem in m_SidebarSavedViewItems.Values)
                savedViewItem.SetSelected(savedViewItem.FilterId == filter?.FilterId);

            m_SavedViewsToggle.value = true;
        }

        void OnFilterAdded(SavedAssetSearchFilter filter)
        {
            RebuildSavedViewItemsList();

            // Enter rename mode for the newly added filter
            if (m_SidebarSavedViewItems.TryGetValue(filter.FilterId, out var sidebarSavedViewItem))
                sidebarSavedViewItem.StartRenaming();

            m_SavedViewsToggle.value = true;
        }

        void OnFilterDeleted(SavedAssetSearchFilter _)
        {
            RebuildSavedViewItemsList();
        }

        void RebuildSavedViewItemsList()
        {
            foreach (var item in m_SidebarSavedViewItems.Values)
            {
                item.ItemClicked -= OnItemClicked;
                item.RenameFilter -= m_ViewModel.RenameFilter;
                item.DeleteFilter -= m_ViewModel.DeleteFilter;
                Remove(item);
            }

            m_SidebarSavedViewItems.Clear();

            var searchFilters = m_ViewModel.GetSavedFilters();

            if (!searchFilters.Any())
                UIElementsUtils.Show(m_SaveCurrentFilterButton);
            else
                UIElementsUtils.Hide(m_SaveCurrentFilterButton);

            foreach (var savedAssetSearchFilter in searchFilters)
            {
                var sidebarSavedViewItem = new SidebarSavedViewItem(savedAssetSearchFilter, m_ViewModel.IsFilterRenameValid);
                sidebarSavedViewItem.ItemClicked += OnItemClicked;
                sidebarSavedViewItem.RenameFilter += m_ViewModel.RenameFilter;
                sidebarSavedViewItem.DeleteFilter += m_ViewModel.DeleteFilter;

                Utilities.DevLog("Filter added to sidebar: " + savedAssetSearchFilter.FilterId + " - " + savedAssetSearchFilter.FilterName);

                if (!m_SidebarSavedViewItems.TryAdd(savedAssetSearchFilter.FilterId, sidebarSavedViewItem))
                    Utilities.DevLogError("Duplicate filter ID found in sidebar: " + savedAssetSearchFilter.FilterId);

                sidebarSavedViewItem.SetSelected(sidebarSavedViewItem.FilterId == m_ViewModel.GetSelectedFilter()?.FilterId);
                Add(sidebarSavedViewItem);
            }
        }

        void OnItemClicked(SidebarSavedViewItem item)
        {
            var filterId = item.FilterId;
            if (m_ViewModel.GetSelectedFilter()?.FilterId == filterId)
            {
                m_ViewModel.ClearSelectedFilter();
                item.SetSelected(false);
            }
            else
            {
                m_ViewModel.SelectFilter(item.FilterId);
                item.SetSelected(true);
            }
        }
    }
}
