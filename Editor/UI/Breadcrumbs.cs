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
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        /// <summary>
        /// Constructs a breadcrumb UI element for the toolbar to help users navigate a hierarchy.
        /// </summary>
        public Breadcrumbs(IPageManager pageManager, IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            AddToClassList(k_UssClassName);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            Refresh();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_PageManager.onSelectedAssetChanged += OnSelectedAssetChanged;
            m_ProjectOrganizationProvider.onProjectSelectionChanged += OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_PageManager.onSelectedAssetChanged -= OnSelectedAssetChanged;
            m_ProjectOrganizationProvider.onProjectSelectionChanged -= OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;
        }

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
        {
            Refresh();
        }

        private void OnProjectSelectionChanged(ProjectInfo _)
        {
            Refresh();
        }

        private void Refresh()
        {
            var page = m_PageManager.activePage;
            if (!ShowOrHideBreadCrumbs(page, m_ProjectOrganizationProvider.organization))
                return;

            Clear();
            
            // Project breadcrumb, should never be bold
            AddBreadcrumbItem(m_ProjectOrganizationProvider.selectedProject?.name, () =>
                {
                    m_PageManager.activePage.selectedAssetId = null;
                    m_PageManager.activePage = m_PageManager.GetPage(PageType.Collection, string.Empty);
                });
            
            // Collection/subcollection breadcrumb
            if (!string.IsNullOrEmpty(page.collectionPath))
            {
                var collectionPaths = page.collectionPath.Split("/");
                foreach (var path in collectionPaths)
                {
                    var collectionPath = page.collectionPath[..(page.collectionPath.IndexOf(path) + path.Length)];
                    AddBreadcrumbItem(path, () =>
                        {
                            m_PageManager.activePage.selectedAssetId = null;
                            m_PageManager.activePage = m_PageManager.GetPage(PageType.Collection, collectionPath);
                        });
                }
            }
            else
            {
                AddBreadcrumbItem("All Assets", () => m_PageManager.activePage.selectedAssetId = null);
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

        private void OnSelectedAssetChanged(IPage page, AssetIdentifier assetId)
        {
            Refresh();
        }

        // Returns true if the breadcrumbs is visible
        private bool ShowOrHideBreadCrumbs(IPage page, OrganizationInfo currentOrganization)
        {
            if (page == null || page.pageType == PageType.InProject || currentOrganization?.projectInfos.Any() != true)
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
