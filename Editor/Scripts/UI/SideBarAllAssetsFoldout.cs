using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarAllAssetsFoldout : SideBarFoldout
    {
        internal SideBarAllAssetsFoldout(IPageManager pageManager, IStateManager stateManager, IProjectOrganizationProvider projectOrganizationProvider, string foldoutName)
            : base(pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            var image = this.Q<Image>();
            image.style.width = 16;
            image.style.height = 16;
            image.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.AllAssetsFolderName]);

            RegisterCallback<PointerDownEvent>(e =>
            {
                var target = (VisualElement)e.target;

                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.button != 0 || target.name == k_CheckMarkName)
                    return;

                pageManager.activePage = pageManager.GetPage(PageType.AllAssets);
                if (projectOrganizationProvider.selectedProject != ProjectInfo.AllAssetsProjectInfo)
                {
                    projectOrganizationProvider.selectedProject = ProjectInfo.AllAssetsProjectInfo;
                }
            }, TrickleDown.TrickleDown);
        }
        protected override void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
        {
            Refresh();
        }

        protected override void OnRefresh(IPage page)
        {
            Refresh();
        }

        internal void Refresh()
        {
            var selected = m_ProjectOrganizationProvider.selectedProject == ProjectInfo.AllAssetsProjectInfo;
            m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);
        }
    }
}
