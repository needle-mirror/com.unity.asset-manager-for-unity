using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Core.Editor
{
    class AssetDataResolutionInfo
    {
        readonly List<BaseAssetDataFile> m_FileConflicts = new();
        readonly List<Object> m_DirtyObjects = new();

        public BaseAssetData AssetData { get; }

        public bool Existed { get; } // Whether the asset is already in the project

        public bool HasChanges { get; } // Whether the asset data has changed

        public int CurrentVersion { get; } // The index of the version in the asset's version list

        public bool HasConflicts => m_FileConflicts.Any(); // Whether the asset has conflicting files

        public int ConflictCount => m_FileConflicts.Count;

        public IEnumerable<Object> DirtyObjects => m_DirtyObjects;

        public AssetDataResolutionInfo(BaseAssetData assetData, IAssetDataManager assetDataManager)
        {
            AssetData = assetData;
            Existed = assetDataManager.IsInProject(assetData.Identifier);

            var currentAssetData = assetDataManager.GetAssetData(AssetData.Identifier);
            if (currentAssetData == null)
            {
                CurrentVersion = 0;
                HasChanges = Existed;
            }
            else
            {
                CurrentVersion = currentAssetData.SequenceNumber;

                var isDifferentVersion = currentAssetData.Identifier.Version != AssetData.Identifier.Version;
                var isUpdated = currentAssetData.Updated != AssetData.Updated;
                HasChanges = Existed && (isDifferentVersion || isUpdated);
            }
        }

        public void GatherFileConflicts(ISettingsManager settingsManager, string destinationPath)
        {
            // For an asset that is newly imported, check if a matching file already exists in the project.
            // We don't check for an asset that was previously imported because it would naturally generate a conflict with it's own files.
            if (!Existed)
            {
                foreach (var file in AssetData.SourceFiles.Where(f => Exists(settingsManager, destinationPath, f)))
                {
                    m_FileConflicts.Add(file);
                }
            }
        }

        public async Task GatherFileConflictsAsync(IAssetDataManager assetDataManager, CancellationToken token)
        {
            var utilitiesProxy = ServicesContainer.instance.Resolve<IUtilitiesProxy>();

            var modifiedFiles = await utilitiesProxy.GetModifiedFilesAsync(AssetData.Identifier, AssetData.SourceFiles,
                assetDataManager, token);

            m_FileConflicts.AddRange(modifiedFiles);
            if (m_FileConflicts.Any())
            {
                var assetDatabase = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
                var importedAssetInfo = assetDataManager.GetImportedAssetInfo(AssetData.Identifier);

                foreach (var file in m_FileConflicts)
                {
                    try
                    {
                        var importedFileInfo =
                            importedAssetInfo?.FileInfos.Find(f => Utilities.ComparePaths(f.OriginalPath, file.Path));

                        // Check dirty flag
                        var path = assetDatabase.GuidToAssetPath(importedFileInfo?.Guid);
                        var asset = assetDatabase.LoadAssetAtPath(path);
                        if (asset != null && EditorUtility.IsDirty(asset))
                        {
                            m_DirtyObjects.Add(asset);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
            }
        }

        public bool ExistsConflict(BaseAssetDataFile file)
        {
            return m_FileConflicts.Contains(file);
        }

        bool Exists(ISettingsManager settingsManager, string destinationPath, BaseAssetDataFile file)
        {
            if (settingsManager.IsSubfolderCreationEnabled)
            {
                var regex = new Regex(@"[\\\/:*?""<>|]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                var sanitizedAssetName = regex.Replace(AssetData.Name, "");
                destinationPath = Path.Combine(destinationPath, $"{sanitizedAssetName.Trim()}");
            }

            var filePath = Path.Combine(destinationPath, file.Path);
            return File.Exists(filePath);
        }
    }
}
