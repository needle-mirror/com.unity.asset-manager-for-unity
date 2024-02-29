using System;
using System.Linq;
using Unity.AssetManager.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class Breadcrumbs : VisualElement
    {
        const string k_UssClassName = "unity-breadcrumbs";
        const string k_ItemArrowClassName = k_UssClassName + "-arrow";
        const string k_ItemButtonClassName = k_UssClassName + "-button";
        internal const string k_ItemHighlightButtonClassName = "highlight";

        private readonly IPageManager m_PageManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        /// <summary>
        /// Constructs a breadcrumb UI element for the toolbar to help users navigate a hierarchy.
        /// </summary>
        public Breadcrumbs(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            AddToClassList(k_UssClassName);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            Refresh();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += ProjectSelectionChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= ProjectSelectionChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;
        }

        private void OrganizationChanged(OrganizationInfo _)
        {
            Refresh();
        }

        private void ProjectSelectionChanged(ProjectInfo _, CollectionInfo __)
        {
            Refresh();
        }

        private void Refresh()
        {
            var page = m_PageManager.activePage;
            if (!ShowOrHideBreadCrumbs(page, m_ProjectOrganizationProvider.SelectedOrganization))
                return;

            Clear();

            // Project breadcrumb
            AddBreadcrumbItem(m_ProjectOrganizationProvider.SelectedProject?.name, () =>
                {
                    m_PageManager.activePage.selectedAssetId = null;
                    m_ProjectOrganizationProvider.SelectProject(m_ProjectOrganizationProvider.SelectedProject);
                });

            // Collection/subcollection breadcrumb
            if (page is CollectionPage collectionPage && !string.IsNullOrEmpty(collectionPage.collectionPath))
            {
                var collectionPaths = collectionPage.collectionPath.Split("/");
                foreach (var path in collectionPaths)
                {
                    var collectionPath = collectionPage.collectionPath[..(collectionPage.collectionPath.IndexOf(path, StringComparison.Ordinal) + path.Length)];
                    AddBreadcrumbItem(path, () =>
                    {
                        m_PageManager.activePage.selectedAssetId = null;
                        m_PageManager.SetActivePage<CollectionPage>();
                        m_ProjectOrganizationProvider.SelectProject(m_ProjectOrganizationProvider.SelectedProject, collectionPath);
                    });
                }
            }

            // Last item should always be bold
            Children().Last().AddToClassList(k_ItemHighlightButtonClassName);
        }

        private void AddBreadcrumbItem(string label, Action clickEvent = null)
        {
            if (Children().Any())
            {
                var arrow = new VisualElement();
                arrow.AddToClassList(k_ItemArrowClassName);
                Add(arrow);
            }

            Add(new BreadcrumbItem(clickEvent) { text = label });
        }

        private void OnActivePageChanged(IPage page)
        {
            Refresh();
        }

        // Returns true if the breadcrumbs is visible
        private bool ShowOrHideBreadCrumbs(IPage page, OrganizationInfo currentOrganization)
        {
            if (page == null || !((BasePage)page).DisplayBreadcrumbs || currentOrganization?.projectInfos.Any() != true)
            {
                UIElementsUtils.Hide(this);
                return false;
            }

            UIElementsUtils.Show(this);
            return true;
        }

        private class BreadcrumbItem : Button
        {
            public BreadcrumbItem(Action clickEvent) : base(clickEvent)
            {
                RemoveFromClassList(ussClassName);
                AddToClassList(k_ItemButtonClassName);
            }
        }
    }
}
