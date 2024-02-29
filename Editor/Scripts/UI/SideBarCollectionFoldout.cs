using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SideBarCollectionFoldout : SideBarFoldout
    {
        public string CollectionPath => m_CollectionPath;

        readonly ProjectInfo m_ProjectInfo;
        readonly string m_CollectionPath;

        internal SideBarCollectionFoldout(IPageManager pageManager, IStateManager stateManager, IProjectOrganizationProvider projectOrganizationProvider,
             string foldoutName, ProjectInfo projectInfo, string collectionPath)
            : base(pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            m_ProjectInfo = projectInfo;
            m_CollectionPath = collectionPath;

            RegisterEventForIconChange();

            RegisterCallback<PointerDownEvent>(e =>
            {
                var target = (VisualElement)e.target;

                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.button != 0 || target.name == k_CheckMarkName)
                    return;

                if (m_ProjectInfo.id == m_ProjectOrganizationProvider.SelectedProject?.id)
                {
                    pageManager.SetActivePage<CollectionPage>();
                }

                m_ProjectOrganizationProvider.SelectProject(m_ProjectInfo, m_CollectionPath);
                if (string.IsNullOrEmpty(m_CollectionPath))
                {
                    AnalyticsSender.SendEvent(new ProjectSelectedEvent(ProjectSelectedEvent.ProjectType.Project, m_ProjectInfo.collectionInfos.Count));
                }
                else
                {
                    AnalyticsSender.SendEvent(new ProjectSelectedEvent(ProjectSelectedEvent.ProjectType.Collection));
                }
            }, TrickleDown.TrickleDown);
        }

        protected override void OnRefresh(IPage page)
        {
            if (page is CollectionPage)
            {
                var selected = m_ProjectOrganizationProvider.SelectedProject?.id == m_ProjectInfo.id
                               && m_ProjectOrganizationProvider.SelectedCollection?.GetFullPath() == (m_CollectionPath ?? string.Empty);

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

            if (string.IsNullOrEmpty(m_CollectionPath))
                return;

            value = !m_StateManager.collapsedCollections.Contains(m_CollectionPath);
            SetIcon();
        }

        void RegisterEventForIconChange()
        {
            this.RegisterValueChangedCallback(e =>
            {
                SetIcon();

                if (!m_HasChild)
                    return;

                if (!value)
                {
                    m_StateManager.collapsedCollections.Add(m_CollectionPath);
                }
                else
                {
                    m_StateManager.collapsedCollections.Remove(m_CollectionPath);
                }
            });
        }

        void SetIcon()
        {
            if (string.IsNullOrEmpty(m_CollectionPath) || !m_HasChild)
                return;

            var iconParent = this.Q(className: inputUssClassName);
            var image = iconParent.Q<Image>();

            image.image = value
                ? UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.OpenFoldoutName])
                : UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ClosedFoldoutName]);
        }
    }
}
