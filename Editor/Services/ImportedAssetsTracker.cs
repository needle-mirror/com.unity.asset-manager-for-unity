using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IImportedAssetsTracker : IService
    {
        void TrackAssets(IEnumerable<(string originalPath, string finalPath)> assetPaths, IAssetData assetData);
        public void UntrackAsset(AssetIdentifier identifier);
    }

    [Serializable]
    class ImportedAssetsTracker : BaseService<IImportedAssetsTracker>, IImportedAssetsTracker
    {
        [SerializeField]
        bool m_InitialImportedAssetInfoLoaded;

        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IIOProxy m_IOProxy;

        const string k_ImportedAssetFolderName = "ImportedAssetInfo";

        string m_ImportedAssetInfoFolderPath;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy, IAssetDataManager assetDataManager)
        {
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_AssetDataManager = assetDataManager;
        }

        public override void OnEnable()
        {
            m_ImportedAssetInfoFolderPath = Path.Combine(Application.dataPath, "..", "ProjectSettings", "Packages",
                Constants.PackageName, k_ImportedAssetFolderName);
            if (!m_InitialImportedAssetInfoLoaded)
            {
                m_AssetDataManager.SetImportedAssetInfos(ReadAllImportedAssetInfosFromDisk());
                m_InitialImportedAssetInfoLoaded = true;
            }

            m_AssetDatabaseProxy.PostprocessAllAssets += OnPostprocessAllAssets;
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
        }

        public void TrackAssets(IEnumerable<(string originalPath, string finalPath)> assetPaths, IAssetData assetData)
        {
            var fileInfos = new List<ImportedFileInfo>();
            foreach (var item in assetPaths)
            {
                var assetPath = m_IOProxy.GetRelativePathToProjectFolder(item.finalPath);
                var guid = m_AssetDatabaseProxy.AssetPathToGuid(assetPath);

                // Sometimes the download asset file is not tracked by the AssetDatabase and for those cases there won't be a guid related to it
                // like meta files, .DS_Stores and .gitignore files
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                var fileInfo = new ImportedFileInfo(guid, item.originalPath);
                fileInfos.Add(fileInfo);
            }

            if (fileInfos.Count > 0)
            {
                WriteToDisk(assetData, fileInfos);
                m_AssetDataManager.AddGuidsToImportedAssetInfo(assetData, fileInfos);
            }
        }

        public void UntrackAsset(AssetIdentifier identifier)
        {
            if (identifier == null)
            {
                return;
            }

            var filename = identifier.AssetId;
            var importInfoFilePath = Path.Combine(m_ImportedAssetInfoFolderPath, filename);
            m_IOProxy.DeleteFileIfExists(importInfoFilePath);
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs assetChangeArgs)
        {
            foreach (var asset in assetChangeArgs.Removed)
            {
                UntrackAsset(asset);
            }
        }

        public override void OnDisable()
        {
            m_AssetDatabaseProxy.PostprocessAllAssets -= OnPostprocessAllAssets;
        }

        IReadOnlyCollection<ImportedAssetInfo> ReadAllImportedAssetInfosFromDisk()
        {
            try
            {
                if (!m_IOProxy.DirectoryExists(m_ImportedAssetInfoFolderPath))
                {
                    return Array.Empty<ImportedAssetInfo>();
                }

                var result = new Dictionary<AssetIdentifier, ImportedAssetInfo>();
                var filesToCleanup = new List<string>();
                foreach (var filePath in m_IOProxy.EnumerateFiles(m_ImportedAssetInfoFolderPath, "*",
                             SearchOption.AllDirectories))
                {
                    var importedAssetInfo = ImportedAssetInfo.Parse(m_IOProxy.FileReadAllText(filePath));
                    var assetData = importedAssetInfo?.AssetData;

                    if (assetData == null)
                    {
                        filesToCleanup.Add(filePath);
                    }
                    else
                    {
                        result[assetData.Identifier] = importedAssetInfo;
                    }
                }

                // The edge case where we tracked an imported asset in project settings folder but we can't actually found the asset in the AssetDatabase.
                // This could happen if the user deletes some files from their project when the UnityEditor is not running
                foreach (var file in filesToCleanup)
                {
                    m_IOProxy.DeleteFileIfExists(file);
                }

                return result.Values;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't load imported asset infos from disk:\n{e}.");
                return Array.Empty<ImportedAssetInfo>();
            }
        }

        bool WriteToDisk(IAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            var filename = assetData.Identifier.AssetId;
            var importInfoFilePath = Path.Combine(m_ImportedAssetInfoFolderPath, filename);
            try
            {
                var directoryPath = Path.GetDirectoryName(importInfoFilePath);
                if (!m_IOProxy.DirectoryExists(directoryPath))
                {
                    m_IOProxy.CreateDirectory(directoryPath);
                }

                m_IOProxy.FileWriteAllText(importInfoFilePath, ImportedAssetInfo.ToJson(assetData, fileInfos));

                return true;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't write imported asset info to {importInfoFilePath} :\n{e}.");
                return false;
            }
        }

        public static string GetSafeFilename(string input, int maxSize = 20)
        {
            var safeFilename = Regex.Replace(input, "[^a-zA-Z0-9_\\-\\.]", "");

            if (safeFilename.Length > maxSize)
            {
                safeFilename = safeFilename[..maxSize];
            }

            return safeFilename;
        }

        bool RemoveFromDisk(string assetGuid)
        {
            if (string.IsNullOrEmpty(assetGuid))
            {
                return false;
            }

            var importInfoFilePath = Path.Combine(GetIndexFolderPath(assetGuid), assetGuid);
            try
            {
                m_IOProxy.DeleteFileIfExists(importInfoFilePath, true);
                return true;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't remove imported asset info from {importInfoFilePath} :\n{e}.");
                return false;
            }
        }

        string GetIndexFolderPath(string guidString)
        {
            return Path.Combine(m_ImportedAssetInfoFolderPath, guidString.Substring(0, 2));
        }

        void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var guidsToUntrack = new List<string>();

            // A list of deleted paths
            foreach (var assetPath in deletedAssets)
            {
                //Get an assetid from the deleted path // paths will be file id's in the context of am4u
                var guid = m_AssetDatabaseProxy.AssetPathToGuid(assetPath);
                if (m_AssetDataManager.GetImportedAssetInfosFromFileGuid(guid) == null)
                    continue;

                guidsToUntrack.Add(guid);
                RemoveFromDisk(guid);
            }

            m_AssetDataManager.RemoveFilesFromImportedAssetInfos(guidsToUntrack);
        }
    }
}
