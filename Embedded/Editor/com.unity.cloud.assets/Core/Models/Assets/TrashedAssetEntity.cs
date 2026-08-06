using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This is a class containing the information about an asset in trash.
    /// </summary>
    sealed class TrashedAssetEntity : ITrashedAsset
    {
        readonly IAssetDataSource m_DataSource;

        /// <inheritdoc />
        public AssetDescriptor Descriptor { get; }

        internal AssetProperties Properties { get; }
        internal IEnumerable<TrashDetails> TrashDetails {  set; get; }

        internal TrashedAssetEntity(AssetDescriptor descriptor, AssetProperties properties, IAssetDataSource dataSource)
        {
            Descriptor = descriptor;
            Properties = properties;
            m_DataSource = dataSource;
        }

        /// <inheritdoc />
        public async Task<AssetProperties> GetPropertiesAsync(CancellationToken cancellationToken)
        {
            var fieldsFilter = FieldsFilter.DefaultAssetIncludes;
            var data = await m_DataSource.GetAssetFromTrashAsync(Descriptor, fieldsFilter, cancellationToken);
            return data.From(Descriptor, fieldsFilter);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<TrashDetails>> GetTrashDetailsAsync(CancellationToken cancellationToken)
        {
            var fieldsFilter = FieldsFilter.TrashDetails;
            var data = await m_DataSource.GetAssetFromTrashAsync(Descriptor, fieldsFilter, cancellationToken);
            return data.TrashDetails?.Select(t => t.From()).ToList() ?? new List<TrashDetails>();
        }
    }
}
