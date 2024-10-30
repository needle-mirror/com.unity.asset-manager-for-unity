using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class AssetDataResolutionInfo
    {
        public IAssetData AssetData { get; set; }
        public bool Existed { get; set; } // Whether the asset is already in the project

        public bool HasChanges =>
            Existed && CurrentVersion != AssetData.SequenceNumber; // Whether the asset version will be changed

        public bool HasConflicts => FileConflicts.Any(); // Whether the asset has conflicting files
        public int CurrentVersion { get; set; } // The index of the version in the asset's version list

        public readonly List<IAssetDataFile> FileConflicts = new();
        public readonly List<Object> DirtyObjects = new();

        public async Task<bool> CheckUpdatedAssetDataUpToDateAsync(IAssetDataManager assetDataManager, CancellationToken token)
        {
            var currentAssetData = assetDataManager.GetAssetData(AssetData.Identifier);

            await AssetData.RefreshVersionsAsync(token);

            CurrentVersion = currentAssetData?.SequenceNumber ?? 0;

            return HasChanges;
        }

        public async Task<bool> CheckUpdatedAssetDataConflictsAsync(IAssetDataManager assetDataManager, CancellationToken token)
        {
            FileConflicts.AddRange(await Utilities.GetModifiedFilesAsync(AssetData.Identifier, AssetData.SourceFiles, assetDataManager, token));

            foreach (var file in FileConflicts)
            {
                try
                {
                    // Check dirty flag
                    string path = $"Assets/{file.Path}";
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
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

            return FileConflicts.Any();
        }
    }
}
