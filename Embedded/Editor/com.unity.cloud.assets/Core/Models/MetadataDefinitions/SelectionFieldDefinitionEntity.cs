using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class SelectionFieldDefinitionEntity : FieldDefinitionEntity, ISelectionFieldDefinition
    {
        /// <inheritdoc/>
        public IEnumerable<string> AcceptedValues { get; set; }

        /// <inheritdoc/>
        public bool Multiselection { get; set; }

        internal SelectionFieldDefinitionEntity(IAssetDataSource dataSource, FieldDefinitionDescriptor descriptor)
            : base(dataSource, descriptor) { }

        /// <inheritdoc/>
        public Task SetSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken)
        {
            var definitionUpdate = new FieldDefinitionBaseData
            {
                AcceptedValues = acceptedValues.ToArray()
            };
            return m_DataSource.UpdateFieldDefinitionAsync(Descriptor, definitionUpdate, cancellationToken);
        }

        /// <inheritdoc/>
        public Task AddSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken)
        {
            return m_DataSource.AddAcceptedValuesToFieldDefinitionAsync(Descriptor, acceptedValues, cancellationToken);
        }

        /// <inheritdoc/>
        public Task RemoveSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken)
        {
            return m_DataSource.RemoveAcceptedValuesFromFieldDefinitionAsync(Descriptor, acceptedValues, cancellationToken);
        }
    }
}
