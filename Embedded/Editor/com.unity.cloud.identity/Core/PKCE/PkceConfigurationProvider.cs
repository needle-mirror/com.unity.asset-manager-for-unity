// AUTO-GENERATED warning suppression, added by the Cloud SDK embedder. Do not edit; edits are lost on the next embedding.
// This is vendored Unity Cloud SDK source. The embedder re-copies it from the upstream package
// on every pack, so the warning cannot be fixed here, and it has to be suppressed because
// PVP-301-1 ("Package should compile without C# compiler warnings") blocks publishing while a
// shipped csc.rsp is rejected by PVP-24-1 (restricted filenames). Report the underlying issue
// upstream; the suppression list lives in Embedder.k_WarningsToSuppressByPath.
#pragma warning disable CS0618
using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{
    /// <summary>
    /// Handles the access to a <see cref="PkceConfiguration"/>.
    /// </summary>
    class PkceConfigurationProvider : IPkceConfigurationProvider
    {
        readonly IServiceHostResolver m_ServiceHostResolver;

        /// <summary>
        /// Builds a `PkceConfigurationProvider` handles the access to a <see cref="PkceConfiguration"/>.
        /// </summary>
        /// <param name="serviceHostResolver">The service host resolver for the service Url.</param>
        [Obsolete("Use the PkceConfigurationProviderFactory instead.")]
        public PkceConfigurationProvider(IServiceHostResolver serviceHostResolver)
        {
            m_ServiceHostResolver = serviceHostResolver;
        }

        /// <summary>
        /// Creates a task that results in a <see cref="PkceConfiguration"/> when internal update is completed.
        /// </summary>
        /// <returns>
        /// A task that results in a <see cref="PkceConfiguration"/> when internal update is completed.
        /// </returns>
        public async Task<PkceConfiguration> GetPkceConfigurationAsync()
        {
            return await UpdatePkceConfiguration();
        }

        async Task<PkceConfiguration> UpdatePkceConfiguration()
        {
            var pkceConfiguration = CreateConfiguration();
            return await Task.FromResult(pkceConfiguration);
        }

        PkceConfiguration CreateConfiguration()
        {
            var serviceDomainHost =  GetServiceDomainHost();
            var serviceEnvironment = m_ServiceHostResolver?.GetResolvedEnvironment();

            var genesisSubdomain = serviceEnvironment switch
            {
                ServiceEnvironment.Staging => "api-staging",
                ServiceEnvironment.Test => "api-staging",
                _ => "api",
            };

            return new PkceConfiguration
            {
                CacheRefreshToken = true,
                ClientId = new ClientId("unity_cloud"),
                ProxyLoginRedirectRoute = $"{serviceDomainHost}/app-linking/v1/login/redirect/",
                ProxyLoginCompletedRoute = $"{serviceDomainHost}/app-linking/v1/login/completed/",
                ProxySignOutCompletedRoute = $"{serviceDomainHost}/app-linking/v1/signout/completed/",
                LoginUrl = $"https://{genesisSubdomain}.unity.com/v1/oauth2/authorize",
                TokenUrl = $"https://{serviceDomainHost}/app-linking/v1/token",
                RefreshTokenUrl = $"https://{serviceDomainHost}/app-linking/v1/token",
                LogoutUrl = $"https://{serviceDomainHost}/app-linking/v1/token/revoke",
                SignOutUrl = $"https://{genesisSubdomain}.unity.com/v1/oauth2/end-session?post_logout_redirect_uri=",
                UserInfoUrl = $"https://{genesisSubdomain}.unity.com/v1/users/current/openid",
                CustomLoginParams = ""
            };
        }

        string GetServiceDomainHost()
        {
            var serviceAddress = m_ServiceHostResolver?.GetResolvedAddress();
            if (serviceAddress != null)
            {
                var serviceAddressUri = new Uri(serviceAddress);
                return serviceAddressUri.Host;
            }

            return string.Empty;
        }
    }
}
