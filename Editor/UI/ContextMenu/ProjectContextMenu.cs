using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class ProjectContextMenu: ContextMenu
    {
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IPageManager m_PageManager;
        readonly IStateManager m_StateManager;
        readonly ProjectInfo m_ProjectInfo;

        VisualElement m_Target;

        public ProjectContextMenu(ProjectInfo projectInfo, IUnityConnectProxy unityConnectProxy,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager,
            IStateManager stateManager)
        {
            m_ProjectInfo = projectInfo;
            m_UnityConnectProxy = unityConnectProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_StateManager = stateManager;
        }

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            // Check the target to avoid adding the same menu entries multiple times add don't know which one is called
            if (evt.target == evt.currentTarget)
            {
                m_Target = (VisualElement)evt.target;
                AddMenuEntry(evt, Constants.CollectionCreate,
                    m_UnityConnectProxy.AreCloudServicesReachable && m_ProjectInfo != null,
                    (_) =>
                    {
                        CreateCollection();
                    });
            }
        }

        void CreateCollection()
        {
            var name = Constants.CollectionDefaultName;

            var index = 1;
            while(m_ProjectInfo.CollectionInfos.Any(c => c.GetFullPath() == $"{name}"))
            {
                name = $"{Constants.CollectionDefaultName} ({index++})";
            }

            var newFoldout = new SideBarCollectionFoldout(m_UnityConnectProxy, m_PageManager, m_StateManager, m_ProjectOrganizationProvider,
                name, m_ProjectInfo, string.Empty);
            m_Target.Add(newFoldout);
            newFoldout.StartNaming();
        }
    }
}
