using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Core.Editor
{
    class AssetDataResolutionInfo
    {
        public BaseAssetData AssetData { get; set; }
        public bool Existed { get; set; } // Whether the asset is already in the project

        public bool IsModified { get; set; }

        public bool HasChanges =>
            Existed && (IsModified || CurrentVersion != AssetData.SequenceNumber); // Whether the asset version will be changed

        public bool IsLatestVersion { get; private set; } // Whether the asset is the latest version

        public bool HasConflicts => FileConflicts.Any(); // Whether the asset has conflicting files
        public int CurrentVersion { get; private set; } // The index of the version in the asset's version list

        public readonly List<BaseAssetDataFile> FileConflicts = new();
        public readonly List<Object> DirtyObjects = new();

        public async Task<bool> CheckUpdatedAssetDataUpToDateAsync(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, CancellationToken token)
        {
            var currentAssetData = assetDataManager.GetAssetData(AssetData.Identifier);
            if(currentAssetData == null)
            {
                IsLatestVersion = false;
                IsModified = false;
                CurrentVersion = 0;
                return false;
            }

            var latestAssetData = await assetsProvider.GetLatestAssetVersionAsync(AssetData.Identifier, token);
            IsLatestVersion = currentAssetData.Identifier.Version == latestAssetData?.Identifier.Version;

            IsModified = currentAssetData.Updated != latestAssetData?.Updated;

            CurrentVersion = currentAssetData.SequenceNumber;

            return HasChanges;
        }

        public async Task<bool> CheckUpdatedAssetDataConflictsAsync(IAssetDataManager assetDataManager, CancellationToken token)
        {
            var utilitiesProxy = ServicesContainer.instance.Resolve<IUtilitiesProxy>();
            FileConflicts.AddRange(await utilitiesProxy.GetModifiedFilesAsync(AssetData.Identifier, AssetData.SourceFiles, assetDataManager, token));

            if (FileConflicts.Any())
            {
                var assetDatabase = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
                var importedAssetInfo = assetDataManager.GetImportedAssetInfo(AssetData.Identifier);

                foreach (var file in FileConflicts)
                {
                    try
                    {
                        var importedFileInfo = importedAssetInfo?.FileInfos.Find(f => Utilities.ComparePaths(f.OriginalPath, file.Path));

                        // Check dirty flag
                        var path = assetDatabase.GuidToAssetPath(importedFileInfo?.Guid);
                        Object asset = assetDatabase.LoadAssetAtPath(path);
                        if (asset != null && EditorUtility.IsDirty(asset))
                        {
                            DirtyObjects.Add(asset);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }

                return true;
            }

            return false;
        }
    }
}
