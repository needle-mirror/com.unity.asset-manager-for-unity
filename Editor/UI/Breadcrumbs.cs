using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class Breadcrumbs : VisualElement
    {
        const string k_UssClassName = "unity-breadcrumbs";
        const string k_ItemArrowClassName = k_UssClassName + "-arrow";
        const string k_ItemButtonClassName = k_UssClassName + "-button";
        internal const string k_ItemHighlightButtonClassName = "highlight";

        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

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

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += ProjectSelectionChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= ProjectSelectionChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;
        }

        void OrganizationChanged(OrganizationInfo _)
        {
            Refresh();
        }

        void ProjectSelectionChanged(ProjectInfo _, CollectionInfo __)
        {
            Refresh();
        }

        void Refresh()
        {
            var page = m_PageManager.ActivePage;
            if (!ShowOrHideBreadCrumbs(page, m_ProjectOrganizationProvider.SelectedOrganization))
                return;

            Clear();

            // Project breadcrumb
            AddBreadcrumbItem(m_ProjectOrganizationProvider.SelectedProject?.Name,
                () => { m_ProjectOrganizationProvider.SelectProject(m_ProjectOrganizationProvider.SelectedProject); });

            // Collection/subcollection breadcrumb
            if (page is CollectionPage collectionPage && !string.IsNullOrEmpty(collectionPage.CollectionPath))
            {
                var collectionPaths = collectionPage.CollectionPath.Split("/");
                foreach (var path in collectionPaths)
                {
                    var collectionPath = collectionPage.CollectionPath[
                        ..(collectionPage.CollectionPath.IndexOf(path, StringComparison.Ordinal) + path.Length)];
                    AddBreadcrumbItem(path,
                        () =>
                        {
                            m_ProjectOrganizationProvider.SelectProject(m_ProjectOrganizationProvider.SelectedProject,
                                collectionPath);
                        });
                }
            }

            // Last item should always be bold
            Children().Last().AddToClassList(k_ItemHighlightButtonClassName);
        }

        void AddBreadcrumbItem(string label, Action clickEvent = null)
        {
            if (Children().Any())
            {
                var arrow = new VisualElement();
                arrow.AddToClassList(k_ItemArrowClassName);
                Add(arrow);
            }

            Add(new BreadcrumbItem(clickEvent) { text = label });
        }

        void OnActivePageChanged(IPage page)
        {
            Refresh();
        }

        // Returns true if the breadcrumbs is visible
        bool ShowOrHideBreadCrumbs(IPage page, OrganizationInfo currentOrganization)
        {
            if (page == null || !((BasePage)page).DisplayBreadcrumbs || currentOrganization?.ProjectInfos.Any() != true)
            {
                UIElementsUtils.Hide(this);
                return false;
            }

            UIElementsUtils.Show(this);
            return true;
        }

        class BreadcrumbItem : Button
        {
            public BreadcrumbItem(Action clickEvent) : base(clickEvent)
            {
                RemoveFromClassList(ussClassName);
                AddToClassList(k_ItemButtonClassName);
            }
        }
    }
}
