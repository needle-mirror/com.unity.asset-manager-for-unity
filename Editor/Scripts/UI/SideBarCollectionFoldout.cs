using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarCollectionFoldout : SideBarFoldout
    {
        public string CollectionPath { get; }

        internal SideBarCollectionFoldout(IPageManager pageManager, IStateManager stateManager, IProjectOrganizationProvider projectOrganizationProvider, string foldoutName, string collectionPath)
            : base(pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            CollectionPath = collectionPath;

            RegisterEventForIconChange();

            RegisterCallback<PointerDownEvent>(e =>
            {
                var target = (VisualElement)e.target;

                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.button != 0 || target.name == k_CheckMarkName)
                    return;
                pageManager.activePage = pageManager.GetPage(PageType.Collection, collectionPath);
            }, TrickleDown.TrickleDown);
        }

        protected override void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
        {
            if (m_PageManager.activePage?.pageType == PageType.Collection)
            {
                var activePage = (CollectionPage)m_PageManager.activePage;
                // When browsing, make sure to return to All Assets selection if there is no collections or the one we had
                // selected does not exist anymore
                if (!isLoading && m_ProjectOrganizationProvider.selectedProject?.collectionInfos?.Any(i => string.Equals(i.GetFullPath(), activePage.collectionPath)) != true)
                {
                    m_PageManager.activePage = m_PageManager.GetPage(PageType.Collection, string.Empty);
                }
            }

            OnRefresh(m_PageManager.activePage);
        }

        protected override void OnRefresh(IPage page)
        {
            if (page != null && page.pageType == PageType.Collection)
            {
                var collectionPage = (CollectionPage)page;
                var selected = (CollectionPath ?? string.Empty) == (collectionPage.collectionPath ?? string.Empty);
                m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);
            }
            else
            {
                m_Toggle.EnableInClassList(k_UnityListViewItemSelected, false);
            }
        }

        internal override void ChangeIntoParentFolder()
        {
            base.ChangeIntoParentFolder();

            if (!string.IsNullOrEmpty(CollectionPath))
            {
                value = !m_StateManager.collapsedCollections.Contains(CollectionPath);
                SetIcon();
            }
        }

        void RegisterEventForIconChange()
        {
            this.RegisterValueChangedCallback(e =>
            {
                SetIcon();
                if (m_HasChild)
                {
                    if (!value)
                    {
                        m_StateManager.collapsedCollections.Add(CollectionPath);
                    }
                    else
                    {
                        m_StateManager.collapsedCollections.Remove(CollectionPath);
                    }
                }
            });
        }

        void SetIcon()
        {
            if (!m_HasChild)
                return;

            var iconParent = this.Q(className: inputUssClassName);
            var image = iconParent.Q<Image>();

            image.image = value
                ? UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.OpenFoldoutName])
                : UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ClosedFoldoutName]);
        }
    }
}
