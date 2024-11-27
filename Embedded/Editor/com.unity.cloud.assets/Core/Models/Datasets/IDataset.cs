using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IDataset
    {
        /// <summary>
        /// The descriptor of the dataset.
        /// </summary>
        DatasetDescriptor Descriptor { get; }

        /// <summary>
        /// The name of the dataset.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// A description of the dataset.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The user tags of the dataset.
        /// </summary>
        IEnumerable<string> Tags { get; }

        /// <summary>
        /// The system tags of the dataset.
        /// </summary>
        IEnumerable<string> SystemTags { get; }

        /// <summary>
        /// The status of the dataset.
        /// </summary>
        string Status { get; }

        /// <summary>
        /// The authoring info of the dataset.
        /// </summary>
        AuthoringInfo AuthoringInfo { get; }

        /// <summary>
        /// The order of the files in the dataset.
        /// </summary>
        IEnumerable<string> FileOrder { get; }

        /// <summary>
        /// Indicates whether the dataset is visible or not.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// The searchable metadata of the dataset.
        /// </summary>
        IMetadataContainer Metadata { get; }

        /// <summary>
        /// The system metadata of the dataset.
        /// </summary>
        IReadOnlyMetadataContainer SystemMetadata => throw new NotImplementedException();

        /// <summary>
        /// Refreshes the dataset with the specified fields.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task RefreshAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Updates the dataset.
        /// </summary>
        /// <param name="datasetUpdate">The object containing the necessary information to update the dataset. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task UpdateAsync(IDatasetUpdate datasetUpdate, CancellationToken cancellationToken);

        /// <summary>
        /// Creates and uploads a new file to the dataset.
        /// </summary>
        /// <param name="fileCreation">The object containing the necessary information to create a new file. </param>
        /// <param name="sourceStream">The stream from which to upload the new file. </param>
        /// <param name="progress">The progress of the upload. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is a newly created file. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task<IFile> UploadFileAsync(IFileCreation fileCreation, Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Adds a file from the specified dataset to the current dataset.
        /// </summary>
        /// <param name="filePath">The path to the file. </param>
        /// <param name="sourceDatasetId">The id of the source dataset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task whose result is the linked file. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task<IFile> AddExistingFileAsync(string filePath, DatasetId sourceDatasetId, CancellationToken cancellationToken);

        /// <summary>
        /// Removes a file from the dataset.
        /// </summary>
        /// <param name="filePath">The path to the file. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task RemoveFileAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the files in the dataset.
        /// </summary>
        /// <param name="range">The range of files to return. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is an async enumeration of file. </returns>
        IAsyncEnumerable<IFile> ListFilesAsync(Range range, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a file in the dataset.
        /// </summary>
        /// <param name="filePath">The path to the file. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the file at <paramref name="filePath"/>. </returns>
        Task<IFile> GetFileAsync(string filePath, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the download URLs for the files of the dataset.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the download URLs for the dataset. </returns>
        Task<IReadOnlyDictionary<string, Uri>> GetDownloadUrlsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Returns the download URL of the file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        Uri GetFileUrl(string filePath);

        /// <summary>
        /// Start a transformation on the dataset.
        /// </summary>
        /// <param name="transformationCreation">The object containing the information necessary to start a transformation. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <exception cref="InvalidArgumentException">If this version of the asset is frozen, because it cannot be modified. </exception>
        /// <remarks>Can only be called if the version of the asset is unfrozen. </remarks>
        Task<ITransformation> StartTransformationAsync(ITransformationCreation transformationCreation, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the transformations on the dataset.
        /// </summary>
        /// <param name="range">The range of results to return. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is an async enumeration of <see cref="ITransformation"/>. </returns>
        IAsyncEnumerable<ITransformation> ListTransformationsAsync(Range range, CancellationToken cancellationToken);

        /// <summary>
        /// Get specified transformation on the dataset
        /// </summary>
        /// <param name="transformationId">The id of the transformation to get. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        Task<ITransformation> GetTransformationAsync(TransformationId transformationId, CancellationToken cancellationToken);
    }
}
