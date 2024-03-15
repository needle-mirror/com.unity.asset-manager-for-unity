using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;

namespace Unity.AssetManager.Editor
{
    public class ProjectEnabler
    {
        private const string k_EnableProjectEndpoint = "/assets/v1/projects/{0}/enable";

        readonly IServiceHttpClient m_ServiceHttpClient;
        readonly IServiceHostResolver m_ServiceHostResolver;
        
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
