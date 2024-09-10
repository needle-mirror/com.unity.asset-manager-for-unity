using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.AssetManager.Editor
{
    class ProjectEnabler
    {
        const string k_EnableProjectEndpoint = "/assets/v1/projects/{0}/enable";

        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        public ProjectEnabler(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            m_ServiceHttpClient = serviceHttpClient;
            m_ServiceHostResolver = serviceHostResolver;
        }

        public async Task EnableProjectAsync(string projectId, CancellationToken cancellationToken)
        {
            var requestUri = m_ServiceHostResolver.GetResolvedRequestUri(string.Format(k_EnableProjectEndpoint, projectId));

            await m_ServiceHttpClient.PostAsync(requestUri, new StringContent("", Encoding.UTF8, "application/json"),
                ServiceHttpClientOptions.Default(),  cancellationToken);
        }
    }
}
