using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Implement this interface to transform user facing data like <see cref="IAssetData"/> into service DTOs
    /// </summary>
    partial interface IAssetDataSource
    {
        /// <summary>
        /// Uploads content.
        /// </summary>
        /// <param name="uploadUri">The url to upload the content stream to. </param>
        /// <param name="sourceStream">The stream to the file content</param>
        /// <param name="progress">The progress provider.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result. </returns>
        Task UploadContentAsync(Uri uploadUri, Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Downloads content.
        /// </summary>
        /// <param name="downloadUri">The url from which to download the content stream. </param>
        /// <param name="destinationStream">The destination stream for the file content</param>
        /// <param name="progress">The progress provider.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task with no result.</returns>
        Task DownloadContentAsync(Uri downloadUri, Stream destinationStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Implement this method to get the service request url for a relative path.
        /// </summary>
        /// <param name="relativePath">The relative path of the requested resource.</param>
        /// <returns>A <see cref="Uri"/>.</returns>
        Uri GetServiceRequestUrl(string relativePath);
    }
}
