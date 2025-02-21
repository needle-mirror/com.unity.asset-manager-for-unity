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
        readonly string m_UserId;
        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        readonly GetRequestResponseCache<UnityUserInfoJson> m_GetUnityUserOrganizationRequestResponseCache;

        public UnityUserInfoJsonProvider(string userId, IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            m_UserId = userId;
            m_ServiceHostResolver = serviceHostResolver;
            m_ServiceHttpClient = serviceHttpClient;

            m_GetUnityUserOrganizationRequestResponseCache = new GetRequestResponseCache<UnityUserInfoJson>(60);
        }

        public async Task<UnityUserInfoJson> GetUnityUserInfoJsonAsync()
        {
            var internalServiceHostResolver = m_ServiceHostResolver.CreateCopyWithDomainResolverOverride(new UnityServicesDomainResolver(true));
            var url = internalServiceHostResolver.GetResolvedRequestUri($"/api/unity/legacy/v1/users/{m_UserId}/organizations");
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
        }
    }

    class CoreApiRequestParams
    {
        public string Path { get; set; }
        public string Body { get; set; }
        public string Method { get; set; }
    }

}
