using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IImportedAssetsTracker : IService
    {
        Task TrackAssets(IEnumerable<(string originalPath, string finalPath, string checksum)> assetPaths, BaseAssetData assetData);
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
        IFileUtility m_FileUtility;

        [SerializeReference]
        IPersistenceManager m_PersistenceManager;

        [ServiceInjection]
        public void Inject(IAssetDatabaseProxy assetDatabaseProxy, IAssetDataManager assetDataManager, IFileUtility fileUtility, IPersistenceManager persistenceManager)
        {
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_AssetDataManager = assetDataManager;
            m_FileUtility = fileUtility;
            m_PersistenceManager = persistenceManager;
        }

        public override void OnEnable()
        {
            base.OnEnable();

            if (!m_InitialImportedAssetInfoLoaded)
            {
                var entries = m_PersistenceManager.ReadAllEntries();
                m_AssetDataManager.SetImportedAssetInfos(entries);

                m_InitialImportedAssetInfoLoaded = true;
            }

            m_AssetDatabaseProxy.PostprocessAllAssets += OnPostprocessAllAssets;
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_PersistenceManager.AssetEntryModified += OnAssetEntryModified;
            m_PersistenceManager.AssetEntryRemoved += OnAssetEntryRemoved;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();

            m_PersistenceManager ??= ServicesContainer.instance.Get<IPersistenceManager>();
            m_FileUtility ??= ServicesContainer.instance.Get<IFileUtility>();
        }

        public async Task TrackAssets(IEnumerable<(string originalPath, string finalPath, string checksum)> assetPaths, BaseAssetData assetData)
        {
            var fileInfos = new List<ImportedFileInfo>();
            foreach (var item in assetPaths)
            {
                var assetPath = Utilities.GetPathRelativeToAssetsFolderIncludeAssets(item.finalPath);
                var guid = m_AssetDatabaseProxy.AssetPathToGuid(assetPath);

                // Sometimes the download asset file is not tracked by the AssetDatabase and for those cases there won't be a guid related to it
                // like meta files, .DS_Stores and .gitignore files
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                string checksum = item.checksum;
                if (checksum == null)
                {
                    checksum = await m_FileUtility.CalculateMD5ChecksumAsync(assetPath, default);
                }

                var timestamp = m_FileUtility.GetTimestamp(assetPath) ?? 0L;

                var metafilePath = MetafilesHelper.AssetMetaFile(assetPath);

                var metaFileTimestamp = m_FileUtility.GetTimestamp(metafilePath);
                string metaFileChecksum = null;

                if (metaFileTimestamp.HasValue)
                {
                    // Ideally run this task in parallel to the one above
                    metaFileChecksum = await m_FileUtility.CalculateMD5ChecksumAsync(metafilePath, default);
                }

                var datasetId = assetData.Datasets.FirstOrDefault(x => x.Files.Any(f => f.Path == item.originalPath))?.Id;

                var fileInfo = new ImportedFileInfo(datasetId, guid, item.originalPath, checksum, timestamp, metaFileChecksum, metaFileTimestamp ?? 0L);
                fileInfos.Add(fileInfo);
            }

            try
            {
                WriteTrackedAsset(assetData, fileInfos);
                // All files written successfully - add all to memory
                m_AssetDataManager.AddOrUpdateGuidsToImportedAssetInfo(assetData, fileInfos);
            }
            catch (TrackingFilePathTooLongException ex)
            {
                // Some files failed to write - only add successfully written files to memory
                if (ex.SuccessfullyWrittenFileInfos.Count > 0)
                {
                    m_AssetDataManager.AddOrUpdateGuidsToImportedAssetInfo(assetData, ex.SuccessfullyWrittenFileInfos);
                }
                // Exception has already been logged and displayed by PersistenceManager
            }
        }

        public void UntrackAsset(AssetIdentifier identifier)
        {
            RemoveTrackedAsset(new TrackedAssetIdentifier(identifier));
        }

        void WriteTrackedAsset(BaseAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            m_PersistenceManager.WriteEntry(assetData as AssetData, fileInfos);
        }

        void RemoveTrackedAsset(TrackedAssetIdentifier identifier)
        {
            if (identifier == null)
                return;

            m_PersistenceManager.RemoveEntry(identifier.AssetId);
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs assetChangeArgs)
        {
            foreach (var id in assetChangeArgs.Removed)
                RemoveTrackedAsset(id);
        }

        public override void OnDisable()
        {
            m_AssetDatabaseProxy.PostprocessAllAssets -= OnPostprocessAllAssets;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_PersistenceManager.AssetEntryModified -= OnAssetEntryModified;
            m_PersistenceManager.AssetEntryRemoved -= OnAssetEntryRemoved;
        }

        void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var guidsToUntrack = new List<string>();

            // Handle deleted assets
            foreach (var assetPath in deletedAssets)
            {
                //Get an assetid from the deleted path // paths will be file id's in the context of am4u
                var guid = m_AssetDatabaseProxy.AssetPathToGuid(assetPath);
                if (m_AssetDataManager.GetImportedAssetInfosFromFileGuid(guid) == null)
                    continue;

                guidsToUntrack.Add(guid);
            }

            m_AssetDataManager.RemoveFilesFromImportedAssetInfos(guidsToUntrack);
        }

        void OnAssetEntryModified(object sender, ImportedAssetInfo importedAssetInfo)
        {
            if (importedAssetInfo == null)
            {
                return;
            }

            Utilities.DevLog($"Asset entry modified: {importedAssetInfo.AssetData?.Identifier.AssetId}", highlight: true);
            // Tracking file was added/modified (e.g. FileWatcher); skip cache update so we don't overwrite with tracking-only data.
            m_AssetDataManager.AddOrUpdateGuidsToImportedAssetInfo(importedAssetInfo.AssetData, importedAssetInfo.FileInfos, shouldUpdateCache: false);
        }

        void OnAssetEntryRemoved(object sender, string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            Utilities.DevLog($"Asset entry removed: {assetId}", DevLogHighlightColor.Red);

            var importedAssetInfo = m_AssetDataManager.GetImportedAssetInfo(assetId);
            if (importedAssetInfo == null)
            {
                // Asset info not found - this is expected if it was already removed or never existed in the manager.
                // The file system event still fires even if the asset info was already cleaned up.
                Utilities.DevLog($"OnAssetEntryRemoved: importedAssetInfo not found for assetId {assetId} (may have been already removed)");
                return;
            }

            if (importedAssetInfo.Identifier == null)
            {
                Utilities.DevLogWarning($"OnAssetEntryRemoved: importedAssetInfo.Identifier is null for assetId {assetId}", highlight: true);
                return;
            }

            m_AssetDataManager.RemoveImportedAssetInfo(new[] { importedAssetInfo.Identifier });
        }

    }
}
