using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class FieldDefinitionEntity : IFieldDefinition
    {
        private protected readonly IAssetDataSource m_DataSource;

        /// <inheritdoc/>
        public FieldDefinitionDescriptor Descriptor { get; }

        /// <inheritdoc/>
        public FieldDefinitionType Type { get; set; }

        /// <inheritdoc/>
        public bool IsDeleted { get; set; }

        /// <inheritdoc/>
        public string DisplayName { get; set; }

        /// <inheritdoc/>
        public AuthoringInfo AuthoringInfo { get; set; }

        /// <inheritdoc/>
        public FieldDefinitionOrigin Origin { get; set; }

        internal FieldDefinitionEntity(IAssetDataSource dataSource, FieldDefinitionDescriptor descriptor)
        {
            m_DataSource = dataSource;
            Descriptor = descriptor;
        }

        /// <inheritdoc/>
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetFieldDefinitionAsync(Descriptor, cancellationToken);
            this.MapFrom(data);
        }

        /// <inheritdoc/>
        public Task UpdateAsync(IFieldDefinitionUpdate definitionUpdate, CancellationToken cancellationToken)
        {
            return m_DataSource.UpdateFieldDefinitionAsync(Descriptor, definitionUpdate.From(), cancellationToken);
        }
    }
}
