using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SideBarCollectionFoldout : SideBarFoldout
    {
        static readonly string k_IconFolderOpen = "icon-folder-open";
        static readonly string k_IconFolderClose = "icon-folder-close";

        readonly ProjectInfo m_ProjectInfo;
        readonly string m_CollectionPath;
        readonly string m_CollectionId;

        readonly Image m_Icon;

        public string CollectionPath => m_CollectionPath;
        public string CollectionId => m_CollectionId;

        internal SideBarCollectionFoldout(IUnityConnectProxy unityConnectProxy, IPageManager pageManager, IStateManager stateManager,
            IProjectOrganizationProvider projectOrganizationProvider,
            string foldoutName, ProjectInfo projectInfo, string collectionPath)
            : base(unityConnectProxy, pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            m_ProjectInfo = projectInfo;
            m_CollectionPath = collectionPath;
            m_CollectionId = $"{m_ProjectInfo.Id}/{m_CollectionPath}";

            var iconParent = this.Q(className: inputUssClassName);
            m_Icon = iconParent.Q<Image>();

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

            if (value)
            {
                m_Icon.RemoveFromClassList(k_IconFolderClose);
                m_Icon.AddToClassList(k_IconFolderOpen);
            }
            else
            {
                m_Icon.RemoveFromClassList(k_IconFolderOpen);
                m_Icon.AddToClassList(k_IconFolderClose);
            }
        }
    }
}
