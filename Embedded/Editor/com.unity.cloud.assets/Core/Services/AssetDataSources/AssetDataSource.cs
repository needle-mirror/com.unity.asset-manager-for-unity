using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    partial class AssetDataSource : IAssetDataSource
    {
        const string k_PublicApiPath = "/assets/v1";

        static readonly UCLogger k_Logger = LoggerProvider.GetLogger<AssetDataSource>();

        readonly IServiceHttpClient m_ServiceHttpClient;
        readonly IServiceHostResolver m_PublicServiceHostResolver;

        internal AssetDataSource(IServiceHttpClient serviceHttpClient, IServiceHostResolver serviceHostResolver)
        {
            if (serviceHostResolver is ServiceHostResolver unityServiceHostResolver &&
                unityServiceHostResolver.GetResolvedEnvironment() == ServiceEnvironment.Test)
            {
                var headers = new Dictionary<string, string>
                {
                    {"x-backend-host", "https://api.fd.amc.test.transformation.unity.com"}
                };
                serviceHttpClient = new ServiceHttpClientHeaderModifier(serviceHttpClient, headers);
            }

            m_ServiceHttpClient = serviceHttpClient;
            m_PublicServiceHostResolver = serviceHostResolver;
        }

        string GetPublicRequestUri(ApiRequest request)
        {
            return m_PublicServiceHostResolver.GetResolvedRequestUri(request.ConstructUrl(k_PublicApiPath));
        }

        /// <inheritdoc />
        public async Task UploadContentAsync(Uri uploadUri, Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken)
        {
            const string blobTypeHeaderKey = "X-Ms-Blob-Type";
            const string blobTypeHeaderValue = "BlockBlob";

            cancellationToken.ThrowIfCancellationRequested();

            if (uploadUri == null)
            {
                throw new InvalidUrlException("Upload url is null or empty");
            }

            using var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.Method = HttpMethod.Put;
            httpRequestMessage.RequestUri = uploadUri;
            httpRequestMessage.Content = new StreamContent(sourceStream);

            httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            httpRequestMessage.Headers.Add(blobTypeHeaderKey, blobTypeHeaderValue);

            using var response = await m_ServiceHttpClient
                .SendAsync(httpRequestMessage, ServiceHttpClientOptions.SkipDefaultAuthenticationOption(), HttpCompletionOption.ResponseContentRead, progress, cancellationToken);

            try
            {
                _ = response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new UploadFailedException($"Upload of content stream for file id {uploadUri} failed.");
            }
        }

        /// <inheritdoc />
        public async Task DownloadContentAsync(Uri downloadUri, Stream destinationStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (downloadUri == null)
            {
                throw new InvalidUrlException("Download url is null or empty");
            }

            using var httpRequestMessage = new HttpRequestMessage();
            httpRequestMessage.Method = HttpMethod.Get;
            httpRequestMessage.RequestUri = downloadUri;

            using var response = await m_ServiceHttpClient.SendAsync(httpRequestMessage, ServiceHttpClientOptions.SkipDefaultAuthenticationOption(), HttpCompletionOption.ResponseContentRead, progress, cancellationToken);
            response.EnsureSuccessStatusCode();

            var source = await response.Content.ReadAsStreamAsync();

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await source.CopyToAsync(destinationStream, cancellationToken);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Could not write to {nameof(destinationStream)}", nameof(destinationStream), e);
            }
            finally
            {
                await source.DisposeAsync();
            }
        }

        /// <inheritdoc />
        public Uri GetServiceRequestUrl(string relativePath)
        {
            return new Uri(m_PublicServiceHostResolver.GetResolvedRequestUri(relativePath));
        }

        static Uri GetEscapedUri(string url)
        {
            var uri = new Uri(url);

            // Using the AbsoluteUri of an existing Uri ensures that the url is properly escaped.
            return new Uri(uri.AbsoluteUri);
        }
    }
}
