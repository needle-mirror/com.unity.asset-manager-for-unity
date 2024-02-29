using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IImportedAssetsTracker : IService
    {
        void TrackAssets(IEnumerable<(string originalPath, string finalPath)> assetPaths, IAssetData assetData);
        public void UntrackAsset(AssetIdentifier identifier);
    }

    [Serializable]
    class TrackedData
    {
        [SerializeReference]
        IAssetData m_AssetData;
            
        [SerializeField]
        List<ImportedFileInfo> m_FileInfos;

        public IAssetData assetData => m_AssetData;
            
        public IEnumerable<ImportedFileInfo> fileInfos => m_FileInfos;
            
        public TrackedData(IAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            m_AssetData = assetData;
            m_FileInfos = fileInfos.ToList();
        }

        public static TrackedData Parse(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                return JsonUtility.FromJson<TrackedData>(jsonString);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToJson(IAssetData assetData, IEnumerable<ImportedFileInfo> importedAssetInfo)
        {
            var trackedData = new TrackedData(assetData, importedAssetInfo);
            return JsonUtility.ToJson(trackedData, true);
        }
    }
    
    [Serializable]
    internal class ImportedAssetsTracker : BaseService<IImportedAssetsTracker>, IImportedAssetsTracker
    {

        private const string k_ImportedAssetFolderName = "ImportedAssetInfo";
        private string m_ImportedAssetInfoFolderPath;

        [SerializeField]
        private bool m_InitialImportedAssetInfoLoaded;

        [SerializeReference]
        IIOProxy m_IOProxy;
        
        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;
        
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy, IAssetDataManager assetDataManager)
        {
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_AssetDataManager = assetDataManager;
        }

        public override void OnEnable()
        {
            m_ImportedAssetInfoFolderPath = Path.Combine(Application.dataPath, "..", "ProjectSettings", "Packages", Constants.PackageName, k_ImportedAssetFolderName);
            if (!m_InitialImportedAssetInfoLoaded)
            {
                m_AssetDataManager.SetImportedAssetInfos(ReadAllImportedAssetInfosFromDisk());
                m_InitialImportedAssetInfoLoaded = true;
            }
            m_AssetDatabaseProxy.onPostprocessAllAssets += OnPostprocessAllAssets;
        }

        public override void OnDisable()
        {
            m_AssetDatabaseProxy.onPostprocessAllAssets -= OnPostprocessAllAssets;
        }

        private IReadOnlyCollection<ImportedAssetInfo> ReadAllImportedAssetInfosFromDisk()
        {
            try
            {
                if (!m_IOProxy.DirectoryExists(m_ImportedAssetInfoFolderPath))
                    return Array.Empty<ImportedAssetInfo>();

                var result = new Dictionary<AssetIdentifier, ImportedAssetInfo>();
                var filesToCleanup = new List<string>();
                foreach (var filePath in m_IOProxy.EnumerateFiles(m_ImportedAssetInfoFolderPath, "*", SearchOption.AllDirectories))
                {
                    var trackedData = TrackedData.Parse(m_IOProxy.FileReadAllText(filePath));
                    var assetData = trackedData?.assetData;

                    if (assetData == null)
                    {
                        filesToCleanup.Add(filePath);
                    }
                    else
                    {
                        var guid = Path.GetFileName(filePath); // TODO Fix Me and Serialize
                        foreach (var file in trackedData.fileInfos)
                        {
                            if (result.TryGetValue(assetData.identifier, out var importedAssetInfo))
                            {
                                importedAssetInfo.fileInfos.Add(new ImportedFileInfo(guid, file.originalPath));
                            }
                            else
                            {
                                result[assetData.identifier] = new ImportedAssetInfo(assetData, new List<ImportedFileInfo> { new (guid, file.originalPath) });
                            }    
                        }    
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

        private bool WriteToDisk(IAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            var filename = assetData.identifier.assetId;
            var importInfoFilePath = Path.Combine(m_ImportedAssetInfoFolderPath, filename);
            try
            {
                var directoryPath = Path.GetDirectoryName(importInfoFilePath);
                if (!m_IOProxy.DirectoryExists(directoryPath))
                {
                    m_IOProxy.CreateDirectory(directoryPath);
                }

                m_IOProxy.FileWriteAllText(importInfoFilePath, TrackedData.ToJson(assetData, fileInfos));
                
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
                safeFilename = safeFilename[0..maxSize];
            }

            return safeFilename;
        }

        private bool RemoveFromDisk(string assetGuid)
        {
            if (string.IsNullOrEmpty(assetGuid))
                return false;

            var importInfoFilePath = Path.Combine(GetIndexFolderPath(assetGuid), assetGuid);
            try
            {
                m_IOProxy.DeleteFileIfExists(importInfoFilePath, recursivelyRemoveEmptyParentFolders: true);
                return true;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't remove imported asset info from {importInfoFilePath} :\n{e}.");
                return false;
            }
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
                    continue;

                var fileInfo = new ImportedFileInfo(guid,  item.originalPath);
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
                return;
            
            var filename = identifier.assetId;
            var importInfoFilePath = Path.Combine(m_ImportedAssetInfoFolderPath, filename);
            m_IOProxy.DeleteFileIfExists(importInfoFilePath);
        }

        private string GetIndexFolderPath(string guidString)
        {
            return Path.Combine(m_ImportedAssetInfoFolderPath, guidString.Substring(0, 2));
        }

        private void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var guidsToUntrack = new List<string>();
            foreach (var assetPath in deletedAssets)
            {
                var guid = m_AssetDatabaseProxy.AssetPathToGuid(assetPath);
                if (m_AssetDataManager.GetImportedAssetInfo(guid) == null)
                    continue;
                guidsToUntrack.Add(guid);
                RemoveFromDisk(guid);
            }
            m_AssetDataManager.RemoveGuidsFromImportedAssetInfos(guidsToUntrack);
        }
    }
}
