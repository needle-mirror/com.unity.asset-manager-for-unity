using System;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace Unity.AssetManager.UI.Editor
{
    class SideBarCollectionFoldout : SideBarFoldout
    {
        static readonly string k_IconFolderOpen = "icon-folder-open";
        static readonly string k_IconFolderClose = "icon-folder-close";

        readonly ProjectInfo m_ProjectInfo;
        readonly IUploadManager m_UploadManager;
        readonly Image m_Icon;
        readonly Label m_Label;
        readonly TextField m_TextField;

        ContextualMenuManipulator m_ContextualMenuManipulator;

        string m_CollectionPath;

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
            m_Label = m_Toggle.Q<Label>();
            m_TextField = new TextField();
            m_Label.parent.Add(m_TextField);
            UIElementsUtils.Hide(m_TextField);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public static string GetCollectionId(ProjectInfo projectInfo, string collectionPath)
        {
            return string.IsNullOrEmpty(collectionPath) ? projectInfo.Id : $"{projectInfo.Id}::{collectionPath}";
        }

        public void StartRenaming()
        {
            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();

            m_TextField.RegisterCallback<FocusOutEvent>(Rename);
        }

        public void StartNaming()
        {
            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();

            m_TextField.RegisterCallback<FocusOutEvent>(OnNameSet);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            RegisterEventForIconChange();
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            if (m_ContextualMenuManipulator != null)
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
            }

            if (m_CollectionPath != null)
            {
                var collectionInfo = CollectionInfo.CreateFromFullPath(m_CollectionPath);
                collectionInfo.ProjectId = m_ProjectInfo.Id;
                collectionInfo.OrganizationId = m_ProjectOrganizationProvider.SelectedOrganization.Id;
                var contextMenu = new CollectionContextMenu(collectionInfo, m_UnityConnectProxy, m_ProjectOrganizationProvider, m_PageManager, m_StateManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }
            else
            {
                var contextMenu = new ProjectContextMenu(m_ProjectInfo, m_UnityConnectProxy, m_ProjectOrganizationProvider,
                    m_PageManager, m_StateManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }
            this.AddManipulator(m_ContextualMenuManipulator);
            SetIcon();

            m_UploadManager.UploadBegan += OnUploadBegan;
            m_UploadManager.UploadEnded += OnUploadEnded;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnRegisterEventForIconChange();
            UnregisterCallback<PointerDownEvent>(OnPointerDown);

            this.RemoveManipulator(m_ContextualMenuManipulator);
            m_ContextualMenuManipulator = null;

            m_UploadManager.UploadBegan -= OnUploadBegan;
            m_UploadManager.UploadEnded -= OnUploadEnded;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if(evt.button != (int)MouseButton.LeftMouse)
                return;

            // We skip the user's click if they aimed the check mark of the foldout
            // to only select foldouts when they click on it's title/label
            if (evt.target != this)
                return;

            m_ProjectOrganizationProvider.SelectProject(m_ProjectInfo, m_CollectionPath, updateProject:m_CollectionPath == null);
        }

        void OnUploadBegan()
        {
            SetEnabled(false);
        }

        void OnUploadEnded(UploadEndedStatus _)
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

        async void OnNameSet(FocusOutEvent evt)
        {
            UIElementsUtils.Hide(m_TextField);
            UIElementsUtils.Show(m_Label);
            m_TextField.UnregisterCallback<FocusOutEvent>(OnNameSet);

            if(string.IsNullOrEmpty(m_TextField.value))
                return;

            m_Label.text = m_TextField.value;

            var collectionInfo = new CollectionInfo
            {
                OrganizationId = m_ProjectOrganizationProvider.SelectedOrganization.Id,
                ProjectId =  m_ProjectInfo.Id,
                ParentPath = m_CollectionPath,
                Name = m_TextField.value
            };

            m_CollectionPath += $"/{m_TextField.value}";
            name = GetCollectionId(m_ProjectInfo, m_CollectionPath);

            try
            {
                await m_ProjectOrganizationProvider.CreateCollection(collectionInfo);

                AnalyticsSender.SendEvent(new ManageCollectionEvent(ManageCollectionEvent.CollectionOperationType.Create));
            }
            catch (Exception e)
            {
                var serviceExceptionInfo = ServiceExceptionHelper.GetServiceExceptionInfo(e);
                if (serviceExceptionInfo != null)
                {
                    m_PageManager.ActivePage.SetMessageData(e.Message, RecommendedAction.None, false,
                        HelpBoxMessageType.Error);
                }

                throw;
            }
            finally
            {
                UIElementsUtils.Hide(this);
            }
        }

        async void Rename(FocusOutEvent evt)
        {
            UIElementsUtils.Hide(m_TextField);
            UIElementsUtils.Show(m_Label);
            m_TextField.UnregisterCallback<FocusOutEvent>(Rename);

            if(m_Label.text == m_TextField.value)
                return;

            m_Label.text = m_TextField.value;
            var collectionInfo = CollectionInfo.CreateFromFullPath(m_CollectionPath);
            collectionInfo.ProjectId = m_ProjectInfo.Id;
            collectionInfo.OrganizationId = m_ProjectOrganizationProvider.SelectedOrganization.Id;

            try
            {
                await m_ProjectOrganizationProvider.RenameCollection(collectionInfo, m_TextField.value);

                AnalyticsSender.SendEvent(new ManageCollectionEvent(ManageCollectionEvent.CollectionOperationType.Rename));
            }
            catch (Exception e)
            {
                var serviceExceptionInfo = ServiceExceptionHelper.GetServiceExceptionInfo(e);

                if(serviceExceptionInfo != null)
                {
                    m_PageManager.ActivePage.SetMessageData(e.Message, RecommendedAction.None, false,
                        HelpBoxMessageType.Error);
                }
                throw;
            }
        }
    }
}
