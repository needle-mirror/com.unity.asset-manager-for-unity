using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarProjectFoldout : SideBarFoldout
    {
        ProjectInfo m_ProjectInfo;

        internal ProjectInfo ProjectInfo => m_ProjectInfo;

        internal SideBarProjectFoldout(IPageManager pageManager, IStateManager stateManager, IProjectOrganizationProvider projectOrganizationProvider, string foldoutName, ProjectInfo project)
            : base(pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            m_ProjectInfo = project;

            RegisterCallback<PointerDownEvent>(e =>
            {
                var target = (VisualElement)e.target;

                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.button != 0 || target.name == k_CheckMarkName)
                    return;

                if (project.id == projectOrganizationProvider.selectedProject?.id)
                {
                    pageManager.activePage = pageManager.GetPage(PageType.Collection);
                }
                else
                {
                    projectOrganizationProvider.selectedProject = projectOrganizationProvider.organization?.projectInfos.FirstOrDefault(proj => proj.id == project.id);
                }
            }, TrickleDown.TrickleDown);

            SetIcon();
        }

        protected override void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
        {
            OnRefresh(m_PageManager.activePage);
        }

        protected override void OnRefresh(IPage page)
        {
            if (page != null && page.pageType == PageType.Collection)
            {
                var collectionPage = (CollectionPage)page;
                var selected = m_ProjectOrganizationProvider.selectedProject?.id == m_ProjectInfo.id && string.IsNullOrEmpty(collectionPage.collectionPath);
                m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);
            }
            else
            {
                m_Toggle.EnableInClassList(k_UnityListViewItemSelected, false);
            }
        }

        void SetIcon() { }
    }
}
