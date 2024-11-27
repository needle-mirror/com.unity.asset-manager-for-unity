using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IFile
    {
        /// <summary>
        /// The descriptor of the file.
        /// </summary>
        FileDescriptor Descriptor { get; }

        /// <summary>
        /// The description of the file.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The status of the file.
        /// Possible values are:
        /// 'Draft' - The file is created, upload may be in progress.
        /// 'Uploaded' - All bytes have been uploaded and the file is finalized.
        /// </summary>
        string Status { get; }

        /// <summary>
        /// The authoring info of the file.
        /// </summary>
        AuthoringInfo AuthoringInfo { get; }

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        long SizeBytes { get; }

        /// <summary>
        /// The checksum of the file.
        /// </summary>
        string UserChecksum { get; }

        /// <summary>
        /// The tags of the file.
        /// </summary>
        IEnumerable<string> Tags { get; }

        /// <summary>
        /// The system tags of the file.
        /// </summary>
        IEnumerable<string> SystemTags { get; }

        /// <summary>
        /// The datasets the file is linked to.
        /// </summary>
        public IEnumerable<DatasetDescriptor> LinkedDatasets { get; }

        /// <summary>
        /// The metadata of the file.
        /// </summary>
        IMetadataContainer Metadata { get; }

        /// <summary>
        /// The system metadata of the file.
        /// </summary>
        IReadOnlyMetadataContainer SystemMetadata => throw new NotImplementedException();

        /// <summary>
        /// Refreshes the file with the specified fields.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task RefreshAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns a file in the context of the specified dataset.
        /// </summary>
        /// <param name="datasetDescriptor">The descriptor of the dataset. </param>
        /// <returns></returns>
        IFile WithDataset(DatasetDescriptor datasetDescriptor);

        /// <summary>
        /// Returns the datasets that are linked to this file.
        /// </summary>
        /// <param name="range">The range of datasets to return. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is an async enumeration of datasets. </returns>
        IAsyncEnumerable<IDataset> GetLinkedDatasetsAsync(Range range, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the preview URL for the file.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the preview url of the file. </returns>
        Task<Uri> GetPreviewUrlAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns the download URL for the file.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the download url of the file. </returns>
        Task<Uri> GetDownloadUrlAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Downloads the file to the specified stream.
        /// </summary>
        /// <param name="targetStream">The stream in which to download the file. </param>
        /// <param name="progress">The progress of the download. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task DownloadAsync(Stream targetStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the download URL for the file, resized to <paramref name="maxDimension"/> if it is an image.
        /// </summary>
        /// <param name="maxDimension">The maximum width or height of the image (whichever is larger), while maintaining the same aspect ratio. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the download url of the file. </returns>
        Task<Uri> GetResizedImageDownloadUrlAsync(int maxDimension, CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Returns the upload URL for the file.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the upload url of the file. </returns>
        // Task<Uri> GetUploadUrlAsync(CancellationToken cancellationToken)

        /// <summary>
        /// Uploads the file from the specified stream.
        /// </summary>
        /// <param name="sourceStream">The stream from which to upload the file. </param>
        /// <param name="progress">The progress of the upload. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task UploadAsync(Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the file.
        /// </summary>
        /// <param name="fileUpdate">The object containing the necessary information to update the file. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task UpdateAsync(IFileUpdate fileUpdate, CancellationToken cancellationToken);

        /// <summary>
        /// Generates tag suggestions for the image file.
        /// Accepted formats are: JPEG, PNG, GIF, TIFF, and WebP.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is an enumeration of <see cref="GeneratedTag"/>. </returns>
        Task<IEnumerable<GeneratedTag>> GenerateSuggestedTagsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
