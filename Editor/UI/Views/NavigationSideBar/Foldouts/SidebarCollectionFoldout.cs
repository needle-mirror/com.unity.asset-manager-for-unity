using System;
using System.Text.RegularExpressions;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

namespace Unity.AssetManager.UI.Editor
{
    // Struct to hold the naming state of a Foldout in order to restore editing state after UI rebuilds
    struct FoldoutNamingState
    {
        public bool IsNaming { get; }
        public bool IsRenaming { get; }
        public string NamingInput { get; }
        public Action OnNamingFailed { get; set; }
        public string CollectionId { get; }
        public string ParentCollectionId { get; }

        public FoldoutNamingState(bool isNaming, bool isRenaming, string namingInput, Action onNamingFailed, string collectionId = null, string parentCollectionId = null)
        {
            IsNaming = isNaming;
            IsRenaming = isRenaming;
            NamingInput = namingInput;
            OnNamingFailed = onNamingFailed;
            CollectionId = collectionId;
            ParentCollectionId = parentCollectionId;
        }

        public bool IsInNamingMode => IsNaming || IsRenaming;
    }

    class SidebarCollectionFoldout : SidebarFoldout
    {
        static readonly string k_IconFolderOpen = "icon-folder-open";
        static readonly string k_IconFolderClose = "icon-folder-close";

        readonly SidebarCollectionFoldoutViewModel m_ViewModel;

        readonly Image m_Icon;
        readonly Label m_Label;
        readonly TextField m_TextField;

        ContextualMenuManipulator m_ContextualMenuManipulator;

        bool m_IsNaming;
        bool m_IsRenaming;
        Action m_OnNamingFailed;

        public string CollectionPath => m_ViewModel.CollectionPath;

        public FoldoutNamingState GetNamingState()
        {
            return new FoldoutNamingState(m_IsNaming, m_IsRenaming, m_TextField?.value ?? string.Empty,
                m_OnNamingFailed, name, (parent as SidebarCollectionFoldout)?.name);
        }

        public void RestoreNamingState(FoldoutNamingState state)
        {
            if (state.IsNaming)
            {
                StartNaming(state.OnNamingFailed);
                m_TextField.value = state.NamingInput;
            }
            else if (state.IsRenaming)
            {
                StartRenaming();
                m_TextField.value = state.NamingInput;
            }
        }

        internal SidebarCollectionFoldout(SidebarCollectionFoldoutViewModel viewModel, IStateManager stateManager, IMessageManager messageManager, bool isPopulated)
            : base(stateManager, messageManager, viewModel.Name, isPopulated)
        {
            m_ViewModel = viewModel;
            name = GetCollectionId(m_ViewModel.ProjectId, m_ViewModel.CollectionPath);

            var foldoutContentParent = this.Q(className: inputUssClassName);
            foldoutContentParent.style.flexShrink = 1;

            m_Icon = foldoutContentParent.Q<Image>();
            m_Label = m_Toggle.Q<Label>();
            m_Label.AddToClassList("sidebar-collection-foldout-label");
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
            m_IsRenaming = true;
            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();
            m_TextField.SelectAll();

            m_TextField.RegisterCallback<FocusOutEvent>(Rename);

            ScrollToThisElement();
        }

        public void StartNaming(Action onNamingFailed = null)
        {
            m_IsNaming = true;
            m_OnNamingFailed = onNamingFailed;

            UIElementsUtils.Hide(m_Label);
            UIElementsUtils.Show(m_TextField);
            m_TextField.value = m_Label.text;
            m_TextField.Focus();
            m_TextField.SelectAll();

            m_TextField.RegisterCallback<FocusOutEvent>(OnNameSet);

            ScrollToThisElement();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (string.IsNullOrEmpty(m_ViewModel.GetOrganizationId()))
            {
                Utilities.DevLog("Organization is not selected, cannot create collection foldout.");
                return;
            }

            m_ViewModel.BindEvents();

            RegisterEventForIconChange();
            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            m_ViewModel.ProjectInfoChanged += OnProjectInfoChanged;

            SetIcon();

            if (!m_ViewModel.ContextualMenuEnabled)
                return;

            if (m_ContextualMenuManipulator != null)
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
            }

