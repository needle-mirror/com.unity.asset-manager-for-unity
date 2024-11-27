using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Core.Editor
{
    interface IUtilitiesProxy : IService
    {
        Task<IEnumerable<BaseAssetDataFile>> GetModifiedFilesAsync(AssetIdentifier identifier, IEnumerable<BaseAssetDataFile> files, IAssetDataManager assetDataManager = null, CancellationToken token = default);
    }

    class UtilitiesProxy : BaseService<IUtilitiesProxy>, IUtilitiesProxy
    {
        public async Task<IEnumerable<BaseAssetDataFile>> GetModifiedFilesAsync(AssetIdentifier identifier, IEnumerable<BaseAssetDataFile> files, IAssetDataManager assetDataManager = null, CancellationToken token = default)
        {
            return await Utilities.GetModifiedFilesAsync(identifier, files, assetDataManager, token);
        }
    }
}
