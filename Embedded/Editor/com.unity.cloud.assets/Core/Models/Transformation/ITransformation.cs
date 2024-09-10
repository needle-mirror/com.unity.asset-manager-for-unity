using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    interface ITransformation
    {
        /// <summary>
        /// The descriptor of the transformation.
        /// </summary>
        TransformationDescriptor Descriptor { get; }

        /// <summary>
        /// The ID of the Dataset on which the transformation is applied
        /// </summary>
        DatasetId InputDatasetId => Descriptor.DatasetId;

        /// <summary>
        /// The ID of the Dataset that will be created by the transformation if any
        /// </summary>
        DatasetId OutputDatasetId { get; }

        /// <summary>
        /// The ID of the Dataset that will be linked to the transformation if any
        /// </summary>
        DatasetId LinkDatasetId { get; }

        /// <summary>
        /// The files on which the transformation is applied
        /// </summary>
        IEnumerable<string> InputFiles { get; }

        /// <summary>
        /// The type of transformation
        /// </summary>
        WorkflowType WorkflowType { get; }

        /// <summary>
        /// The status of the transformation
        /// </summary>
        TransformationStatus Status { get; }

        /// <summary>
        /// If the transformation failed, this will contain the associated error message
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// The progress of the transformation. This is a value between 0 and 100.
        /// </summary>
        int Progress => 0;

        /// <summary>
        /// The datetime at which the transformation was created
        /// </summary>
        DateTime CreatedOn { get; }

        /// <summary>
        /// The datetime at which the transformation was last updated
        /// </summary>
        DateTime UpdatedAt { get; }

        /// <summary>
        /// The datetime at which the transformation was started
        /// </summary>
        DateTime StartedAt { get; }

        /// <summary>
        /// The user ID of the user who started the transformation
        /// </summary>
        UserId UserId => throw new NotImplementedException();

        /// <summary>
        /// The job ID of the transformation
        /// </summary>
        string JobId { get; }

        /// <summary>
        /// Refreshes the transformation properties.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task RefreshAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Cancels the transformation.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task TerminateAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
