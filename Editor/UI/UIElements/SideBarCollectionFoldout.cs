using System;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace Unity.AssetManager.UI.Editor
{
    class SideBarCollectionFoldout : SideBarFoldout
    {
        static readonly string k_IconFolderOpen = "icon-folder-open";
        static readonly string k_IconFolderClose = "icon-folder-close";

        readonly string m_ProjectId;
        readonly Image m_Icon;
        readonly Label m_Label;
        readonly TextField m_TextField;

        ContextualMenuManipulator m_ContextualMenuManipulator;

        string m_CollectionPath;

        internal SideBarCollectionFoldout(IStateManager stateManager, IMessageManager messageManager, IProjectOrganizationProvider projectOrganizationProvider,
            string foldoutName, string projectId, string collectionPath, bool isPopulated)
            : base(stateManager, messageManager, projectOrganizationProvider, foldoutName, isPopulated)
        {
            m_ProjectId = projectId;
            m_CollectionPath = collectionPath;
            name = GetCollectionId(m_ProjectId, m_CollectionPath);

            var iconParent = this.Q(className: inputUssClassName);
            m_Icon = iconParent.Q<Image>();
            m_Label = m_Toggle.Q<Label>();
            m_TextField = new TextField();
            m_TextField.selectAllOnFocus = false;
            m_TextField.selectAllOnMouseUp = false;
            m_TextField.AddToClassList("sidebar-text-field");
            m_Label.parent.Add(m_TextField);
            UIElementsUtils.Hide(m_TextField);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public static string GetCollectionId(string projectId, string collectionPath)
        {
            return string.IsNullOrEmpty(collectionPath) ? projectId : $"{projectId}::{collectionPath}";
        }

        public void StartRenaming()
        {
            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();
            m_TextField.SelectAll();

            m_TextField.RegisterCallback<FocusOutEvent>(Rename);
        }

        public void StartNaming()
        {
            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();
            m_TextField.SelectAll();

            m_TextField.RegisterCallback<FocusOutEvent>(OnNameSet);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (m_ProjectOrganizationProvider.SelectedOrganization == null)
            {
                Utilities.DevLog("Organization is not selected, cannot create collection foldout.");
                return;
            }
            
            RegisterEventForIconChange();
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);

            if (m_ContextualMenuManipulator != null)
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
            }

            if (m_CollectionPath != null)
            {
                var collectionInfo = CollectionInfo.CreateFromFullPath(
                    m_ProjectOrganizationProvider.SelectedOrganization.Id,
                    m_ProjectId,
                    m_CollectionPath);
                var contextMenu = new CollectionContextMenu(collectionInfo, m_ProjectOrganizationProvider, m_StateManager, m_MessageManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }
            else
            {
                var contextMenu = new ProjectContextMenu(m_ProjectId, m_ProjectOrganizationProvider,
                    m_StateManager, m_MessageManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }

            this.AddManipulator(m_ContextualMenuManipulator);
            SetIcon();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnRegisterEventForIconChange();
            UnregisterCallback<PointerDownEvent>(OnPointerDown);

            this.RemoveManipulator(m_ContextualMenuManipulator);
            m_ContextualMenuManipulator = null;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int) MouseButton.LeftMouse)
                return;

            // We skip the user's click if they aimed the check mark of the foldout
            // to only select foldouts when they click on it's title/label
            if (evt.target != this)
                return;

            m_ProjectOrganizationProvider.SelectProject(m_ProjectId, m_CollectionPath, updateProject: m_CollectionPath == null);
        }

        public override void SetSelected(bool selected)
        {
            base.SetSelected(selected);
            
            m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);

            if (selected)
            {
                m_Toggle.RemoveFromClassList(k_EmptyFoldoutClassName);
                UncollapseHierarchy();
            }
            else
            {
                m_Toggle.EnableInClassList(k_EmptyFoldoutClassName, !m_IsPopulated);
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

            if (string.IsNullOrWhiteSpace(m_TextField.value))
                return;

            var collectionName = m_TextField.value.Trim();

            m_Label.text = collectionName;

            var collectionInfo = new CollectionInfo(
                m_ProjectOrganizationProvider.SelectedOrganization.Id,
                m_ProjectId,
                collectionName,
                m_CollectionPath);

            m_CollectionPath = collectionInfo.GetFullPath();
            name = GetCollectionId(m_ProjectId, m_CollectionPath);

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
                    m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage(e.Message,
                        messageType:HelpBoxMessageType.Error));
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

            var collectionName = m_TextField.value.Trim();

            if (m_Label.text == collectionName)
                return;

            if (string.IsNullOrWhiteSpace(m_TextField.value))
                return;

            m_Label.text = collectionName;
            var collectionInfo = CollectionInfo.CreateFromFullPath(
                m_ProjectOrganizationProvider.SelectedOrganization.Id,
                m_ProjectId,
                m_CollectionPath);

            try
            {
                await m_ProjectOrganizationProvider.RenameCollection(collectionInfo, collectionName);

                AnalyticsSender.SendEvent(new ManageCollectionEvent(ManageCollectionEvent.CollectionOperationType.Rename));
            }
            catch (Exception e)
            {
                var serviceExceptionInfo = ServiceExceptionHelper.GetServiceExceptionInfo(e);

                if(serviceExceptionInfo != null)
                {
                    m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage(e.Message,
                        messageType:HelpBoxMessageType.Error));
                }
                throw;
            }
        }
    }
}
