using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class LabelEntity : ILabel
    {
        readonly IAssetDataSource m_AssetDataSource;

        /// <inheritdoc/>
        public LabelDescriptor Descriptor { get; private set; }

        /// <inheritdoc/>
        public string Description { get; set; }

        /// <inheritdoc/>
        public bool IsSystemLabel { get; set; }

        /// <inheritdoc/>
        public bool IsAssignable { get; set; }

        /// <inheritdoc/>
        public Color DisplayColor { get; set; }

        /// <inheritdoc/>
        public AuthoringInfo AuthoringInfo { get; set; }

        public LabelEntity(IAssetDataSource assetDataSource, LabelDescriptor descriptor)
        {
            m_AssetDataSource = assetDataSource;
            Descriptor = descriptor;
        }

        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var data = await m_AssetDataSource.GetLabelAsync(Descriptor, cancellationToken);
            if (data != null)
                this.MapFrom(data);
        }

        /// <inheritdoc/>
        public Task UpdateAsync(ILabelUpdate labelUpdate, CancellationToken cancellationToken)
        {
            return m_AssetDataSource.UpdateLabelAsync(Descriptor, labelUpdate.From(), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task RenameAsync(string labelName, CancellationToken cancellationToken)
        {
            var labelUpdate = new LabelBaseData {Name = labelName};
            await m_AssetDataSource.UpdateLabelAsync(Descriptor, labelUpdate, cancellationToken);

            // On success, the descriptor must be modified immediately.
            Descriptor = new LabelDescriptor(Descriptor.OrganizationId, labelUpdate.Name);
        }

        /// <inheritdoc/>
        public Task ArchiveAsync(CancellationToken cancellationToken)
        {
            return m_AssetDataSource.UpdateLabelStatusAsync(Descriptor, true, cancellationToken);
        }

        /// <inheritdoc/>
        public Task UnarchiveAsync(CancellationToken cancellationToken)
        {
            return m_AssetDataSource.UpdateLabelStatusAsync(Descriptor, false, cancellationToken);
        }
    }
}
