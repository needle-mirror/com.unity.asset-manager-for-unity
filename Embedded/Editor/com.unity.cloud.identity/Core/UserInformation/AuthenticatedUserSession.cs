using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{
    /// <summary>
    /// An <see cref="IUserInfoProvider"/> and <see cref="IOrganizationRepository"/> implementation that exposes the current logged in user's information and the list of organizations and projects it has access to.
    /// </summary>
    internal class AuthenticatedUserSession : IUserInfoProvider, IOrganizationRepository
    {

        static readonly UCLogger s_Logger = LoggerProvider.GetLogger<AuthenticatedUserSession>();

        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        readonly IOrganizationProjectsJsonProvider m_OrganizationProjectsJsonProvider;
        IEntityRoleProvider m_EntityRoleProvider;

        readonly IUnityUserInfoJsonProvider m_UnityUserInfoJsonProvider;
        readonly IGuestProjectJsonProvider m_GuestProjectJsonProvider;
        readonly IOrganizationJsonProvider m_OrganizationJsonProvider;
        readonly GetRequestResponseCache<OrganizationJson> m_GetOrganizationRequestResponseCache;

        /// <summary>
        /// Builds an <see cref="AuthenticatedUserSession"/> class.
        /// </summary>
        /// <param name="serviceHttpClient">A <see cref="IServiceHttpClient"/> implementation.</param>
        /// <param name="serviceHostResolver">A <see cref="IServiceHostResolver"/> instance.</param>
        /// <param name="unityUserInfoJsonProvider">An optional <see cref="IUnityUserInfoJsonProvider"/> instance.</param>
        /// <param name="guestProjectJsonProvider">An optional <see cref="IGuestProjectJsonProvider"/> instance.</param>
        /// <param name="organizationJsonProvider">An optional <see cref="IOrganizationJsonProvider"/> instance.</param>
        public AuthenticatedUserSession(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver, IUnityUserInfoJsonProvider unityUserInfoJsonProvider = null, IGuestProjectJsonProvider guestProjectJsonProvider = null, IOrganizationJsonProvider organizationJsonProvider = null)
        {
            m_ServiceHostResolver = serviceHostResolver;
            m_ServiceHttpClient = serviceHttpClient;
            m_UnityUserInfoJsonProvider = unityUserInfoJsonProvider ?? new UnityUserInfoJsonProvider(m_ServiceHttpClient, m_ServiceHostResolver);
            m_GuestProjectJsonProvider = guestProjectJsonProvider ??
                                         new GuestProjectJsonProvider(m_ServiceHttpClient, m_ServiceHostResolver,
                                             m_UnityUserInfoJsonProvider);
            m_OrganizationJsonProvider = organizationJsonProvider;
            m_OrganizationProjectsJsonProvider = new OrganizationProjectsJsonProvider(m_ServiceHttpClient, m_ServiceHostResolver);
            m_GetOrganizationRequestResponseCache = new GetRequestResponseCache<OrganizationJson>(60);
        }

        async Task<UnityUserInfoJson> GetUnityUserInfoJsonAsync()
        {
            var userInfoJson = await m_UnityUserInfoJsonProvider.GetUnityUserInfoJsonAsync();
            m_EntityRoleProvider ??= new AuthenticatorRoleProvider(userInfoJson.GenesisId, m_ServiceHttpClient, m_ServiceHostResolver);
            return userInfoJson;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var allOrganizations = new List<IOrganization>();
            var userInfoJson = await GetUnityUserInfoJsonAsync();
            var organizations = userInfoJson.Organizations.Select(userOrgJson => new Organization(userOrgJson, m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider)).Cast<IOrganization>().ToList();
            foreach (var organization in organizations)
            {
                allOrganizations.Add(await Task.FromResult(organization));
            }
            var organizationsAsyncEnumerable = ListGuestOrganizationsAsync(cancellationToken);
            await foreach (var organization in organizationsAsyncEnumerable)
            {
                allOrganizations.Add(organization);
            }
            range = range.NormalizeRange(allOrganizations.Count);
            allOrganizations = allOrganizations.GetRange(range.Start.Value, range.End.Value - range.Start.Value);
            foreach (var organization in allOrganizations)
            {
                yield return organization;
            }
        }

        /// <inheritdoc/>
        public async Task<IOrganization> GetOrganizationAsync(OrganizationId organizationId)
        {
            return await InternalGetOrganizationAsync(organizationId);
        }

        async Task<IOrganization> InternalGetOrganizationAsync(OrganizationId organizationId)
        {
            if (m_OrganizationJsonProvider != null)
            {
                return new Organization(await m_OrganizationJsonProvider.GetOrganizationJsonAsync(organizationId), m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider);
            }
#if EXPERIMENTAL_WEBGL_PROXY
            var coreApiRequestPath = $"api/unity/legacy/v1/organizations/{organizationId}";
            var url = m_ServiceHostResolver.GetResolvedRequestUri("/app-linking/v1alpha1/core");
            if (m_GetOrganizationRequestResponseCache.TryGetRequestResponseFromCache(coreApiRequestPath, out OrganizationJson value))
            {
                return new Organization(value, m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider);
            }

            var coreApiRequest = new CoreApiRequestParams
            {
                Path = coreApiRequestPath,
                Method = "Get",
            };
            var content = new StringContent(JsonSerialization.Serialize(coreApiRequest), Encoding.UTF8, "application/json");

            var response = await m_ServiceHttpClient.PostAsync(url, content);
            var deserializedResponse = await response.JsonDeserializeAsync<OrganizationJson>();
            var organizationJson = m_GetOrganizationRequestResponseCache.AddGetRequestResponseToCache(coreApiRequestPath, deserializedResponse);
            return new Organization(organizationJson, m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider);
#else
            var internalServiceHostResolver = m_ServiceHostResolver.CreateCopyWithDomainResolverOverride(new UnityServicesDomainResolver(true));
            var url = internalServiceHostResolver.GetResolvedRequestUri($"/api/unity/legacy/v1/organizations/{organizationId}");
            if (m_GetOrganizationRequestResponseCache.TryGetRequestResponseFromCache(url, out OrganizationJson value))
            {
                return new Organization(value, m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider);
            }
            var response = await m_ServiceHttpClient.GetAsync(url);
            var deserializedResponse = await response.JsonDeserializeAsync<OrganizationJson>();
            var organizationJson = m_GetOrganizationRequestResponseCache.AddGetRequestResponseToCache(url, deserializedResponse);
            return new Organization(organizationJson, m_ServiceHttpClient, m_ServiceHostResolver, m_OrganizationProjectsJsonProvider, m_EntityRoleProvider, m_GuestProjectJsonProvider);
#endif
        }

        async IAsyncEnumerable<IProject> GetGuestProjectsAsync(Range range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var guestProjectJsonAsyncEnumerable = m_GuestProjectJsonProvider.GetGuestProjectsAsync(range, cancellationToken);
            await foreach (var guestProjectJson in guestProjectJsonAsyncEnumerable)
            {
                yield return new Project(guestProjectJson, m_ServiceHttpClient, m_ServiceHostResolver, m_EntityRoleProvider);
            }
        }

        async IAsyncEnumerable<IOrganization> ListGuestOrganizationsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var guestOrganizationIds = new HashSet<OrganizationId>();
            var guestProjectsAsyncEnumerable = GetGuestProjectsAsync(Range.All, cancellationToken);
            await foreach (var guestProject in guestProjectsAsyncEnumerable)
            {
                var guestOrganizationId = guestProject.Descriptor.OrganizationId;
                if (guestOrganizationIds.Add(guestOrganizationId))
                {
                    yield return await InternalGetOrganizationAsync(guestOrganizationId);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<IUserInfo> GetUserInfoAsync()
        {
            return new UserInfo(await GetUnityUserInfoJsonAsync());
        }
    }
}
