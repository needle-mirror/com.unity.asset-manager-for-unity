using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    class AssetDataResolutionInfo
    {
        public IAssetData AssetData { get; set; }
        public bool Existed { get; set; } // Whether the asset is already in the project
        public bool HasChanges => Existed && CurrentVersion != AssetData.SequenceNumber; // Whether the asset version will be changed
        public bool HasConflicts { get; set; } // Whether the asset has been modified locally
        public int CurrentVersion { get; set; } // The index of the version in the asset's version list

        public async Task<bool> CheckUpdatedAssetDataUpToDateAsync(CancellationToken token)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var currentAssetData = assetDataManager.GetAssetData(AssetData.Identifier);

            await AssetData.RefreshVersionsAsync(token);

            CurrentVersion = currentAssetData.SequenceNumber;

            return HasChanges;
        }

        public Task<bool> CheckUpdatedAssetDataConflictsAsync(CancellationToken token)
        {
            // TODO: Find a way to check conflicting files (maybe using file update date)
            return Task.FromResult(false);
        }
    }
}
