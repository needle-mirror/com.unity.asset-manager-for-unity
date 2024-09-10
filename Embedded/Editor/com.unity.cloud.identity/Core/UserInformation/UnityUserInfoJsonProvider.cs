using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{
    internal interface IUnityUserInfoJsonProvider
    {
        public Task<UnityUserInfoJson> GetUnityUserInfoJsonAsync();
    }

    internal class UnityUserInfoJsonProvider : IUnityUserInfoJsonProvider
    {
        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        readonly GetRequestResponseCache<UnityUserInfoJson> m_GetUnityUserOrganizationRequestResponseCache;

        public UnityUserInfoJsonProvider(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            m_ServiceHostResolver = serviceHostResolver;
            m_ServiceHttpClient = serviceHttpClient;

            m_GetUnityUserOrganizationRequestResponseCache = new GetRequestResponseCache<UnityUserInfoJson>(60);
        }

        public async Task<UnityUserInfoJson> GetUnityUserInfoJsonAsync()
        {
#if EXPERIMENTAL_WEBGL_PROXY
            var coreApiRequestPath = "api/unity/v1/users/me/organizations";
            var url = m_ServiceHostResolver.GetResolvedRequestUri("/app-linking/v1alpha1/core");
            UnityUserInfoJson userInfoJson;
            if (m_GetUnityUserOrganizationRequestResponseCache.TryGetRequestResponseFromCache(coreApiRequestPath, out UnityUserInfoJson value))
            {
                userInfoJson = value;
            }
            else
            {
                var coreApiRequest = new CoreApiRequestParams
                {
                    Path = coreApiRequestPath,
                    Method = "Get",
                };
                var contentBody = new StringContent(JsonSerialization.Serialize(coreApiRequest), Encoding.UTF8, "application/json");
                var response = await m_ServiceHttpClient.PostAsync(url, contentBody);

                var deserializedResponse = await response.JsonDeserializeAsync<UnityUserInfoJson>();
                userInfoJson = m_GetUnityUserOrganizationRequestResponseCache.AddGetRequestResponseToCache(coreApiRequestPath, deserializedResponse);
            }
            return userInfoJson;
#else
            var internalServiceHostResolver = m_ServiceHostResolver.CreateCopyWithDomainResolverOverride(new UnityServicesDomainResolver(true));
            var url = internalServiceHostResolver.GetResolvedRequestUri("/api/unity/v1/users/me/organizations");
            UnityUserInfoJson userInfoJson;
            if (m_GetUnityUserOrganizationRequestResponseCache.TryGetRequestResponseFromCache(url, out UnityUserInfoJson value))
            {
                userInfoJson = value;
            }
            else
            {
                var response = await m_ServiceHttpClient.GetAsync(url);
                var deserializedResponse = await response.JsonDeserializeAsync<UnityUserInfoJson>();
                userInfoJson = m_GetUnityUserOrganizationRequestResponseCache.AddGetRequestResponseToCache(url, deserializedResponse);
            }
            return userInfoJson;
#endif
        }
    }

    class CoreApiRequestParams
    {
        public string Path { get; set; }
        public string Body { get; set; }
        public string Method { get; set; }
    }

}
