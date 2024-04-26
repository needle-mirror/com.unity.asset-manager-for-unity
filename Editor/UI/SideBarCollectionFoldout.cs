using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SideBarCollectionFoldout : SideBarFoldout
    {
        readonly ProjectInfo m_ProjectInfo;
        readonly string m_CollectionPath;
        readonly string m_CollectionId;

        public string CollectionPath => m_CollectionPath;
        public string CollectionId => m_CollectionId;

        internal SideBarCollectionFoldout(IPageManager pageManager, IStateManager stateManager,
            IProjectOrganizationProvider projectOrganizationProvider,
            string foldoutName, ProjectInfo projectInfo, string collectionPath)
            : base(pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            m_ProjectInfo = projectInfo;
            m_CollectionPath = collectionPath;
            m_CollectionId = $"{m_ProjectInfo.Id}/{m_CollectionPath}";

            RegisterEventForIconChange();

            RegisterCallback<PointerDownEvent>(e =>
            {
                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.target != this)
                    return;

                m_ProjectOrganizationProvider.SelectProject(m_ProjectInfo, m_CollectionPath);
                m_PageManager.SetActivePage<CollectionPage>();
            }, TrickleDown.TrickleDown);
        }

        protected override void OnRefresh(IPage page)
        {
            if (page is CollectionPage)
            {
                var selected = m_ProjectOrganizationProvider.SelectedProject?.Id == m_ProjectInfo.Id
                    && m_ProjectOrganizationProvider.SelectedCollection?.GetFullPath() ==
                    (m_CollectionPath ?? string.Empty);

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

            value = !m_StateManager.CollapsedCollections.Contains(m_CollectionId);
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
                    m_StateManager.CollapsedCollections.Add(m_CollectionId);
                }
                else
                {
                    m_StateManager.CollapsedCollections.Remove(m_CollectionId);
                }
            });
        }

        void SetIcon()
        {
            if (string.IsNullOrEmpty(m_CollectionPath) || !m_HasChild)
                return;

            var iconParent = this.Q(className: inputUssClassName);
            var image = iconParent.Q<Image>();

            image.image = value ?
                UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.OpenFoldoutName]) :
                UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ClosedFoldoutName]);
        }
    }
}
