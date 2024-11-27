using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using Unity.Cloud.IdentityEmbedded;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
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
        Task<Role> GetRoleAsync(string organizationId, string projectId);
        Task<bool> CheckPermissionAsync(string organizationId, string projectId, string permission);

        void Reset();
    }

    [Serializable]
    class OrganizationProjectPair: IEquatable<OrganizationProjectPair>
    {
        [SerializeField]
        string m_OrganizationId;

        [SerializeField]
        string m_ProjectId;

        public string OrganizationId => m_OrganizationId;
        public string ProjectId => m_ProjectId;

        public OrganizationProjectPair(string organizationId, string projectId)
        {
            m_OrganizationId = organizationId;
            m_ProjectId = projectId;
        }

        public bool Equals(OrganizationProjectPair other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return OrganizationId == other.OrganizationId && ProjectId == other.ProjectId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((OrganizationProjectPair)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OrganizationId, ProjectId);
        }
    }

    [Serializable]
    class PermissionsManager : BaseService<IPermissionsManager>, IPermissionsManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        string[] m_SerializableOrganizationIds;

        [SerializeField]
        string[] m_SerializableOrganizationPermissions;

        [SerializeField]
        int[] m_SerializableOrganizationPermissionRanges;

        [SerializeField]
        OrganizationProjectPair[] m_SerializablePermissionKeys;

        [SerializeField]
        string[] m_SerializablePermissions;

        [SerializeField]
        int[] m_SerializablePermissionRanges;

        [SerializeField]
        OrganizationProjectPair[] m_SerializableRoleKeys;

        [SerializeField]
        int[] m_SerializableRoles;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        readonly Dictionary<string, Permission[]> m_OrganizationPermissions = new();
        readonly Dictionary<OrganizationProjectPair, Role> m_CachedRoles = new();
        readonly Dictionary<OrganizationProjectPair, Permission[]> m_CachedPermissions = new();
        readonly Dictionary<string, IOrganization> m_Organizations = new();

        static readonly string k_AssetManagerAdmin = "asset manager admin";
        static readonly string k_Manager = "manager";
        static readonly string k_Owner = "owner";
        static readonly string k_AssetManagerContributor = "asset manager contributor";
        static readonly string k_AssetManagerConsumer = "asset manager consumer";

        [ServiceInjection]
        public void Inject(IAssetsProvider assetsProvider)
        {
            m_AssetsProvider = assetsProvider;
        }

        public async Task<Role> GetRoleAsync(string organizationId, string projectId)
        {
            var key = new OrganizationProjectPair(organizationId, projectId);

            if (m_CachedRoles.TryGetValue(key, out var role))
            {
                return role;
            }

            return await FetchRoleAsync(key);
        }

        public async Task<bool> CheckPermissionAsync(string organizationId, string projectId, string permission)
        {
            var key = new OrganizationProjectPair(organizationId, projectId);

            if (m_CachedPermissions.TryGetValue(key, out var permissions))
            {
                return CheckPermission(permissions, permission);
            }

            return CheckPermission(await FetchPermissionsAsync(key), permission);
        }

        public void Reset()
        {
            m_Organizations.Clear();
            m_CachedPermissions.Clear();
            m_CachedRoles.Clear();
            m_OrganizationPermissions.Clear();
        }

        bool CheckPermission(Permission[] permissions, string permission)
        {
            if (permissions == null)
            {
                return false;
            }

            return Array.Exists(permissions, p => p.ToString() == permission);
        }

        async Task<Role> FetchRoleAsync(OrganizationProjectPair key)
        {
            var role = await FetchRoleAsyncInternal(key);
            m_CachedRoles[key] = role;
            return role;
        }

        async Task<Role> FetchRoleAsyncInternal(OrganizationProjectPair key)
        {
            var organization = await GetOrganizationAsync(key.OrganizationId);
            if (organization == null)
            {
                return Role.None;
            }

            var results = await organization.ListRolesAsync();
            var orgRoles = results.Select(r => r.ToString().ToLower()).ToHashSet();

            // Asset Manager Admin, Manager, and Owner roles have by default all the permissions
            if (orgRoles.Contains(k_AssetManagerAdmin) || orgRoles.Contains(k_Manager) || orgRoles.Contains(k_Owner))
            {
                return Role.Contributor;
            }

            await foreach (var project in organization.ListProjectsAsync(Range.All))
            {
                if (project.Descriptor.ProjectId.ToString() != key.ProjectId)
                    continue;

                var res = await project.ListRolesAsync();
                var projectRoles = res.Select(r => r.ToString().ToLower()).ToHashSet();

                if (projectRoles.Contains(k_AssetManagerContributor) || projectRoles.Contains(k_Manager) || projectRoles.Contains(k_Owner))
                {
                    return Role.Contributor;
                }

                if (projectRoles.Contains(k_AssetManagerConsumer))
                {
                    return Role.Consumer;
                }

                return Role.Viewer;
            }

            return Role.Viewer;
        }

        async Task<IOrganization> GetOrganizationAsync(string organizationId)
        {
            if (string.IsNullOrEmpty(organizationId))
            {
                return null;
            }

            if(m_Organizations.TryGetValue(organizationId, out var org))
            {
                return org;
            }

            var organizations = m_AssetsProvider.ListOrganizationsAsync(Range.All, CancellationToken.None);
            await foreach (var organization in organizations)
            {
                if (organization.Id.ToString() == organizationId)
                {
                    m_Organizations[organizationId] = organization;
                    break;
                }
            }

            if (m_Organizations[organizationId] == null)
            {
                return null;
            }

            var orgPermissions = await m_Organizations[organizationId].ListPermissionsAsync();
            m_OrganizationPermissions[organizationId] = orgPermissions.ToArray();

            return m_Organizations[organizationId];
        }

        async Task<Permission[]> FetchPermissionsAsync(OrganizationProjectPair key)
        {
            var organization = await GetOrganizationAsync(key.OrganizationId);
            if (organization == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(key.ProjectId))
            {
                m_CachedPermissions[new OrganizationProjectPair(key.OrganizationId, String.Empty)] = m_OrganizationPermissions[key.OrganizationId];
                return m_OrganizationPermissions[key.OrganizationId];
            }

            var projectsAsync = organization.ListProjectsAsync(Range.All);
            if (projectsAsync != null)
            {
                await foreach (var project in projectsAsync)
                {
                    if (project.Descriptor.ProjectId.ToString() == key.ProjectId)
                    {
                        var projectPermissions = await project.ListPermissionsAsync();
                        m_CachedPermissions[key] = m_OrganizationPermissions[key.OrganizationId].Concat(projectPermissions).ToArray();
                        return m_CachedPermissions[key];
                    }
                }
            }

            m_CachedPermissions[key] = m_OrganizationPermissions[key.OrganizationId];
            return m_OrganizationPermissions[key.OrganizationId];
        }

        void SerializeOrganizationPermissions()
        {
            m_SerializableOrganizationIds = m_OrganizationPermissions.Keys.ToArray();

            var permissions = new List<string>();
            var ranges = new List<int>();
            foreach (var orgPermissions in m_OrganizationPermissions.Values)
            {
                ranges.Add(orgPermissions.Length);
                permissions.AddRange(orgPermissions.Select(p => p.ToString()));
            }

            m_SerializableOrganizationPermissionRanges = ranges.ToArray();
            m_SerializableOrganizationPermissions = permissions.ToArray();
        }

        void UnSerializeOrganizationPermissions()
        {
            m_OrganizationPermissions.Clear();
            int rangeIndex = 0;
            for (var i = 0; i < m_SerializableOrganizationIds?.Length; i++)
            {
                var range = m_SerializableOrganizationPermissionRanges[i];
                m_OrganizationPermissions[m_SerializableOrganizationIds[i]] = m_SerializableOrganizationPermissions.Skip(rangeIndex).Take(range).Select(p => new Permission(p)).ToArray();
                rangeIndex += range;
            }
        }

        void SerializePermissions()
        {
            m_SerializablePermissionKeys = m_CachedPermissions.Keys.ToArray();
            var permissions = new List<string>();
            var ranges = new List<int>();
            foreach (var perms in m_CachedPermissions.Values)
            {
                ranges.Add(perms.Length);
                permissions.AddRange(perms.Select(p => p.ToString()));
            }

            m_SerializablePermissionRanges = ranges.ToArray();
            m_SerializablePermissions = permissions.ToArray();
        }

        void UnSerializePermissions()
        {
            m_CachedPermissions.Clear();
            int rangeIndex = 0;
            for (var i = 0; i < m_SerializablePermissionKeys?.Length; i++)
            {
                var range = m_SerializablePermissionRanges[i];
                m_CachedPermissions[m_SerializablePermissionKeys[i]] = m_SerializablePermissions.Skip(rangeIndex).Take(range).Select(p => new Permission(p)).ToArray();
                rangeIndex += range;
            }
        }

        void SerializeRoles()
        {
            m_SerializableRoleKeys = m_CachedRoles?.Select(p => p.Key).ToArray();
            m_SerializableRoles = m_CachedRoles?.Select(p => (int)p.Value).ToArray();
        }

        void UnSerializeRoles()
        {
            m_CachedRoles.Clear();
            for (var i = 0; i < m_SerializableRoleKeys?.Length; i++)
            {
                m_CachedRoles[m_SerializableRoleKeys[i]] = (Role)m_SerializableRoles[i];
            }
        }

        public void OnBeforeSerialize()
        {
            SerializeOrganizationPermissions();
            SerializePermissions();
            SerializeRoles();
        }

        public void OnAfterDeserialize()
        {
            UnSerializeOrganizationPermissions();
            UnSerializePermissions();
            UnSerializeRoles();
        }
    }
}
