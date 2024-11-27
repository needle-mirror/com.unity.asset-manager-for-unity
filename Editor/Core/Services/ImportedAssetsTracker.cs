using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IImportedAssetsTracker : IService
    {
        Task TrackAssets(IEnumerable<(string originalPath, string finalPath)> assetPaths, BaseAssetData assetData);
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
                AssetManagerCoreConstants.PackageName, k_ImportedAssetFolderName);
            if (!m_InitialImportedAssetInfoLoaded)
            {
                m_AssetDataManager.SetImportedAssetInfos(ReadAllImportedAssetInfosFromDisk());
                m_InitialImportedAssetInfoLoaded = true;
            }

            m_AssetDatabaseProxy.PostprocessAllAssets += OnPostprocessAllAssets;
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
        }

        public async Task TrackAssets(IEnumerable<(string originalPath, string finalPath)> assetPaths, BaseAssetData assetData)
        {
            var fileInfos = new List<ImportedFileInfo>();
            foreach (var item in assetPaths)
            {
                var assetPath = Utilities.GetPathRelativeToAssetsFolderIncludeAssets(item.finalPath);
                var guid = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().AssetPathToGuid(assetPath);

                // Sometimes the download asset file is not tracked by the AssetDatabase and for those cases there won't be a guid related to it
                // like meta files, .DS_Stores and .gitignore files
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                var (timestamp, checksum) = await ExtractTimestampAndChecksum(assetPath);

                var metafilePath = MetafilesHelper.AssetMetaFile(assetPath);

                var metaFileTimestamp = 0L;
                string metaFileChecksum = null;

                if (File.Exists(metafilePath))
                {
                    // Ideally run this task in parallel to the one above
                    (metaFileTimestamp, metaFileChecksum) = await ExtractTimestampAndChecksum(metafilePath);
                }

                var fileInfo = new ImportedFileInfo(guid, item.originalPath, checksum, timestamp, metaFileChecksum, metaFileTimestamp);
                fileInfos.Add(fileInfo);
            }

            if (fileInfos.Count > 0)
            {
                WriteToDisk(assetData, fileInfos);
                m_AssetDataManager.AddOrUpdateGuidsToImportedAssetInfo(assetData, fileInfos);
            }
        }

        static async Task<(long, string)> ExtractTimestampAndChecksum(string assetPath)
        {
            var timestamp = ((DateTimeOffset)File.GetLastWriteTimeUtc(assetPath)).ToUnixTimeSeconds();
            var checksum = await Utilities.CalculateMD5ChecksumAsync(assetPath, default);

            return (timestamp, checksum);
        }

        public void UntrackAsset(AssetIdentifier identifier)
        {
            UntrackAsset(new TrackedAssetIdentifier(identifier));
        }

        void UntrackAsset(TrackedAssetIdentifier identifier)
        {
            if (identifier == null)
            {
                return;
            }

            Persistence.RemoveEntry(m_IOProxy, identifier.AssetId);
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs assetChangeArgs)
        {
            foreach (var id in assetChangeArgs.Removed)
            {
                UntrackAsset(id);
            }
        }

        public override void OnDisable()
        {
            m_AssetDatabaseProxy.PostprocessAllAssets -= OnPostprocessAllAssets;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
        }

        IReadOnlyCollection<ImportedAssetInfo> ReadAllImportedAssetInfosFromDisk()
        {
            try
            {
                var result = new Dictionary<AssetIdentifier, ImportedAssetInfo>();
                var importedAssetInfos = Persistence.ReadAllEntries(m_IOProxy);
                foreach (var importedAssetInfo in importedAssetInfos)
                {
                    var assetData = importedAssetInfo?.AssetData;

                    if (assetData != null)
                    {
                        result[assetData.Identifier] = importedAssetInfo;
                    }
                }

                return result.Values;
            }
            catch (Exception e)
            {
                Debug.Log($"Couldn't load imported asset infos from disk:\n{e}.");
                return Array.Empty<ImportedAssetInfo>();
            }
        }

        void WriteToDisk(BaseAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            Persistence.WriteEntry(m_IOProxy, assetData as AssetData, fileInfos);
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
                m_IOProxy.DeleteFile(importInfoFilePath, true);
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
                var guid = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().AssetPathToGuid(assetPath);
                if (m_AssetDataManager.GetImportedAssetInfosFromFileGuid(guid) == null)
                    continue;

                guidsToUntrack.Add(guid);
                RemoveFromDisk(guid);
            }

            m_AssetDataManager.RemoveFilesFromImportedAssetInfos(guidsToUntrack);
        }
    }
}
