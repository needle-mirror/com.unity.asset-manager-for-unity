using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class CollectionContextMenu : ContextMenu
    {
        readonly IEditorUtilityProxy m_EditorUtilityProxy;
        readonly ProjectCollectionContextMenuViewModel m_ViewModel;
        readonly IStateManager m_StateManager;
        readonly IMessageManager m_MessageManager;

        public CollectionContextMenu(ProjectCollectionContextMenuViewModel viewModel, IStateManager stateManager, IMessageManager messageManager)
        {
            m_ViewModel = viewModel;
            m_EditorUtilityProxy = ServicesContainer.instance.Resolve<IEditorUtilityProxy>();
            m_MessageManager = messageManager;
            m_StateManager = stateManager;
        }


        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            // Check the target to avoid adding the same menu entries multiple times add don't know which one is called
            if (evt.target == evt.currentTarget)
            {
                var targetElement = (VisualElement) evt.currentTarget;
                AddMenuEntry(evt, Constants.CollectionCreate,
                    m_ViewModel.IsEnabled && !string.IsNullOrEmpty(m_ViewModel.GetProjectId()),
                    _ =>
                    {
                        CreateCollection(targetElement);
                    });
            }

            // Check the target to avoid adding the same menu entries multiple times add don't know which one is called
            if (evt.target == evt.currentTarget)
            {
                var targetElement = (VisualElement) evt.currentTarget;
                AddMenuEntry(evt, Constants.CollectionDelete, m_ViewModel.IsEnabled,
                    _ =>
                    {
                        TaskUtils.TrackException(DeleteCollectionAsync());
                    });
                AddMenuEntry(evt, Constants.CollectionRename, m_ViewModel.IsEnabled,
                    _ =>
                    {
                        RenameCollection(targetElement);
                    });
            }
        }

        void CreateCollection(VisualElement target)
        {
            var name = Constants.CollectionDefaultName;
            var projectInfo = m_ViewModel.GetProjectInfo();

            if (projectInfo?.CollectionInfos != null)
            {
                var index = 1;
                while (projectInfo.CollectionInfos.Any(c => c.Name == name && c.ParentPath == m_ViewModel.GetCollectionPath()))
                {
                    name = $"{Constants.CollectionDefaultName} ({index++})";
                }
            }

            var newFoldout = new SidebarCollectionFoldout(
                m_ViewModel.GetChildSidebarCollectionFoldoutViewModel(m_ViewModel.GetCollectionPath(), name),
                m_StateManager, m_MessageManager, true);
            target.Add(newFoldout);
            newFoldout.StartNaming(() => OnNamingFailed(target, newFoldout));
        }

        static void RenameCollection(VisualElement target)
        {
            if (target is not SidebarCollectionFoldout foldout)
                return;

            foldout.StartRenaming();
        }

        async Task DeleteCollectionAsync()
        {
            var projectInfo = m_ViewModel.GetProjectInfo();
            var path = $"{projectInfo.Name} > {m_ViewModel.GetCollectionPath().Replace("/", " > ")}";
            if (m_EditorUtilityProxy.DisplayDialog(L10n.Tr(Constants.CollectionDeleteTitle),
                    $"{L10n.Tr(Constants.CollectionDeleteMessage)}\n\n{path}", L10n.Tr(Constants.CollectionDeleteOk),
                    L10n.Tr(Constants.CollectionDeleteCancel)))
            {
                try
                {
                    await m_ViewModel.DeleteCollection();
                }
                catch (Exception e)
                {
                    var serviceExceptionInfo = ServiceExceptionHelper.GetServiceExceptionInfo(e);
                    if (serviceExceptionInfo != null)
                    {
                        m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage(e.Message,
                            messageType: HelpBoxMessageType.Error));
                    }

                    throw;
                }
            }
        }

        static void OnNamingFailed(VisualElement target, SidebarCollectionFoldout newFoldout)
        {
            target.Remove(newFoldout);
        }
    }
}
