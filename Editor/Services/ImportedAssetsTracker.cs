using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IImportedAssetsTracker : IService
    {
        void TrackAssets(IReadOnlyCollection<(string originalPath, string finalPath)> assetPaths, AssetIdentifier assetId);
    }

    [Serializable]
    internal class ImportedAssetsTracker : BaseService<IImportedAssetsTracker>, IImportedAssetsTracker
    {
        private class TrackedData
        {
            public string organizationId;
            public string projectId;
            public string sourceId;
            public string version;
            public string originalPath;

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

            public AssetIdentifier GetAssetId()
            {
                return new AssetIdentifier
                {
                    organizationId = organizationId,
                    projectId = projectId,
                    sourceId = sourceId,
                    version = version
                };
            }

            public ImportedFileInfo GetImportedFileInfo(string guid)
            {
                return new ImportedFileInfo
                {
                    guid = guid,
                    originalPath = originalPath,
                };
            }

            public static string ToJson(AssetIdentifier assetId, ImportedFileInfo fileInfo)
            {
                var trackedData = new TrackedData
                {
                    organizationId = assetId.organizationId,
                    projectId = assetId.projectId,
                    sourceId = assetId.sourceId,
                    version = assetId.version,
                    originalPath = fileInfo.originalPath
                };
                return JsonUtility.ToJson(trackedData, true);
            }
        }

        private const string k_ImportedAssetFolderName = "ImportedAssetInfo";
        private string m_ImportedAssetInfoFolderPath;

        [SerializeField]
        private bool m_InitialImportedAssetInfoLoaded;

        private readonly IIOProxy m_IOProxy;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        private readonly IAssetDataManager m_AssetDataManager;
        public ImportedAssetsTracker(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy, IAssetDataManager assetDataManager)
        {
            m_IOProxy = RegisterDependency(ioProxy);
            m_AssetDatabaseProxy = RegisterDependency(assetDatabaseProxy);
            m_AssetDataManager = RegisterDependency(assetDataManager);
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
                    var assetId = trackedData?.GetAssetId();
                    if (assetId == null)
                        continue;
                    var guid = Path.GetFileName(filePath);
                    // The edge case where we tracked an imported asset in project settings folder but we can't actually found the asset in the AssetDatabase.
                    // This could happen if the user deletes some files from their project when the UnityEditor is not running
                    var path = m_AssetDatabaseProxy.GuidToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !m_IOProxy.FileExists(path))
                    {
                        filesToCleanup.Add(filePath);
                        continue;
                    }

                    if (result.TryGetValue(assetId, out var importedAssetInfo))
                        importedAssetInfo.fileInfos.Add(trackedData.GetImportedFileInfo(guid));
                    else
                        result[assetId] = new ImportedAssetInfo{ id = assetId, fileInfos = new List<ImportedFileInfo>{ trackedData.GetImportedFileInfo(guid) } };
                }

                foreach (var file in filesToCleanup)
                    m_IOProxy.DeleteFileIfExists(file);
                return result.Values;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't load imported asset infos from disk:\n{e}.");
                return Array.Empty<ImportedAssetInfo>();
            }
        }

        private bool WriteToDisk(ImportedFileInfo fileInfo, AssetIdentifier assetId)
        {
            if (string.IsNullOrEmpty(fileInfo?.guid))
                return false;
            var indexFolderPath = GetIndexFolderPath(fileInfo.guid);
            var importInfoFilePath = Path.Combine(indexFolderPath, fileInfo.guid);
            try
            {
                if (!m_IOProxy.DirectoryExists(indexFolderPath))
                    m_IOProxy.CreateDirectory(indexFolderPath);
                m_IOProxy.FileWriteAllText(importInfoFilePath, TrackedData.ToJson(assetId, fileInfo));
                return true;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't write imported asset info to {importInfoFilePath} :\n{e}.");
                return false;
            }
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

        public void TrackAssets(IReadOnlyCollection<(string originalPath, string finalPath)> assetPaths, AssetIdentifier assetId)
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

                var fileInfo = new ImportedFileInfo { guid = guid, originalPath = item.originalPath};
                fileInfos.Add(fileInfo);
                WriteToDisk(fileInfo, assetId);
            }
            if (fileInfos.Count > 0)
                m_AssetDataManager.AddGuidsToImportedAssetInfo(assetId, fileInfos);
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
