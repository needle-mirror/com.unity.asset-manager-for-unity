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

        readonly IUploadManager m_UploadManager;

        readonly Image m_Icon;

        internal SideBarCollectionFoldout(IUnityConnectProxy unityConnectProxy, IPageManager pageManager, IStateManager stateManager,
            IProjectOrganizationProvider projectOrganizationProvider,
            string foldoutName, ProjectInfo projectInfo, string collectionPath)
            : base(unityConnectProxy, pageManager, stateManager, projectOrganizationProvider, foldoutName)
        {
            m_ProjectInfo = projectInfo;
            m_CollectionPath = collectionPath;
            name = GetCollectionId(m_ProjectInfo, m_CollectionPath);

            m_UploadManager = ServicesContainer.instance.Resolve<IUploadManager>();

            var iconParent = this.Q(className: inputUssClassName);
            m_Icon = iconParent.Q<Image>();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public static string GetCollectionId(ProjectInfo projectInfo, string collectionPath)
        {
            return string.IsNullOrEmpty(collectionPath) ? projectInfo.Id : $"{projectInfo.Id}::{collectionPath}";
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            RegisterEventForIconChange();
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            m_UploadManager.UploadBegan += OnUploadBegan;
            m_UploadManager.UploadEnded += OnUploadEnded;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnRegisterEventForIconChange();
            UnregisterCallback<PointerDownEvent>(OnPointerDown);

            m_UploadManager.UploadBegan -= OnUploadBegan;
            m_UploadManager.UploadEnded -= OnUploadEnded;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            // We skip the user's click if they aimed the check mark of the foldout
            // to only select foldouts when they click on it's title/label
            if (evt.target != this)
                return;

            m_ProjectOrganizationProvider.SelectProject(m_ProjectInfo, m_CollectionPath);
        }

        void OnUploadBegan()
        {
            SetEnabled(false);
        }

        void OnUploadEnded()
        {
            SetEnabled(true);
        }

        protected override void OnActivePageChanged(IPage page)
        {
            if(m_UploadManager.IsUploading)
            {
                SetEnabled(page is not UploadPage);
            }

            RefreshSelectionStatus();
        }

        protected override void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            if (m_PageManager.ActivePage is not (CollectionPage or UploadPage))
            {
                SetSelected(false);
                return;
            }

            var selected = projectInfo?.Id == m_ProjectInfo.Id &&
                           collectionInfo?.GetFullPath() == (m_CollectionPath ?? string.Empty);

            SetSelected(selected);
        }

        protected internal void RefreshSelectionStatus()
        {
            OnProjectSelectionChanged(m_ProjectOrganizationProvider.SelectedProject, m_ProjectOrganizationProvider.SelectedCollection);
        }

        void SetSelected(bool selected)
        {
            m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);

            if (selected)
            {
                UncollapseHierarchy();
            }
        }

        void UncollapseHierarchy()
        {
            var p = parent;
            while (p is SideBarCollectionFoldout foldout)
            {
                foldout.value = true;
                p = foldout.parent;
            }
        }

        public override void AddFoldout(SideBarCollectionFoldout child)
        {
            value = m_StateManager.UncollapsedCollections.Contains(name); // Do not force the foldout to close if something else (like auto selection) is forcing it to open

            base.AddFoldout(child);

            if (string.IsNullOrEmpty(m_CollectionPath))
                return;

            SetIcon();
        }

        void RegisterEventForIconChange()
        {
            this.RegisterValueChangedCallback(OnIconChanged);
        }

        void UnRegisterEventForIconChange()
        {
            this.UnregisterValueChangedCallback(OnIconChanged);
        }

        void OnIconChanged(ChangeEvent<bool> evt)
        {
            SetIcon();

            if (!m_HasChild)
                return;

            if (value)
            {
                m_StateManager.UncollapsedCollections.Add(name);
            }
            else
            {
                m_StateManager.UncollapsedCollections.Remove(name);
            }
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
