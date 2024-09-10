using System.Reflection;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static class AssetRepositoryFactory
    {
        public static IAssetRepository Create(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            serviceHttpClient = serviceHttpClient.WithApiSourceHeadersFromAssembly(Assembly.GetExecutingAssembly());
            var dataSource = new AssetDataSource(serviceHttpClient, serviceHostResolver);
            return new AssetRepository(dataSource);
        }
    }
}
