using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class ProjectCollectionContextMenuViewModel
    {
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly string m_ProjectId;
        readonly CollectionInfo m_CollectionInfo;

        public bool IsEnabled => m_UnityConnectProxy.AreCloudServicesReachable;

        public ProjectCollectionContextMenuViewModel(string projectId, IProjectOrganizationProvider projectOrganizationProvider,
            IUnityConnectProxy unityConnectProxy, CollectionInfo collectionInfo = null)
        {
            m_ProjectId = projectId;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;
            m_CollectionInfo = collectionInfo;
        }

        public ProjectOrLibraryInfo GetProjectInfo()
        {
            return m_ProjectOrganizationProvider.GetProject(m_ProjectId);
        }

        public string GetProjectId()
        {
            return m_ProjectId;
        }

        public string GetCollectionPath()
        {
            return m_CollectionInfo?.GetFullPath();
        }

        public bool IsCollectionSelected()
        {
            return m_ProjectOrganizationProvider.SelectedCollection != null && m_ProjectId == m_ProjectOrganizationProvider.SelectedCollection.ProjectId &&
                   m_CollectionInfo != null && m_CollectionInfo.GetFullPath() == m_ProjectOrganizationProvider.SelectedCollection.GetFullPath();
        }

        public async Task DeleteCollection()
        {
            // Check if the collection is selected before it is deleted, otherwise the selection will be lost
            var isSelected = IsCollectionSelected();

            await m_ProjectOrganizationProvider.DeleteCollection(m_CollectionInfo);

            AnalyticsSender.SendEvent(new ManageCollectionEvent(ManageCollectionEvent.CollectionOperationType.Delete));

            if (isSelected)
            {
                m_ProjectOrganizationProvider.SelectProject(m_ProjectId, m_CollectionInfo.ParentPath);
            }
        }

        public SidebarCollectionFoldoutViewModel GetChildSidebarCollectionFoldoutViewModel(string collectionPath, string name)
        {
            return new SidebarCollectionFoldoutViewModel(m_ProjectId, name, m_ProjectOrganizationProvider,
                collectionPath, false, m_UnityConnectProxy);
        }
    }
}
