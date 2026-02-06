using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Task = System.Threading.Tasks.Task;

namespace Unity.AssetManager.Core.Editor
{
    interface IAssetImporter : IService
    {
        ImportOperation GetImportOperation(AssetIdentifier identifier);
        bool IsImporting(AssetIdentifier identifier);
        Task<ImportResultInternal> StartImportAsync(ImportTrigger trigger, List<BaseAssetData> assets, ImportSettings importSettings, CancellationToken cancellationToken = default);
        Task UpdateAllToLatestAsync(ImportTrigger trigger, ProjectOrLibraryInfo projectOrLibrary, CollectionInfo collection,  CancellationToken token);
        Task UpdateAllToLatestAsync(ImportTrigger trigger, IEnumerable<BaseAssetData> assets, CancellationToken token);
        void StopTrackingAssets(List<AssetIdentifier> identifiers);
        bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false);
        bool RemoveImports(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false);
        void ShowInProject(AssetIdentifier identifier);
        void CancelImport(AssetIdentifier identifier, bool showConfirmationDialog = false);
        void CancelBulkImport(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false);
    }

    static class MetafilesHelper
    {
        public static readonly string MetaFileExtension = ".meta";

        public static bool IsOrphanMetafile(string fileName, IEnumerable<string> allFiles)
        {
            return IsMetafile(fileName) && !allFiles.Contains(fileName[..^MetaFileExtension.Length]);
        }

        public static bool IsMetafile(string fileName)
        {
            return fileName.EndsWith(MetaFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static string RemoveMetaExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }

            return IsMetafile(fileName) ? fileName[..^MetaFileExtension.Length] : fileName;
        }

        public static string AssetMetaFile(string fileName)
        {
            return ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().GetTextMetaFilePathFromAssetPath(fileName);
        }

        /// <summary>
        /// Parses the GUID from a Unity .meta file content.
        /// Unity .meta files contain a line like: "guid: abc123def456..."
        /// </summary>
        /// <param name="metaFileContent">The content of the .meta file</param>
        /// <returns>The GUID string, or null if not found</returns>
        public static string ParseGuidFromMetaContent(string metaFileContent)
        {
            if (string.IsNullOrEmpty(metaFileContent))
                return null;

            var match = Regex.Match(metaFileContent, @"guid:\s*([a-fA-F0-9]+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    [Serializable]
    class AssetImporter : BaseService<IAssetImporter>, IAssetImporter
    {
        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [SerializeReference]
        IEditorUtilityProxy m_EditorUtilityProxy;

        [SerializeReference]
        IImportedAssetsTracker m_ImportedAssetsTracker;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IAssetImportResolver m_Resolver;

        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;

        [SerializeReference]
        IFileUtility m_FileUtility;

        // Do not serialize as import operations are not domain reload safe and the flag should be reset.
        [NonSerialized]
        bool m_IsImporting;

        readonly Dictionary<string, ImportOperation> m_ImportOperations = new();
        readonly Dictionary<Uri, DownloadOperation> m_UriToDownloadOperationMap = new();

        CancellationTokenSource m_TokenSource;
        BulkImportOperation m_BulkImportOperation;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker,
            IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager,
            ISettingsManager settingsManager, IAssetsProvider assetsProvider, IAssetImportResolver assetImportResolver,
            IApplicationProxy applicationProxy, IFileUtility fileUtility)
        {
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_EditorUtilityProxy = editorUtilityProxy;
            m_ImportedAssetsTracker = importedAssetsTracker;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
            m_SettingsManager = settingsManager;
            m_AssetsProvider = assetsProvider;
            m_Resolver = assetImportResolver;
            m_ApplicationProxy = applicationProxy;
            m_FileUtility = fileUtility;
        }

        public override void OnEnable()
        {
            m_AssetOperationManager.FinishedOperationsCleared += OnFinishedOperationsCleared;
        }

        public override void OnDisable()
        {
            m_AssetOperationManager.FinishedOperationsCleared -= OnFinishedOperationsCleared;
        }

        public ImportOperation GetImportOperation(AssetIdentifier identifier)
        {
            return m_ImportOperations.GetValueOrDefault(identifier.AssetId);
        }

        public bool IsImporting(AssetIdentifier identifier)
        {
            var importOperation = GetImportOperation(identifier);
            return importOperation is { Status: OperationStatus.InProgress };
        }

        public async Task<ImportResultInternal> StartImportAsync(ImportTrigger trigger, List<BaseAssetData> assets, ImportSettings importSettings, CancellationToken cancellationToken = default)
        {
            if (IsDebugLogEnabled())
            {
                if (assets == null || assets.Count == 0)
                {
                    Debug.Log("No asset were requested for import.");
                }
                else
                {
                    Debug.Log(
                        $"The assets requested to be imported are:\n{string.Join("\n", assets.Select(x => x.Identifier.ToString()))}\n");
                }
            }

            if (m_IsImporting)
            {
                return new ImportResultInternal
                {
                    OperationInProgress = true
                };
            }

            if (!string.IsNullOrEmpty(importSettings.DestinationPathOverride))
            {
                if (!Utilities.IsSubdirectoryOrSame(importSettings.DestinationPathOverride, m_ApplicationProxy.DataPath))
                    throw new ArgumentException("Import destination is outside of the Assets folder.");
            }

            m_IsImporting = true;

            SendImportAnalytics(trigger, assets);

            var avoidRollingBackAssetVersion = false;
            var disableReimportModal = false;

            switch (importSettings.ConflictResolutionOverride)
            {
                case ConflictResolutionOverride.None:
                    disableReimportModal = m_SettingsManager.IsReimportModalDisabled;
                    avoidRollingBackAssetVersion = m_SettingsManager.IsKeepHigherVersionEnabled;
                    break;

                case ConflictResolutionOverride.AllowAssetVersionRollbackAndShowConflictResolver:
                    disableReimportModal = false;
                    avoidRollingBackAssetVersion = false;
                    break;

                case ConflictResolutionOverride.PreventAssetVersionRollbackAndShowConflictResolver:
                    disableReimportModal = false;
                    avoidRollingBackAssetVersion = true;
                    break;

                case ConflictResolutionOverride.PreventAssetVersionRollbackAndReplaceAll:
                    disableReimportModal = true;
                    avoidRollingBackAssetVersion = true;
                    break;
            }

            var mappedImportSettings = new ImportSettingsInternal(
                importSettings.Type,
                disableReimportModal,
                avoidRollingBackAssetVersion,
                m_SettingsManager.DefaultImportLocation,
                importSettings.DestinationPathOverride);

            m_AssetOperationManager.ClearFinishedOperations();

            try
            {
                // Create a temporary operation to track the (long) dependency loading
                var processingOperation = new ProcessingOperation();
                processingOperation.Start();
                var syncWithCloudOperations = new List<IndefiniteOperation>();
                foreach (var asset in assets)
                {
                    var operation = new IndefiniteOperation(asset);
                    m_AssetOperationManager.RegisterOperation(operation);
                    operation.Start();
                    syncWithCloudOperations.Add(operation);
                }

                m_TokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = m_TokenSource.Token;

                BaseAssetData[] resolutions;
                try
                {
                    var result = await m_Resolver.Resolve(assets, mappedImportSettings, token);
                    resolutions = result?.ToArray() ?? Array.Empty<BaseAssetData>();

                    var emptyAssets = resolutions.Where(a => !a.HasImportableFiles()).ToArray();
                    if (emptyAssets.Length > 0)
                    {
                        var names = string.Join(", ", emptyAssets.Select(a => $"\"{a.Name}\""));
                        Debug.LogWarning($"Import skipped for assets with no importable files: {names}");
                        resolutions = resolutions.Where(a => a.HasImportableFiles()).ToArray();
                    }

                    if (IsDebugLogEnabled())
                    {
                        if (resolutions.Length == 0)
                        {
                            Debug.Log("No assets to import after resolution.");
                        }
                        else
                        {
                            Debug.Log("The assets and dependencies to import following any conflict resolution:\n" +
                                      string.Join("\n", resolutions.Select(x => x.Identifier.ToString())));
                        }
                    }
                }
                finally
                {
                    processingOperation.Finish(OperationStatus.Success);
                    foreach (var operation in syncWithCloudOperations)
                    {
                        operation.Finish(OperationStatus.Success);
                    }
                }

                var importResult = new ImportResultInternal
                {
                    AssetsAndDependencies = Array.Empty<BaseAssetData>(),
                    Assets = Array.Empty<BaseAssetData>()
                };

                if (resolutions.Length > 0)
                {
                    if (await Import(trigger, resolutions, mappedImportSettings, token))
                    {
                        importResult.AssetsAndDependencies = resolutions;

                        // Isolate the assets to those from the original list;
                        // the versions may have changed so only compare part of the AssetIdentifier
                        importResult.Assets = resolutions.Where(x => assets.Any(y =>
                            y.Identifier.OrganizationId == x.Identifier.OrganizationId &&
                            y.Identifier.ProjectId == x.Identifier.ProjectId &&
                            y.Identifier.AssetId == x.Identifier.AssetId));
                    }
                }

                // Because the Import call above will not throw on cancel, we need to check the token here
                token.ThrowIfCancellationRequested();

                if (IsDebugLogEnabled())
                {
                    Debug.Log(
                        $"Import completed. The following assets were imported (including dependencies):\n{string.Join("\n", importResult.AssetsAndDependencies.Select(x => x.Identifier.ToString()))}");
                }

                return importResult;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Import operation cancelled.");
                return new ImportResultInternal
                {
                    Cancelled = true
                };
            }
            catch (Exception)
            {
                return default;
            }
            finally
            {
                m_IsImporting = false;
            }
        }

        public async Task UpdateAllToLatestAsync(ImportTrigger trigger, ProjectOrLibraryInfo projectOrLibrary, CollectionInfo collection, CancellationToken token)
        {
            var insideCollection = collection != null && !string.IsNullOrEmpty(collection.Name);

            var assets = m_AssetDataManager.ImportedAssetInfos.Select(info => info.AssetData);

            if (insideCollection)
            {
                var collectionProjectId = collection.ProjectId;
                var collectionAssetIds = new HashSet<string>();

                m_EditorUtilityProxy.DisplayProgressBar($"Getting assets from collection {collection.GetFullPath()}", string.Empty, 0f);

                // Get assets from the collection
                await foreach (var assetIdentifier in m_AssetsProvider.SearchLiteAsync(collection.OrganizationId,
                                   new List<string> {collectionProjectId},
                                   new AssetSearchFilter {Collection = new List<string> {collection.GetFullPath()}},
                                   SortField.Name, SortingOrder.Ascending, 0, 0, token))
                {
                    collectionAssetIds.Add(assetIdentifier.AssetId);
                }

                if (collectionAssetIds.Count == 0)
                    return;

                m_EditorUtilityProxy.ClearProgressBar();

                assets = assets.Where(info => info.Identifier.ProjectId == collectionProjectId && collectionAssetIds.Contains(info.Identifier.AssetId));
            }
            else if (projectOrLibrary != null)
            {
                assets = assets.Where(info => info.Identifier.ProjectId == projectOrLibrary.Id);
            }

            await UpdateAllToLatestAsync_Internal(trigger, assets, token);
        }

        public Task UpdateAllToLatestAsync(ImportTrigger trigger, IEnumerable<BaseAssetData> assets, CancellationToken token)
        {
            assets = assets?.Where(a => m_AssetDataManager.ImportedAssetInfos.Any(i => i.Identifier == a.Identifier));
            return UpdateAllToLatestAsync_Internal(trigger, assets, token);
        }

        async Task UpdateAllToLatestAsync_Internal(ImportTrigger trigger, IEnumerable<BaseAssetData> assetDatas, CancellationToken token)
        {
            if (assetDatas == null || !assetDatas.Any())
                return;

            m_EditorUtilityProxy.DisplayProgressBar("Searching outdated assets...", string.Empty, 0f);

            var results = await m_AssetsProvider.GatherImportStatusesAsync(assetDatas, token);
            var outdatedAssets = assetDatas.Where(x => IsOutOfDate(x, results)).ToList();

            m_EditorUtilityProxy.ClearProgressBar();

            if (outdatedAssets.Count > 0)
            {
                await StartImportAsync(trigger, outdatedAssets, new ImportSettings {Type = ImportOperation.ImportType.UpdateToLatest}, token);
            }
        }

        static bool IsOutOfDate(BaseAssetData assetData, ImportStatuses importStatuses)
        {
            if (importStatuses.TryGetValue(assetData.Identifier, out var status))
            {
                return status == ImportAttribute.ImportStatus.OutOfDate;
            }

            return false;
        }

        public void ShowInProject(AssetIdentifier identifier)
        {
            try
            {
                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(identifier);

                if (importedInfo != null)
                {
                    // Order is from least usable to most usable; try to ping by matching cloud path first
                    var files = importedInfo.AssetData?
                        .GetFiles(d => d.CanBeImported)?
                        .FilterUsableFilesAsPrimaryExtensions()
                        .OrderBy(x => x, new AssetDataFileComparerByExtension())
                        .ToArray() ?? Array.Empty<AssetDataFile>();

                    for (var i = files.Length - 1; i >= 0; --i)
                    {
                        var fileInfo = importedInfo.FileInfos.Find(f => Utilities.ComparePaths(f.OriginalPath, files[i].Path));
                        if (fileInfo != null && m_AssetDatabaseProxy.PingAssetByGuid(fileInfo.Guid))
                            return;
                    }

                    // Fallback: ping by GUID without path matching (e.g. if paths diverged after move or "Import To")
                    foreach (var fileInfo in importedInfo.FileInfos)
                    {
                        if (fileInfo != null && !string.IsNullOrEmpty(fileInfo.Guid) && m_AssetDatabaseProxy.PingAssetByGuid(fileInfo.Guid))
                            return;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore
            }

            Debug.LogError("Unable to find asset location");
        }

        public void CancelImport(AssetIdentifier identifier, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr(AssetManagerCoreConstants.CancelImportActionText),
                    L10n.Tr("Are you sure you want to cancel?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            m_TokenSource?.Cancel();
            m_TokenSource?.Dispose();
            m_TokenSource = null;

            if (!m_ImportOperations.TryGetValue(identifier.AssetId, out var importOperation))
                return;

            foreach (var operation in m_ImportOperations.Values.ToList())
            {
                FinalizeImport(operation, OperationStatus.Cancelled);
            }
        }

        public void CancelBulkImport(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr(AssetManagerCoreConstants.CancelImportActionText),
                    L10n.Tr("Are you sure you want to cancel?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            m_TokenSource?.Cancel();

            var importOperations = identifiers
                .Where(identifier => m_ImportOperations.ContainsKey(identifier.AssetId))
                .Select(identifier => m_ImportOperations[identifier.AssetId])
                .ToList();

            // Cannot be done in the same loop because m_ImportOperations is clear after the first call to FinalizeImport
            foreach (var importOperation in importOperations)
            {
                FinalizeImport(importOperation, OperationStatus.Cancelled);
            }

            m_AssetOperationManager.ClearFinishedOperations();

            m_TokenSource?.Dispose();
            m_TokenSource = null;
        }

        public bool RemoveImports(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Assets"),
                    L10n.Tr("Remove the selected assets and all their exclusive dependencies?" + Environment.NewLine +
                            "Any changes you made to these assets will be lost."), L10n.Tr("Remove"),
                    L10n.Tr(AssetManagerCoreConstants.Cancel)))
            {
                return false;
            }

            return RemoveImportsInternal(identifiers);
        }

        public bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Asset"),
                    L10n.Tr("Remove the selected asset?" + Environment.NewLine +
                            "Any changes you made to this asset will be lost."), L10n.Tr("Remove"),
                    L10n.Tr(AssetManagerCoreConstants.Cancel)))
            {
                return false;
            }

            return RemoveImportsInternal(new List<AssetIdentifier> { identifier });
        }

        public void StopTrackingAssets(List<AssetIdentifier> identifiers)
        {
            m_AssetDataManager.RemoveImportedAssetInfo(identifiers);
        }

        bool RemoveImportsInternal(List<AssetIdentifier> identifiers)
        {
            try
            {
                var assetsAndFoldersToRemove = new HashSet<string>();

                foreach (var identifier in identifiers)
                {
                    var files = GetRemovablePathsFor(identifier);

                    if (files.Length == 0)
                    {
                        Utilities.DevLog($"Asset '{identifier.AssetId}' had no files to remove.");
                        continue;
                    }

                    foreach (var file in files)
                    {
                        if (!FileIsUsedByAnotherAsset(file, identifiers))
                        {
                            assetsAndFoldersToRemove.Add(file);
                        }
                        else
                        {
                            Utilities.DevLog($"File '{file}' is used by another imported asset and will not be removed.");
                        }
                    }
                }

                // Make sure to remove the asset imported info from memory before deleting the files
                StopTrackingAssets(identifiers);

                if (!assetsAndFoldersToRemove.Any()) // Nothing to remove, the asset was empty
                {
                    return true;
                }

                DeleteFilesAndFolders(assetsAndFoldersToRemove);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }

            return true;
        }

        bool FileIsUsedByAnotherAsset(string path, IEnumerable<AssetIdentifier> identifiers)
        {
            var guid = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().AssetPathToGuid(path);

            if (string.IsNullOrEmpty(guid))
                return false;

            var importedAssetInfos = m_AssetDataManager.GetImportedAssetInfosFromFileGuid(guid);

            if (importedAssetInfos == null || importedAssetInfos.Count == 0)
                return false;

            return importedAssetInfos.Exists(importedAssetInfo => !identifiers.Contains(importedAssetInfo.Identifier));
        }

        void DeleteFilesAndFolders(IEnumerable<string> assetsAndFoldersToRemove)
        {
            var pathsFailedToRemove = new List<string>();

            m_AssetDatabaseProxy.DeleteAssets(assetsAndFoldersToRemove.ToArray(), pathsFailedToRemove);

            if (!pathsFailedToRemove.Any())
                return;

            var errorMessage = L10n.Tr("Failed to remove the following asset(s) and/or folder(s):");
            errorMessage += Environment.NewLine + string.Join(Environment.NewLine, pathsFailedToRemove);

            Debug.LogError(errorMessage);
        }

        void FinalizeImport(ImportOperation importOperation, OperationStatus finalStatus)
        {
            importOperation.Finish(finalStatus);

            m_ImportOperations.Remove(importOperation.Identifier.AssetId);
        }

        async Task ProcessImports(IList<ImportOperation> imports, CancellationToken token)
        {
            bool hasErrors = false;
            foreach (var import in imports)
            {
                m_ImportOperations.Remove(import.Identifier.AssetId);
                hasErrors |= import.Status != OperationStatus.Success;
            }

            m_UriToDownloadOperationMap.Clear();

            List<Task<Dictionary<string, string>>> checksumTasks = new();
            foreach (var import in imports)
            {
                var checksumsTask = CalculateChecksumsAsync(import, token);
                checksumTasks.Add(checksumsTask);
            }
            await TaskUtils.WaitForTasksWithHandleExceptions(checksumTasks);

            Dictionary<string, string> checksums = new(); // downloadPath -> checksum
            foreach (var checksumTask in checksumTasks)
            {
                if (!checksumTask.IsCompletedSuccessfully)
                    continue;

                var checksumResult = checksumTask.Result;
                foreach (var kvp in checksumResult)
                {
                    checksums[kvp.Key] = kvp.Value;
                }
            }

            var filesToTrack = new Dictionary<ImportOperation, List<(string originalPath, string finalPath, string downloadPath)>>();

            // Create all required folders BEFORE StartAssetEditing (CreateFolder doesn't work inside StartAssetEditing block)
            foreach (var import in imports)
            {
                if (import.Status != OperationStatus.Success)
                    continue;

                var relocatedFiles = DetectRelocatedFiles(import);
                foreach (var (guid, (oldPath, newPath)) in relocatedFiles)
                {
                    var parentDir = Path.GetDirectoryName(newPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        EnsureFolderExists(parentDir);
                    }
                }
            }

            m_AssetDatabaseProxy.StartAssetEditing();
            m_AssetDatabaseProxy.ReleaseCachedFileHandles();

            try
            {
                foreach (var import in imports)
                {
                    if (import.Status != OperationStatus.Success)
                        continue;

                    var files = MoveImportedFiles(import);
                    filesToTrack[import] = files;
                }
            }
            finally
            {
                m_AssetDatabaseProxy.StopAssetEditing();
                m_AssetDatabaseProxy.Refresh();
            }

            var tasks = new List<Task>();
            foreach (var kvp in filesToTrack)
            {
                List<(string originalPath, string finalPath, string checksum)> assetPathsAndChecksum = new();
                foreach (var file in kvp.Value)
                {
                    checksums.TryGetValue(file.downloadPath, out string checksum); // checksum will be null if not found
                    assetPathsAndChecksum.Add((file.originalPath, file.finalPath, checksum));
                }

                tasks.Add(m_ImportedAssetsTracker.TrackAssets(assetPathsAndChecksum, kvp.Key.AssetData));
            }

            // Will report for errors but won't throw. To be redone better
            await TaskUtils.WaitForTasksWithHandleExceptions(tasks);

            // If there weren't any error, we can clear the import operations
            if (!hasErrors)
            {
                foreach (var import in imports)
                {
                    m_AssetOperationManager.ClearOperation(new TrackedAssetIdentifier(import.Identifier));
                }
            }

            m_TokenSource?.Dispose();
            m_TokenSource = null;
        }

        // Return a dict of downloadPath -> checksum
        async Task<Dictionary<string, string>> CalculateChecksumsAsync(ImportOperation importOperation, CancellationToken token)
        {
            var checksums = new Dictionary<string, string>();

            foreach (var downloadRequest in importOperation.DownloadRequests)
            {
                var downloadPath = downloadRequest.DownloadPath;
                string checksum = null;

                if (m_IOProxy.FileExists(downloadPath))
                {
                    checksum = await m_FileUtility.CalculateMD5ChecksumAsync(downloadPath, token);
                }

                checksums[downloadPath] = checksum;
            }

            return checksums;
        }

        List<(string originalPath, string finalPath, string downloadPath)> MoveImportedFiles(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath, string downloadPath)>();

            try
            {
                var cleanedUpAssets = new HashSet<AssetIdentifier>();

                // In case there are no download requests, we should do a direct clean-up the operation assets
                if (importOperation.DownloadRequests.Count == 0)
                {
                    CleanupAssetsAndLeftoverFolders(importOperation);
                }
                else
                {
                    // Build a map of relocated files (files that moved to a different folder between versions)
                    // This is needed to use MoveAsset instead of delete+copy, which preserves GUIDs
                    var relocatedFiles = DetectRelocatedFiles(importOperation);

                    foreach (var downloadRequest in importOperation.DownloadRequests)
                    {
                        var downloadPath = downloadRequest.DownloadPath;
                        var finalPath = Path.GetRelativePath(importOperation.TempDownloadPath, downloadPath).Replace("\\", "/");

                        if (m_IOProxy.FileExists(downloadPath))
                        {
                            // If multiple requests are targeting the same asset, we should only clean it up once, otherwise we run the risk of deleting newly downloaded files
                            if (cleanedUpAssets.Add(importOperation.Identifier))
                            {
                                // Cleanup files that were NOT relocated (i.e., files removed in new version).
                                // Relocated file are moved to ensure guid are preserved.a
                                CleanupAssetsAndLeftoverFolders(importOperation, excludePaths: relocatedFiles.Values.Select(x => x.oldPath));
                            }

                            // If this file was relocated, use MoveAsset to preserve GUIDs
                            // Note: Parent folders are created before StartAssetEditing (CreateFolder doesn't work inside StartAssetEditing block)
                            if(IsFileRelocated(finalPath, relocatedFiles))
                            {
                                var relocationInfo = relocatedFiles.Values.FirstOrDefault(r => r.newPath == finalPath);
                                if (!string.IsNullOrEmpty(relocationInfo.oldPath))
                                {
                                    var error = m_AssetDatabaseProxy.MoveAsset(relocationInfo.oldPath, relocationInfo.newPath);
                                    if (!string.IsNullOrEmpty(error))
                                    {
                                        Debug.LogWarning( $"Failed to move asset from '{relocationInfo.oldPath}' to '{relocationInfo.newPath}': {error}. Guid might be lost for {relocationInfo.newPath}");
                                        m_IOProxy.DeleteFile(relocationInfo.oldPath);
                                    }
                                }
                            }

                            try
                            {
                                // Delete existing file at final path (may be the moved file with old content, or nothing)
                                m_IOProxy.DeleteFile(finalPath);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"File: '{finalPath}' could not be deleted because of exception {ex.GetType().Name} with message: {ex.Message}");
                                throw;
                            }
                            
                            try
                            {
                                // Move downloaded file to final location
                                m_IOProxy.FileMove(downloadPath, finalPath);
							}
	                        catch (Exception ex)
	                        {
                                Debug.LogError($"File: '{downloadPath}' cannot be moved to '{finalPath}' because of exception {ex.GetType().Name} with message: {ex.Message}");
	                            throw;
	                        }

                            // Only refresh import of non-metadata files.
                            if (!finalPath.EndsWith(MetafilesHelper.MetaFileExtension))
                            {
                                m_AssetDatabaseProxy.ImportAsset(finalPath);
                            }
                        }

                        filesToTrack.Add((downloadRequest.OriginalPath, finalPath, downloadPath));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                // We might want to delete files that have already been moved
            }
            finally
            {
                m_IOProxy.DirectoryDelete(importOperation.TempDownloadPath, true);
            }

            return filesToTrack;
        }

        /// <summary>
        /// Detects files that have been relocated (moved to a different folder) between asset versions.
        /// These files need special handling via MoveAsset to preserve their GUIDs.
        /// </summary>
        /// <returns>Dictionary mapping GUID to (oldPath, newPath) for relocated files</returns>
        Dictionary<string, (string oldPath, string newPath)> DetectRelocatedFiles(ImportOperation importOperation)
        {
            var relocatedFiles = new Dictionary<string, (string oldPath, string newPath)>();

            foreach (var downloadRequest in importOperation.DownloadRequests)
            {
                var downloadPath = downloadRequest.DownloadPath;

                // Skip meta files - we process them along with their asset files
                if (downloadPath.EndsWith(MetafilesHelper.MetaFileExtension))
                    continue;

                // Check if there's a downloaded meta file for this asset
                var metaDownloadPath = downloadPath + MetafilesHelper.MetaFileExtension;
                if (!m_IOProxy.FileExists(metaDownloadPath))
                    continue;

                try
                {
                    // Read and parse the GUID from the downloaded meta file
                    var metaContent = m_IOProxy.FileReadAllText(metaDownloadPath);
                    var downloadedGuid = MetafilesHelper.ParseGuidFromMetaContent(metaContent);

                    if (string.IsNullOrEmpty(downloadedGuid))
                        continue;

                    // Check if this GUID exists in the project at a different path
                    var existingPath = m_AssetDatabaseProxy.GuidToAssetPath(downloadedGuid);
                    var newPath = Path.GetRelativePath(importOperation.TempDownloadPath, downloadPath);

                    // Normalize paths for comparison
                    existingPath = existingPath?.Replace('\\', '/');
                    newPath = newPath?.Replace('\\', '/');

                    if (!string.IsNullOrEmpty(existingPath) && m_IOProxy.FileExists(existingPath) && existingPath != newPath)
                    {
                        // This file has been relocated - it exists at a different path
                        relocatedFiles[downloadedGuid] = (existingPath, newPath);
                        Utilities.DevLog($"Detected relocated file: GUID={downloadedGuid}, '{existingPath}' -> '{newPath}'");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to detect relocation for '{downloadPath}': {e.Message}");
                }
            }

            return relocatedFiles;
        }

        /// <summary>
        /// Checks if a file path corresponds to a relocated file (or its meta file).
        /// </summary>
        bool IsFileRelocated(string finalPath, Dictionary<string, (string oldPath, string newPath)> relocatedFiles)
        {
            var pathWithoutMeta = MetafilesHelper.RemoveMetaExtension(finalPath);
            return relocatedFiles.Values.Any(r =>
                r.newPath == pathWithoutMeta ||
                r.newPath == finalPath);
        }

        void CleanupAssetsAndLeftoverFolders(ImportOperation importOperation, IEnumerable<string> excludePaths = null)
        {
            var exclusions = excludePaths != null ? new HashSet<string>(excludePaths) : null;

            var assetsAndFolders = GetRemovablePathsFor(importOperation.Identifier);
            foreach (var path in assetsAndFolders)
            {
                if (exclusions!= null && exclusions.Contains(path))
                    continue;

                m_IOProxy.DeleteFile(path, true);
                m_IOProxy.DeleteFile(path + MetafilesHelper.MetaFileExtension, true);
            }
        }

        static void SendImportAnalytics(ImportTrigger trigger, IEnumerable<BaseAssetData> allAssetData)
        {
            foreach (var assetData in allAssetData)
            {
                if (assetData != null)
                {
                    var fileCount = 0;
                    var fileExtension = string.Empty;

                    var fileExtensions = assetData.GetFiles()?.Select(adf => Path.GetExtension(adf.Path)) ?? Array.Empty<string>();
                    if (fileExtensions.Any())
                    {
                        fileCount = fileExtensions.Count();

                        fileExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(fileExtensions);
                        fileExtension = fileExtension?.TrimStart('.'); // remove the dot
                    }

                    // We only want to send the system tags of the datasets that can be imported
                    var systemTags = assetData.Datasets
                        .Where(d => d.CanBeImported)
                        .SelectMany(d => d.SystemTags)
                        .Where(t => !string.IsNullOrEmpty(t));

                    AnalyticsSender.SendEvent(new ImportEvent(trigger, assetData.Identifier.AssetId, fileCount, fileExtension, systemTags));
                }
            }
        }

        void EnsureFolderExists(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || m_AssetDatabaseProxy.IsValidFolder(folderPath))
            {
                return;
            }

            // Recursively ensure parent folder exists first
            var parentFolder = Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                EnsureFolderExists(parentFolder);
            }

            // Now create this folder using AssetDatabase
            var folderName = Path.GetFileName(folderPath);
            m_AssetDatabaseProxy.CreateFolder(parentFolder ?? string.Empty, folderName);
            m_AssetDatabaseProxy.ImportAsset(folderPath + "/");
        }

        string GetNonConflictingImportPath(string path)
        {
            if (!m_IOProxy.FileExists(path) && !m_IOProxy.DirectoryExists(path))
            {
                return path;
            }

            var extension = Path.GetExtension(path);
            var pathWithoutExtension = path[..^extension.Length];
            if (!string.IsNullOrEmpty(extension))
            {
                path = pathWithoutExtension;
            }

            var pattern = @"( \d+)$";
            var isPathEndsWithNumber = Regex.Match(path, pattern);
            var number = 0;
            var pathWithoutNumber = path;

            if (isPathEndsWithNumber.Success)
            {
                pathWithoutNumber = Regex.Replace(path, pattern, string.Empty).Trim();
                number = int.Parse(isPathEndsWithNumber.Value);
            }

            var pathToCheck = pathWithoutNumber;
            while (true)
            {
                if (number > 0)
                {
                    pathToCheck = $"{pathWithoutNumber} {number}";
                }

                if (m_IOProxy.FileExists(pathToCheck + extension) || m_IOProxy.DirectoryExists(pathToCheck + extension))
                {
                    number++;
                }
                else
                {
                    pathToCheck += extension;
                    return pathToCheck;
                }
            }
        }

        string GetDestinationPath(BaseAssetData assetData, string importDestination)
        {
            if (!m_SettingsManager.IsSubfolderCreationEnabled)
                return importDestination;

            return Path.Combine(importDestination, PathUtils.SanitizeAssetName(assetData.Name));
        }

        string GetDefaultDestinationPath(BaseAssetData assetData, string importDestination, out bool cancelImport)
        {
            cancelImport = false;
            var tempPath = m_IOProxy.GetUniqueTempPathInProject();
            var tempFilesToTrack = new List<string>();
            var destinationPath = GetDestinationPath(assetData, importDestination);

            var existingImport = m_AssetDataManager.GetImportedAssetInfo(assetData.Identifier);
            if (existingImport != null)
            {
                tempFilesToTrack = MoveImportToTempPath(assetData.Identifier, tempPath, importDestination);
            }

            if (existingImport != null)
            {
                MoveImportToAssetPath(tempPath, importDestination, tempFilesToTrack);
                m_IOProxy.DirectoryDelete(tempPath, true);
            }

            return destinationPath;
        }

        ImportOperation CreateImportOperation(BaseAssetData assetData, string importPath, bool forceDestination)
        {
            if (m_ImportOperations.TryGetValue(assetData.Identifier.AssetId, out var existingImportOperation))
            {
                return existingImportOperation;
            }

            var filePaths = new Dictionary<string, string>();

            // If the user hasn't forced the destination using "Import To", we try to find the imported paths
            if (!forceDestination)
            {
                var importedAssetInfo = m_AssetDataManager.GetImportedAssetInfo(assetData.Identifier);

                if (importedAssetInfo != null)
                {
                    importedAssetInfo.FileInfos.ForEach(f =>
                    {
                        var path = m_AssetDatabaseProxy.GuidToAssetPath(f.Guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            filePaths[f.OriginalPath] = path;
                        }
                    });
                }
            }

            var importOperation = new ImportOperation(assetData, m_IOProxy.GetUniqueTempPathInProject(), filePaths, importPath, m_SettingsManager);

            m_AssetOperationManager.RegisterOperation(importOperation);

            m_ImportOperations[importOperation.Identifier.AssetId] = importOperation;
            return importOperation;
        }

        async Task StartImport(ImportOperation importOperation, CancellationToken token)
        {
            importOperation.Start();

            try
            {
                m_IOProxy.CreateDirectory(importOperation.TempDownloadPath);
                await importOperation.ImportAsync(token);
            }
            catch (OperationCanceledException)
            {
                importOperation.Finish(OperationStatus.Cancelled);
                throw;
            }
            catch (Exception e)
            {
                importOperation.Finish(OperationStatus.Error);
                Debug.LogException(e);
                throw;
            }
        }

        static async Task StartDownloads(ImportOperation importOperation)
        {
            try
            {
                await importOperation.StartDownloadRequests();
            }
            catch (OperationCanceledException)
            {
                importOperation.Finish(OperationStatus.Cancelled);
                throw;
            }
            catch (Exception e)
            {
                importOperation.Finish(OperationStatus.Error);
                Debug.LogException(e);
                throw;
            }
        }

        public UnityWebRequestAsyncOperation SendWebRequest(string url, string path, bool append = false,
            string bytesRange = null)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
            {
                disposeDownloadHandlerOnDispose = true
            };

            if (!string.IsNullOrWhiteSpace(bytesRange))
            {
                request.SetRequestHeader("Range", bytesRange);
            }

            request.downloadHandler = new DownloadHandlerFile(path, append) { removeFileOnAbort = true };
            return request.SendWebRequest();
        }

        async Task<bool> Import(ImportTrigger importTrigger, IEnumerable<BaseAssetData> assetDataList, ImportSettingsInternal importSettings, CancellationToken token)
        {
            if (!assetDataList.Any())
            {
                return false;
            }

            var importOperations = new List<ImportOperation>();

            foreach (var assetData in assetDataList)
            {
                if (m_ImportOperations.Any(x => x.Key.Equals(assetData.Identifier.AssetId)))
                {
                    Utilities.DevLogError("Dupes");
                    continue; // Dupes, I'm not sure why distinct is not working properly
                }

                var path = GetDefaultDestinationPath(assetData, importSettings.ImportPath, out var cancelImport);

                if (cancelImport)
                {
                    return false;
                }

                var importOperation = CreateImportOperation(assetData, path, !importSettings.IsUsingDefaultImportPath);
                importOperations.Add(importOperation);
            }

            var processImportTask = new TaskCompletionSource<bool>();

            m_BulkImportOperation?.Remove();
            m_BulkImportOperation = new BulkImportOperation(importOperations, importTrigger);
            m_BulkImportOperation.Finished += async status =>
            {
                Utilities.DevLog($"Bulk import finished with status: {status}");
                await TaskUtils.WaitForTaskWithHandleExceptions(ProcessImports(importOperations, token));
                processImportTask.TrySetResult(status != OperationStatus.Success);
            };

            m_BulkImportOperation.Start();

            await TaskUtils.RunAllTasksBatched(importOperations, importOperation => StartImport(importOperation, token));
            await TaskUtils.RunAllTasksBatched(importOperations, importOperation => StartDownloads(importOperation));

            await processImportTask.Task; // Wait for the imports to be processed before returning
            return true;
        }

        /// <summary>
        /// Returns file and folder paths that can be safely deleted when removing an imported asset.
        /// This includes the asset's files AND any parent folders that would become empty after deletion.
        /// Folders containing other assets (from different imports or user-created) are preserved.
        /// </summary>
        /// <param name="identifier">The identifier of the imported asset to remove.</param>
        /// <returns>
        /// Array of paths to delete, ordered with files first, then folders deepest-first
        /// (so child folders are deleted before their parents).
        /// </returns>
        string[] GetRemovablePathsFor(AssetIdentifier identifier)
        {
            var fileInfos = m_AssetDataManager.GetImportedAssetInfo(identifier)?.FileInfos;
            fileInfos ??= new List<ImportedFileInfo>();

            try
            {
                const string assetsPath = "Assets";

                // Step 1: Get all file paths belonging to this imported asset
                var filesToRemove = fileInfos.Select(fileInfo => m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.Guid))
                    .Where(f => !string.IsNullOrEmpty(f)).OrderByDescending(p => p).ToList();

                // Step 2: Collect all parent folders up to "Assets/" as candidates for removal.
                // These folders MIGHT become empty after we delete the files.
                var foldersToRemove = new HashSet<string>();
                foreach (var file in filesToRemove)
                {
                    var path = Path.GetDirectoryName(file);
                    while (!string.IsNullOrEmpty(path) && !foldersToRemove.Contains(path) &&
                           path.StartsWith(assetsPath) && path.Length > assetsPath.Length)
                    {
                        foldersToRemove.Add(path);
                        path = Path.GetDirectoryName(path);
                    }
                }

                // Step 3: Find assets that will REMAIN after deletion (not part of this import).
                // These are assets in the candidate folders that belong to other imports or were user-created.
                var assetsRemainingAfterDeletion =
                    m_AssetDatabaseProxy.FindAssets(string.Empty, foldersToRemove.ToArray()).ToHashSet();

                // Remove from the set: files we're deleting + the folders themselves (they have GUIDs too)
                foreach (var guid in fileInfos.Select(i => i.Guid)
                             .Concat(foldersToRemove.Select(ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().AssetPathToGuid)))
                {
                    assetsRemainingAfterDeletion.Remove(guid);
                }

                // Step 4: Protect folders that contain remaining assets.
                // For each remaining asset, remove its parent folders from the deletion list.
                foreach (var assetPath in assetsRemainingAfterDeletion.Select(i => m_AssetDatabaseProxy.GuidToAssetPath(i)))
                {
                    var path = Path.GetDirectoryName(assetPath);

                    if (!foldersToRemove.Any())
                        break;

                    while (foldersToRemove.Contains(path))
                    {
                        foldersToRemove.Remove(path);
                        path = Path.GetDirectoryName(path);
                    }
                }

                // Return files + folders, with folders ordered deepest-first so children are deleted before parents
                return filesToRemove.Concat(foldersToRemove.OrderByDescending(i => i)).ToArray();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        public List<string> MoveImportToTempPath(AssetIdentifier identifier, string tempPath, string assetPath)
        {
            try
            {
                var assetsAndFolders = GetRemovablePathsFor(identifier);
                if (!assetsAndFolders.Any())
                {
                    return new List<string>();
                }

                if (!m_IOProxy.DirectoryExists(tempPath))
                {
                    m_IOProxy.CreateDirectory(tempPath);
                }

                var filesToMove = new List<string>();
                var foldersToRemove = new List<string>();
                foreach (var path in assetsAndFolders)
                {
                    if (m_IOProxy.FileExists(path))
                    {
                        filesToMove.Add(path);
                    }
                    else if (m_IOProxy.DirectoryExists(path))
                    {
                        foldersToRemove.Add(path);
                    }

                    var metaFile = path + ".meta";
                    if (m_IOProxy.FileExists(metaFile))
                    {
                        filesToMove.Add(metaFile);
                    }
                }

                var filesToTrack = new List<string>();
                foreach (var file in filesToMove)
                {
                    var originalPath = Path.GetRelativePath(assetPath, file);
                    var finalPath = Path.Combine(tempPath, originalPath);
                    if (m_IOProxy.FileExists(file))
                    {
                        m_IOProxy.FileMove(file, finalPath);
                    }

                    filesToTrack.Add(finalPath);
                }

                foreach (var path in foldersToRemove)
                {
                    m_IOProxy.DirectoryDelete(path, true);
                }

                return filesToTrack;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        public void MoveImportToAssetPath(string tempPath, string assetPath, List<string> filesToTrack)
        {
            if (filesToTrack.Count == 0)
                return;

            try
            {
                foreach (var file in filesToTrack)
                {
                    var originalPath = Path.GetRelativePath(tempPath, file);
                    var finalPath = Path.Combine(assetPath, originalPath);
                    m_IOProxy.FileMove(file, finalPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        void OnFinishedOperationsCleared()
        {
            if (m_BulkImportOperation == null)
                return;

            if (m_BulkImportOperation.Status == OperationStatus.Success)
            {
                m_BulkImportOperation.Remove();
                m_BulkImportOperation = null;
            }
        }

        bool IsDebugLogEnabled()
        {
            return m_SettingsManager != null && m_SettingsManager.IsDebugLogsEnabled;
        }
    }
}
