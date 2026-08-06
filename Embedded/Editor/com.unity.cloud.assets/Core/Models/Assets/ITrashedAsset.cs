using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// An interface containing the information about an asset in trash.
    /// </summary>
    interface ITrashedAsset
    {
        /// <summary>
        /// The descriptor of the asset.
        /// </summary>
        AssetDescriptor Descriptor { get; }

        /// <summary>
        /// Returns the properties of the asset.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the <see cref="AssetProperties"/> of the asset. </returns>
        Task<AssetProperties> GetPropertiesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns the trash details of the asset.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the trash details of the asset. </returns>
        Task<IEnumerable<TrashDetails>> GetTrashDetailsAsync(CancellationToken cancellationToken);
    }
}
