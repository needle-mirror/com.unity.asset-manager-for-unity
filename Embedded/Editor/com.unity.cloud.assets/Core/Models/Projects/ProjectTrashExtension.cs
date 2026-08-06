using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    static class ProjectTrashExtension
    {
        /// <summary>
        /// Restores the trashed assets from the trash back to the project.
        /// </summary>
        /// <param name="assetProject">The target project. </param>
        /// <param name="trashedAssets">The trashed assets to restore. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        public static Task RestoreTrashedAssetsAsync(this IAssetProject assetProject, IEnumerable<ITrashedAsset> trashedAssets, CancellationToken cancellationToken)
        {
            return assetProject.RestoreTrashedAssetsAsync(trashedAssets.Select(a => a.Descriptor.AssetId), cancellationToken);
        }

        /// <summary>
        /// Permanently deletes the trashed assets from the trash.
        /// </summary>
        /// <param name="assetProject">The target project. </param>
        /// <param name="trashedAssets">The trashed assets to delete. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        public static Task DeleteAssetsFromTrashAsync(this IAssetProject assetProject, IEnumerable<ITrashedAsset> trashedAssets, CancellationToken cancellationToken)
        {
            return assetProject.DeleteAssetsFromTrashAsync(trashedAssets.Select(a => a.Descriptor.AssetId), cancellationToken);
        }
    }
}
