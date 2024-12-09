using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{
    /// <summary>
    /// An <see cref="IAccessTokenExchanger{T, T}"/> where the T1 input is a DeviceToken and T2 output is a <see cref="UnityServicesToken"/>.
    /// </summary>
    [Obsolete("Deprecated in favor of AccessTokenToUnityServicesTokenExchanger.")]
class DeviceTokenToUnityServicesTokenExchanger : IAccessTokenExchanger<DeviceToken, UnityServicesToken>
    {
        readonly AccessTokenToUnityServicesTokenExchanger m_AccessTokenToUnityServicesTokenExchanger;

        /// <summary>
        /// Provides Unity Services token from DeviceToken
        /// </summary>
        /// <param name="httpClient">An <see cref="IHttpClient"/> instance.</param>
        /// <param name="serviceHostResolver">An <see cref="IServiceHostResolver"/> instance.</param>
        public DeviceTokenToUnityServicesTokenExchanger(IHttpClient httpClient, IServiceHostResolver serviceHostResolver)
        {
            m_AccessTokenToUnityServicesTokenExchanger =
                new AccessTokenToUnityServicesTokenExchanger(httpClient, serviceHostResolver);
        }

        /// <inheritdoc/>
        public async Task<UnityServicesToken> ExchangeAsync(DeviceToken deviceToken)
        {
            return await m_AccessTokenToUnityServicesTokenExchanger.ExchangeAsync(deviceToken.AccessToken);
        }
    }

    /// <summary>
    /// An <see cref="IAccessTokenExchanger{T, T}"/> where the T1 input is a string and T2 output is a <see cref="UnityServicesToken"/>.
    /// </summary>
    class AccessTokenToUnityServicesTokenExchanger : IAccessTokenExchanger<string, UnityServicesToken>
    {
        readonly IHttpClient m_HttpClient;
#if EXPERIMENTAL_WEBGL_PROXY
        readonly IServiceHostResolver m_ServiceHostResolver;
#else
        static readonly string s_BaseUnityApiUrl = ".unity.com";

        readonly string m_UnityApiUrl = ".unity.com";

        readonly TargetClientIdTokenToUnityServicesTokenExchanger m_TargetClientIdTokenToUnityServicesTokenExchanger;
#endif
        /// <summary>
        /// Provides Unity Services token from DeviceToken
        /// </summary>
        /// <param name="httpClient">An <see cref="IHttpClient"/> instance.</param>
        /// <param name="serviceHostResolver">An <see cref="IServiceHostResolver"/> instance.</param>
        public AccessTokenToUnityServicesTokenExchanger(IHttpClient httpClient, IServiceHostResolver serviceHostResolver)
        {
            m_HttpClient = httpClient;
#if EXPERIMENTAL_WEBGL_PROXY
            m_ServiceHostResolver = serviceHostResolver;
#else
            m_TargetClientIdTokenToUnityServicesTokenExchanger =
                new TargetClientIdTokenToUnityServicesTokenExchanger(m_HttpClient, serviceHostResolver);

            var environment = serviceHostResolver?.GetResolvedEnvironment();

            m_UnityApiUrl = environment switch
            {
                ServiceEnvironment.Staging => string.Concat("api-staging", s_BaseUnityApiUrl),
                ServiceEnvironment.Test => string.Concat("api-staging", s_BaseUnityApiUrl),
                _ => string.Concat("api", s_BaseUnityApiUrl)
            };
#endif
        }

        // PKCE access token returned from Genesis requires a first exchange targeting a specific targetClientId
        // before reaching Unity Services exchange endpoint
        async Task<UnityServicesToken> ExchangeGenesisAccessTokenRequestAsync(string genesisAccessToken, string targetClientId = "ads-publisher")
        {
#if EXPERIMENTAL_WEBGL_PROXY
            var url = m_ServiceHostResolver.GetResolvedRequestUri("/app-linking/v1alpha1/token/exchange");

            var exchangeGenesisTokenRequest = new ExchangeGenesisTokenRequest
            {
                accessToken = genesisAccessToken, grantType = "EXCHANGE_ACCESS_TOKEN", targetClientId = targetClientId
            };
            var stringContent = new StringContent(JsonSerialization.Serialize(exchangeGenesisTokenRequest), Encoding.UTF8,
                    "application/json");

            var clientTargetIdTokenResponse = await m_HttpClient.PostAsync(url, stringContent);
            var unityServicesToken = await clientTargetIdTokenResponse.JsonDeserializeAsync<ExchangeTargetClientIdTokenResponse>();

            return new UnityServicesToken{ AccessToken = unityServicesToken.token};
#else
            var exchangeGenesisTokenRequest = new ExchangeGenesisTokenRequest
            {
                accessToken = genesisAccessToken, grantType = "EXCHANGE_ACCESS_TOKEN", targetClientId = targetClientId
            };
            var stringContent = new StringContent(JsonSerialization.Serialize(exchangeGenesisTokenRequest), Encoding.UTF8,
                "application/json");

            var clientTargetIdTokenResponse = await m_HttpClient.PostAsync($"https://{m_UnityApiUrl}/v1/oauth2/token/exchange", stringContent);
            var exchangeGenesisAccessTokenResponse = await clientTargetIdTokenResponse.JsonDeserializeAsync<ExchangeGenesisAccessTokenResponse>();

            return await m_TargetClientIdTokenToUnityServicesTokenExchanger.ExchangeAsync(new TargetClientIdToken
                { token = exchangeGenesisAccessTokenResponse.access_token });
#endif
        }

        /// <inheritdoc/>
        public async Task<UnityServicesToken> ExchangeAsync(string accessToken)
        {
            return await ExchangeGenesisAccessTokenRequestAsync(accessToken);
        }
    }
}
