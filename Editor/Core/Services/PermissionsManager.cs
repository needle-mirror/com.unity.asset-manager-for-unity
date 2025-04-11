using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using Unity.Cloud.IdentityEmbedded;
using Unity.Cloud.IdentityEmbedded.Editor;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    enum AuthenticationState
    {
        /// <summary>
        /// Indicates the application is waiting for the completion of the initialization.
        /// </summary>
        AwaitingInitialization,
        /// <summary>
        /// Indicates when an authenticated user is logged in.
        /// </summary>
        LoggedIn,
        /// <summary>
        /// Indicates no authenticated user is available.
        /// </summary>
        LoggedOut,
        /// <summary>
        /// Indicates the application is waiting for the completion of a login operation.
        /// </summary>
        AwaitingLogin,
        /// <summary>
        /// Indicates the application is waiting for the completion of a logout operation.
        /// </summary>
        AwaitingLogout
    };

    enum Role
    {
        None,
        Contributor,
        Consumer,
        Viewer
    }

    interface IPermissionsManager : IService
    {
        event Action<AuthenticationState> AuthenticationStateChanged;
        AuthenticationState AuthenticationState { get; }

        Task<Role> GetRoleAsync(string organizationId, string projectId);
        Task<bool> CheckPermissionAsync(string organizationId, string projectId, string permission);

        void Reset();
    }

    [Serializable]
    class OrganizationProjectPair : IEquatable<OrganizationProjectPair>
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
            m_ProjectId = string.IsNullOrEmpty(projectId) ? string.Empty : projectId;
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

            return Equals((OrganizationProjectPair) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OrganizationId, ProjectId);
        }
    }

    [Serializable]
    class PermissionsManager : BaseSdkService, IPermissionsManager, ISerializationCallbackReceiver
    {
        public override Type RegistrationType => typeof(IPermissionsManager);

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

        readonly Dictionary<string, Permission[]> m_OrganizationPermissions = new();
        readonly Dictionary<OrganizationProjectPair, Role> m_CachedRoles = new();
        readonly Dictionary<OrganizationProjectPair, Permission[]> m_CachedPermissions = new();
        readonly Dictionary<string, IOrganization> m_Organizations = new();

        static readonly string k_AssetManagerAdmin = "asset manager admin";
        static readonly string k_Manager = Unity.Cloud.CommonEmbedded.Role.Manager.ToString();
        static readonly string k_Owner = Unity.Cloud.CommonEmbedded.Role.Owner.ToString();
        static readonly string k_AssetManagerContributor = "asset manager contributor";
        static readonly string k_AssetManagerConsumer = "asset manager consumer";

        public PermissionsManager() { }

        /// <inheritdoc />
        internal PermissionsManager(SdkServiceOverride sdkServiceOverride)
            : base(sdkServiceOverride) { }

        public event Action<AuthenticationState> AuthenticationStateChanged;

        public AuthenticationState AuthenticationState => Map(GetAuthenticationState());

        public override void OnEnable()
        {
            // AMECO-3518
            ResetServiceAuthorizerAwaitingExchangeOperationState();

            RegisterOnAuthenticationStateChanged(OnAuthenticationStateChanged);
            InitAuthenticatedServices();
        }

        // AMECO-3518 Hack to avoid infinite "Awaiting Unity Hub User Session" loop that can happen after two consecutive domain reloads
        // Delete this once the fix is inside Identity.
        static void ResetServiceAuthorizerAwaitingExchangeOperationState()
        {
            var unityEditorServiceAuthorizerType = typeof(UnityEditorServiceAuthorizer);
            var field = unityEditorServiceAuthorizerType.GetField("m_AwaitingExchangeOperation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(UnityEditorServiceAuthorizer.instance, false);
        }

        public override void OnDisable()
        {
            UnregisterOnAuthenticationStateChanged(OnAuthenticationStateChanged);
        }

        void OnAuthenticationStateChanged()
        {
            AuthenticationStateChanged?.Invoke(AuthenticationState);
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
            return permissions != null && Array.Exists(permissions, p => p.ToString() == permission);
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

            var organizationKey = new OrganizationProjectPair(key.OrganizationId, string.Empty);
            if (!m_CachedRoles.TryGetValue(organizationKey, out var orgRole))
            {
                orgRole = Role.Viewer;

                var results = await organization.ListRolesAsync();
                var orgRoles = results.Select(r => r.ToString().ToLower()).ToHashSet();

                // Asset Manager Admin, Manager, and Owner roles have by default all the permissions
                if (orgRoles.Contains(k_AssetManagerAdmin) || orgRoles.Contains(k_Manager) || orgRoles.Contains(k_Owner))
                {
                    orgRole = Role.Contributor;
                }

                m_CachedRoles[organizationKey] = orgRole;
            }

            if (orgRole == Role.Contributor || string.IsNullOrEmpty(key.ProjectId))
            {
                return orgRole;
            }

            await foreach (var project in organization.ListProjectsAsync(Range.All))
            {
                if (project.Descriptor.ProjectId.ToString() != key.ProjectId)
                    continue;

                var res = await project.ListRolesAsync();
                var projectRoles = res.Select(r => r.ToString().ToLower()).ToHashSet();

                if (projectRoles.Contains(k_AssetManagerContributor) || projectRoles.Contains(k_Manager) ||
                    projectRoles.Contains(k_Owner))
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

            if (m_Organizations.TryGetValue(organizationId, out var org))
            {
                return org;
            }

            var organization = await GetOrganizationAsync_Internal(organizationId);

            m_Organizations[organizationId] = organization;

            IEnumerable<Permission> orgPermissions = null;
            if (organization != null)
            {
                orgPermissions = await organization.ListPermissionsAsync();
            }
            m_OrganizationPermissions[organizationId] = orgPermissions?.ToArray() ?? Array.Empty<Permission>();

            return organization;
        }

        async Task<IOrganization> GetOrganizationAsync_Internal(string organizationId)
        {
            try
            {
                var organizations = OrganizationRepository.ListOrganizationsAsync(Range.All, CancellationToken.None);
                await foreach (var organization in organizations)
                {
                    if (organization.Id.ToString() == organizationId)
                    {
                        return organization;
                    }
                }
            }
            catch (Exception)
            {
                // Patch for fixing UnityEditorCloudServiceAuthorizer not serializing properly.
                // This case only shows up in 2021 because organization is fetched earlier than other versions
                // Delete this code once Identity get bumped beyond 1.2.0-exp.1
            }

            return null;
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
                m_CachedPermissions[new OrganizationProjectPair(key.OrganizationId, string.Empty)] =
                    m_OrganizationPermissions[key.OrganizationId];
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
                        m_CachedPermissions[key] = m_OrganizationPermissions[key.OrganizationId]
                            .Concat(projectPermissions).ToArray();
                        return m_CachedPermissions[key];
                    }
                }
            }

            m_CachedPermissions[key] = m_OrganizationPermissions[key.OrganizationId];
            return m_OrganizationPermissions[key.OrganizationId];
        }

        static AuthenticationState Map(Unity.Cloud.IdentityEmbedded.AuthenticationState authenticationState) =>
            authenticationState switch
            {
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingInitialization => AuthenticationState
                    .AwaitingInitialization,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingLogin => AuthenticationState.AwaitingLogin,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.LoggedIn => AuthenticationState.LoggedIn,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingLogout => AuthenticationState.AwaitingLogout,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.LoggedOut => AuthenticationState.LoggedOut,
                _ => throw new ArgumentOutOfRangeException(nameof(authenticationState), authenticationState, null)
            };

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
                m_OrganizationPermissions[m_SerializableOrganizationIds[i]] = m_SerializableOrganizationPermissions
                    .Skip(rangeIndex).Take(range).Select(p => new Permission(p)).ToArray();
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
                m_CachedPermissions[m_SerializablePermissionKeys[i]] = m_SerializablePermissions.Skip(rangeIndex)
                    .Take(range).Select(p => new Permission(p)).ToArray();
                rangeIndex += range;
            }
        }

        void SerializeRoles()
        {
            m_SerializableRoleKeys = m_CachedRoles?.Select(p => p.Key).ToArray();
            m_SerializableRoles = m_CachedRoles?.Select(p => (int) p.Value).ToArray();
        }

        void UnSerializeRoles()
        {
            m_CachedRoles.Clear();
            for (var i = 0; i < m_SerializableRoleKeys?.Length; i++)
            {
                m_CachedRoles[m_SerializableRoleKeys[i]] = (Role) m_SerializableRoles[i];
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
