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
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IEditorUtilityProxy m_EditorUtilityProxy;
        readonly IPageManager m_PageManager;
        readonly IStateManager m_StateManager;
        readonly CollectionInfo m_CollectionInfo;

        VisualElement m_Target;

        bool IsEnabled => m_UnityConnectProxy.AreCloudServicesReachable && m_CollectionInfo != null;

        public CollectionContextMenu(CollectionInfo collectionInfo, IUnityConnectProxy unityConnectProxy,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager, IStateManager stateManager)
        {
            m_CollectionInfo = collectionInfo;
            m_UnityConnectProxy = unityConnectProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_StateManager = stateManager;
            m_EditorUtilityProxy = ServicesContainer.instance.Resolve<IEditorUtilityProxy>();
        }

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            // Check the target to avoid adding the same menu entries multiple times add don't know which one is called
            if (evt.target == evt.currentTarget)
            {
                m_Target = (VisualElement)evt.target;
                AddMenuEntry(evt, Constants.CollectionDelete, IsEnabled,
                    (_) =>
                    {
                        TaskUtils.TrackException(DeleteCollectionAsync());
                    });
                AddMenuEntry(evt, Constants.CollectionRename, IsEnabled,
                    (_) =>
                    {
                        RenameCollection();
                    });
                AddMenuEntry(evt, Constants.CollectionCreate, IsEnabled,
                    (_) =>
                    {
                        CreateCollection();
                    });
            }
        }

        void CreateCollection()
        {
            var name = Constants.CollectionDefaultName;
            var projectInfo = m_ProjectOrganizationProvider.GetProject(m_CollectionInfo.ProjectId);

            var index = 1;
            while(projectInfo.CollectionInfos.Any(c => c.GetFullPath() == $"{m_CollectionInfo.GetFullPath()}/{name}"))
            {
                name = $"{Constants.CollectionDefaultName} ({index++})";
            }

            var newFoldout = new SideBarCollectionFoldout(m_UnityConnectProxy, m_PageManager, m_StateManager, m_ProjectOrganizationProvider,
                name, projectInfo, m_CollectionInfo.GetFullPath());
            m_Target.Add(newFoldout);
            newFoldout.StartNaming();
        }

        void RenameCollection()
        {
            if(!(m_Target is SideBarCollectionFoldout foldout))
                return;

            foldout.StartRenaming();
        }

        async Task DeleteCollectionAsync()
        {
            var projectInfo = m_ProjectOrganizationProvider.GetProject(m_CollectionInfo.ProjectId);
            var path = $"{projectInfo.Name} > {m_CollectionInfo.GetFullPath().Replace("/", " > ")}";
            if (m_EditorUtilityProxy.DisplayDialog(L10n.Tr(Constants.CollectionDeleteTitle),
                    $"{L10n.Tr(Constants.CollectionDeleteMessage)}\n\n{path}", L10n.Tr(Constants.CollectionDeleteOk),
                    L10n.Tr(Constants.CollectionDeleteCancel)))
            {
                // Check if the collection is selected before it is deleted, otherwise the selection will be lost
                var isSelected = m_CollectionInfo.ProjectId == m_ProjectOrganizationProvider.SelectedCollection.ProjectId &&
                                 m_CollectionInfo.GetFullPath() == m_ProjectOrganizationProvider.SelectedCollection.GetFullPath();

                try
                {
                    await m_ProjectOrganizationProvider.DeleteCollection(m_CollectionInfo);

                    AnalyticsSender.SendEvent(new ManageCollectionEvent(ManageCollectionEvent.CollectionOperationType.Delete));
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

                if(isSelected)
                {
                    m_ProjectOrganizationProvider.SelectProject(projectInfo, m_CollectionInfo.ParentPath);
                }
            }
        }
    }
}
