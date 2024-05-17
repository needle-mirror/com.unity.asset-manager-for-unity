using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    enum Role
    {
        None,
        Contributor,
        Consumer,
        Viewer
    }

    interface IPermissionsManager : IService
    {
        Role Role { get; }
        bool CheckPermission(string permission);
        Task<Role> FetchRoleAsync(string projectId);
    }

    [Serializable]
    class PermissionsManager : BaseService<IPermissionsManager>, IPermissionsManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        Role m_Role;

        [SerializeField]
        string[] m_SerializableOrganizationPermissions;

        [SerializeField]
        string[] m_SerializablePermissions;

        Permission[] m_OrganizationPermissions;

        Permission[] m_Permissions;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        IOrganization m_Organization;

        public Role Role => m_Role;

        [ServiceInjection]
        public void Inject(IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public override void OnEnable()
        {
            base.OnEnable();

            if (m_ProjectOrganizationProvider != null)
            {
                _ = Init();
                m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;
                m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();

            if (m_ProjectOrganizationProvider != null)
            {
                m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
                m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            }
        }

        public bool CheckPermission(string permission)
        {
            return m_Permissions?.Any(p => p.ToString() == permission) ?? false;
        }

        public async Task<Role> FetchRoleAsync(string projectId)
        {
           m_Role = await FetchRoleAsyncInternal(projectId);
           return m_Role;
        }

        async Task<Role> FetchRoleAsyncInternal(string projectId)
        {
            if (m_Organization == null)
            {
                if (m_ProjectOrganizationProvider.SelectedOrganization != null)
                {
                    await SetOrganizationAsync(m_ProjectOrganizationProvider.SelectedOrganization);
                }
                else
                {
                    return Role.None;
                }

                if (m_Organization == null)
                {
                    return Role.None;
                }
            }

            var orgRoles = await m_Organization.ListRolesAsync();

            // Asset Manager Admin, Manager, and Owner roles have by default all the permissions
            if (orgRoles.Any(r => r.ToString().ToLower() == "asset manager admin") ||
                orgRoles.Any(r => r.ToString().ToLower() == "manager") ||
                orgRoles.Any(r => r.ToString().ToLower() == "owner"))
            {
                return Role.Contributor;
            }

            await foreach (var project in m_Organization.ListProjectsAsync(Range.All))
            {
                if (project.Descriptor.ProjectId.ToString() == projectId)
                {
                    var projectRoles = await project.ListRolesAsync();
                    if (projectRoles.Any(r => r.ToString().ToLower() == "asset manager contributor"))
                    {
                        return Role.Contributor;
                    }

                    if (projectRoles.Any(r => r.ToString().ToLower() == "asset manager consumer"))
                    {
                        return Role.Consumer;
                    }

                    return Role.Viewer;
                }
            }

            return Role.Viewer;
        }

        async Task Init()
        {
            await SetOrganizationAsync(m_ProjectOrganizationProvider.SelectedOrganization);
            await SetProjectAsync(m_ProjectOrganizationProvider.SelectedProject);
        }

        async Task SetOrganizationAsync(OrganizationInfo organizationInfo)
        {
            if (organizationInfo == null)
                return;

            var organizations = Services.OrganizationRepository.ListOrganizationsAsync(Range.All);
            await foreach (var organization in organizations)
            {
                if (organization.Id.ToString() == organizationInfo.Id)
                {
                    m_Organization = organization;
                    break;
                }
            }

            if (m_Organization == null)
                return;

            var orgPermissions = await m_Organization.ListPermissionsAsync();
            m_OrganizationPermissions = orgPermissions.ToArray();
            m_Permissions = m_OrganizationPermissions;
        }

        async Task SetProjectAsync(ProjectInfo projectInfo)
        {
            if (m_Organization == null)
            {
                if (m_ProjectOrganizationProvider.SelectedOrganization != null)
                {
                    await SetOrganizationAsync(m_ProjectOrganizationProvider.SelectedOrganization);
                }
                else
                {
                    return;
                }
            }

            if (projectInfo == null || string.IsNullOrEmpty(projectInfo.Id))
            {
                m_Permissions = m_OrganizationPermissions;
                return;
            }

            var projectsAsync = m_Organization.ListProjectsAsync(Range.All);
            await foreach (var project in projectsAsync)
            {
                if (project.Descriptor.ProjectId.ToString() == projectInfo.Id)
                {
                    var projectPermissions = await project.ListPermissionsAsync();
                    m_Permissions = m_OrganizationPermissions.Concat(projectPermissions).ToArray();
                    break;
                }
            }
        }

        void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            _ = SetProjectAsync(projectInfo);
        }

        void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            Reset();
            _ = SetOrganizationAsync(organizationInfo);
        }

        void Reset()
        {
            m_Permissions = null;
            m_OrganizationPermissions = null;
            m_Organization = null;
        }

        public void OnBeforeSerialize()
        {
            m_SerializableOrganizationPermissions = m_OrganizationPermissions?.Select(p => p.ToString()).ToArray();
            m_SerializablePermissions = m_Permissions?.Select(p => p.ToString()).ToArray();
        }

        public void OnAfterDeserialize()
        {
            m_OrganizationPermissions = m_SerializableOrganizationPermissions?.Select(p => new Permission(p)).ToArray();
            m_Permissions = m_SerializablePermissions?.Select(p => new Permission(p)).ToArray();
        }
    }
}
