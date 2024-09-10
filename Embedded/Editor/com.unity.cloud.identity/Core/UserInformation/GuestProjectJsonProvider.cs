using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.IdentityEmbedded
{
    internal interface IGuestProjectJsonProvider
    {
        public IAsyncEnumerable<ProjectJson> GetGuestProjectsAsync(Range range, CancellationToken cancellationToken);
    }

    internal class GuestProjectJsonProvider : IGuestProjectJsonProvider
    {
        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        private readonly IUnityUserInfoJsonProvider m_UnityUserInfoJsonProvider;

        readonly GetRequestResponseCache<RangeResultsJson<ProjectJson>> m_GetGuestProjectRequestResponseCache;

        public GuestProjectJsonProvider(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver, IUnityUserInfoJsonProvider unityUserInfoJsonProvider = null)
        {
            m_ServiceHostResolver = serviceHostResolver;
            m_ServiceHttpClient = serviceHttpClient;
            m_UnityUserInfoJsonProvider = unityUserInfoJsonProvider;
            m_GetGuestProjectRequestResponseCache = new GetRequestResponseCache<RangeResultsJson<ProjectJson>>(60);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<ProjectJson> GetGuestProjectsAsync(Range range,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var userInfoJson = await m_UnityUserInfoJsonProvider.GetUnityUserInfoJsonAsync();
            var rangeRequest = new RangeRequest<ProjectJson>(GetGuestProjects, 1000);
            var requestBasePath = $"api/unity/legacy/v1/users/{userInfoJson.GenesisId}/guest-projects";
            var results = rangeRequest.Execute(requestBasePath, range, cancellationToken);
            await foreach (var projectJson in results)
            {
                yield return projectJson;
            }
        }

        async Task<RangeResultsJson<ProjectJson>> GetGuestProjects(string rangeRequestPath, CancellationToken cancellationToken)
        {
#if EXPERIMENTAL_WEBGL_PROXY
            var url = m_ServiceHostResolver.GetResolvedRequestUri("/app-linking/v1alpha1/core");
            if (m_GetGuestProjectRequestResponseCache.TryGetRequestResponseFromCache(rangeRequestPath, out RangeResultsJson<ProjectJson> value))
            {
                return value;
            }

            var coreApiRequest = new CoreApiRequestParams
            {
                Path = rangeRequestPath,
                Method = "Get",
            };
            var content = new StringContent(JsonSerialization.Serialize(coreApiRequest), Encoding.UTF8, "application/json");
            var response = await m_ServiceHttpClient.PostAsync(url, content, cancellationToken: cancellationToken);

            var deserializedResponse = await response.JsonDeserializeAsync<RangeResultsJson<ProjectJson>>();
            return m_GetGuestProjectRequestResponseCache.AddGetRequestResponseToCache(rangeRequestPath, deserializedResponse);
#else
            var internalServiceHostResolver = m_ServiceHostResolver.CreateCopyWithDomainResolverOverride(new UnityServicesDomainResolver(true));
            var url = internalServiceHostResolver.GetResolvedRequestUri($"/{rangeRequestPath}");
            if (m_GetGuestProjectRequestResponseCache.TryGetRequestResponseFromCache(url, out RangeResultsJson<ProjectJson> value))
            {
                return value;
            }
            var response = await m_ServiceHttpClient.GetAsync(url, cancellationToken: cancellationToken);
            var deserializedResponse = await response.JsonDeserializeAsync<RangeResultsJson<ProjectJson>>();
            return m_GetGuestProjectRequestResponseCache.AddGetRequestResponseToCache(url, deserializedResponse);
#endif
        }
    }
}
