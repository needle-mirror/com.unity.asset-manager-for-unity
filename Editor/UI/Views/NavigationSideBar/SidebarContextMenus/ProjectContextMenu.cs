using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class ProjectContextMenu : ContextMenu
    {
        readonly ProjectCollectionContextMenuViewModel m_ViewModel;
        readonly IMessageManager m_MessageManager;
        readonly IStateManager m_StateManager;

        public ProjectContextMenu(ProjectCollectionContextMenuViewModel viewModel, IStateManager stateManager, IMessageManager messageManager)
        {
            m_ViewModel = viewModel;
            m_StateManager = stateManager;
            m_MessageManager = messageManager;
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
        }

        void CreateCollection(VisualElement target)
        {
            var name = Constants.CollectionDefaultName;
            var projectInfo = m_ViewModel.GetProjectInfo();

            if (projectInfo?.CollectionInfos != null)
            {
                var index = 1;
                while (projectInfo.CollectionInfos.Any(c => c.Name == name && c.ParentPath == string.Empty))
                {
                    name = $"{Constants.CollectionDefaultName} ({index++})";
                }
            }

            var newFoldout = new SidebarCollectionFoldout(
                m_ViewModel.GetChildSidebarCollectionFoldoutViewModel(string.Empty, name), m_StateManager,
                m_MessageManager, true);
            target.Add(newFoldout);
            newFoldout.StartNaming(() => OnNamingFailed(target, newFoldout));
        }



        static void OnNamingFailed(VisualElement target, SidebarCollectionFoldout newFoldout)
        {
            target.Remove(newFoldout);
        }
    }
}