            if (!string.IsNullOrEmpty(m_ViewModel.CollectionPath))
            {
                var contextMenu = new CollectionContextMenu(m_ViewModel.GetProjectCollectionContextMenuViewModel(), m_StateManager, m_MessageManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }
            else
            {
                var contextMenu = new ProjectContextMenu(m_ViewModel.GetProjectCollectionContextMenuViewModel(),
                    m_StateManager, m_MessageManager);
                m_ContextualMenuManipulator = new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries);
            }

            this.AddManipulator(m_ContextualMenuManipulator);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ViewModel.UnbindEvents();
            UnRegisterEventForIconChange();
            UnregisterCallback<PointerDownEvent>(OnPointerDown);

            this.RemoveManipulator(m_ContextualMenuManipulator);
            m_ContextualMenuManipulator = null;

            m_ViewModel.ProjectInfoChanged -= OnProjectInfoChanged;
        }

        void OnProjectInfoChanged(string projectName)
        {
            m_Label.text = projectName;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int) MouseButton.LeftMouse)
                return;

            // We skip the user's click if they aimed the check mark of the foldout
            // to only select foldouts when they click on it's title/label
            if (evt.target != this)
                return;

            m_ViewModel.SelectProject();
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
            while (p is SidebarCollectionFoldout foldout)
            {
                foldout.value = true;
                p = foldout.parent;
            }
        }

        public override void AddFoldout(SidebarCollectionFoldout child)
        {
            value = m_StateManager.UncollapsedCollections.Contains(name); // Do not force the foldout to close if something else (like auto selection) is forcing it to open

            base.AddFoldout(child);

            if (string.IsNullOrEmpty(m_ViewModel.CollectionPath))
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
            if (string.IsNullOrEmpty(m_ViewModel.CollectionPath) || !m_HasChild)
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

        void ScrollToThisElement()
        {
            var element = parent;
            while (element != null)
            {
                if (element is SidebarContent sidebarContent)
                {
                    sidebarContent.ScrollToElement(this);
                    return;
                }
                element = element.parent;
            }
        }

        bool IsValidCollectionName(string collectionName)
        {
            return !string.IsNullOrWhiteSpace(collectionName) &&
                   Regex.IsMatch(collectionName, @"^[^%]+$") &&
                   collectionName.Trim() != ".";
        }

        async void OnNameSet(FocusOutEvent evt)
        {
            m_IsNaming = false;
            UIElementsUtils.Hide(m_TextField);
            UIElementsUtils.Show(m_Label);
            m_TextField.UnregisterCallback<FocusOutEvent>(OnNameSet);

            if (string.IsNullOrWhiteSpace(m_TextField.value))
                return;

            var collectionName = m_TextField.value.Trim();

            if (!IsValidCollectionName(collectionName))
            {
                m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage("Collection name cannot contain '%' character or be a single '.' character.",
                    RecommendedAction.None, messageType:HelpBoxMessageType.Error));
                m_OnNamingFailed?.Invoke();
                return;
            }

            m_Label.text = collectionName;

            var collectionInfo = new CollectionInfo(
                m_ViewModel.GetOrganizationId(),
                m_ViewModel.ProjectId,
                collectionName,
                m_ViewModel.CollectionPath);

            name = GetCollectionId(m_ViewModel.ProjectId, m_ViewModel.CollectionPath);

            try
            {
                await m_ViewModel.CreateCollection(collectionInfo);

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

                m_OnNamingFailed?.Invoke();

                throw;
            }
            finally
            {
                UIElementsUtils.Hide(this);
            }
        }

        async void Rename(FocusOutEvent evt)
        {
            m_IsRenaming = false;
            UIElementsUtils.Hide(m_TextField);
            UIElementsUtils.Show(m_Label);
            m_TextField.UnregisterCallback<FocusOutEvent>(Rename);

            var collectionName = m_TextField.value.Trim();

            if (m_Label.text == collectionName)
                return;

            if (string.IsNullOrWhiteSpace(m_TextField.value))
                return;

            if (!IsValidCollectionName(collectionName))
            {
                m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage("Collection name cannot contain '%' character or be a single '.' character.",
                    RecommendedAction.None, messageType:HelpBoxMessageType.Error));
                return;
            }

            m_Label.text = collectionName;
            var collectionInfo = CollectionInfo.CreateFromFullPath(
                m_ViewModel.GetOrganizationId(),
                m_ViewModel.ProjectId,
                m_ViewModel.CollectionPath);

            try
            {
                await m_ViewModel.RenameCollection(collectionInfo, collectionName);

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
