using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class AssetResultDto
    {
        public string assetVersion;
    }
    
    [Serializable]
    class AssetSearchResultDto
    {
        public AssetResultDto[] results;
    }
   
    /// <summary>
    /// Temporary class that directly access the asset version search endpoint. To be replaced
    /// by proper IAssetRepository implementation when available.
    /// </summary>
    public class AssetVersionsSearch
    {
        const string k_Endpoint = "/assets/v1/projects/{0}/assets/{1}/versions/search";
        const string k_GetFirstVersionBody = @"{"                                          +
                                             @"   ""includeFields"": [],"                  +
                                             @"   ""pagination"": {"                       +
                                             @"      ""limit"": 1,"                        +
                                             @"      ""sortingField"": ""versionNumber""," +
                                             @"      ""sortingOrder"": ""Ascending"""      +
                                             @"   }"                                       +
                                             @"}"                                          ;
        
        readonly IServiceHostResolver m_ServiceHostResolver;
        readonly IServiceHttpClient m_ServiceHttpClient;

        public AssetVersionsSearch(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            m_ServiceHostResolver = serviceHostResolver;
            m_ServiceHttpClient = serviceHttpClient;
        }

        public async Task<string> GetFirstVersionAsync(ProjectId projectId, AssetId assetId, CancellationToken token)
        {
            if (m_ServiceHostResolver == null || m_ServiceHttpClient == null)
                return string.Empty;
            
            var requestUri = m_ServiceHostResolver.GetResolvedRequestUri(string.Format(k_Endpoint, projectId.ToString(), assetId.ToString()));
            var requestContent = new StringContent(k_GetFirstVersionBody, Encoding.UTF8, "application/json");
            var response = await m_ServiceHttpClient.PostAsync(requestUri, requestContent, ServiceHttpClientOptions.Default(), token);
            var responseContent = await response.GetContentAsString();
            var responseContentDto = JsonSerialization.Deserialize<AssetSearchResultDto>(responseContent);
            return responseContentDto?.results?.Length > 0 ? responseContentDto.results[0].assetVersion : string.Empty;
        }
    }
}
