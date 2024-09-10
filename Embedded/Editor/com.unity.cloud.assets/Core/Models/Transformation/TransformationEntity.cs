using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    class TransformationEntity : ITransformation
    {
        readonly IAssetDataSource m_DataSource;

        /// <inheritdoc />
        public TransformationDescriptor Descriptor { get; }

        /// <inheritdoc />
        public DatasetId OutputDatasetId { get; set; }

        /// <inheritdoc />
        public DatasetId LinkDatasetId { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> InputFiles { get; set; }

        /// <inheritdoc />
        public WorkflowType WorkflowType { get; set; }

        /// <inheritdoc />
        public TransformationStatus Status { get; set; }

        /// <inheritdoc />
        public string ErrorMessage { get; set; }

        /// <inheritdoc />
        public int Progress { get; set; }

        /// <inheritdoc />
        public DateTime CreatedOn { get; set; }

        /// <inheritdoc />
        public DateTime UpdatedAt { get; set; }

        /// <inheritdoc />
        public DateTime StartedAt { get; set; }

        /// <inheritdoc />
        public UserId UserId { get; set; }

        /// <inheritdoc/>
        public string JobId { get; set; }

        internal TransformationEntity(IAssetDataSource dataSource, TransformationDescriptor descriptor)
        {
            m_DataSource = dataSource;
            Descriptor = descriptor;
        }

        /// <inheritdoc />
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetTransformationAsync(Descriptor, cancellationToken);
            this.MapFrom(data);
        }

        /// <inheritdoc />
        public async Task TerminateAsync(CancellationToken cancellationToken)
        {
            await m_DataSource.TerminateTransformationAsync(Descriptor.DatasetDescriptor.AssetDescriptor.ProjectDescriptor, Descriptor.TransformationId, cancellationToken);
        }
    }
}
