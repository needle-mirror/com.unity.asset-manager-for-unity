using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
        Task<IEnumerable<BaseAssetData>> StartImportAsync(List<BaseAssetData> assets, ImportOperation.ImportType importType, string importDestination = null);
        Task UpdateAllToLatestAsync(ProjectInfo project, CollectionInfo collection,  CancellationToken token);
        Task UpdateAllToLatestAsync(IEnumerable<BaseAssetData> assets, CancellationToken token);
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
        IProgressManager m_ProgressManager;

        readonly Dictionary<string, ImportOperation> m_ImportOperations = new();
        readonly Dictionary<Uri, DownloadOperation> m_UriToDownloadOperationMap = new();

        const int k_MaxConcurrentDownloads = 10;
        static readonly SemaphoreSlim k_DownloadFileSemaphore = new(k_MaxConcurrentDownloads);

        CancellationTokenSource m_TokenSource;
        BulkImportOperation m_BulkImportOperation;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker,
            IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager,
            ISettingsManager settingsManager, IAssetsProvider assetsProvider, IAssetImportResolver assetImportResolver,
            IProgressManager progressManager)
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
            m_ProgressManager = progressManager;
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

        public async Task<IEnumerable<BaseAssetData>> StartImportAsync(List<BaseAssetData> assets, ImportOperation.ImportType importType, string importDestination = null)
        {
            SendImportAnalytics(assets);

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

                m_TokenSource = new CancellationTokenSource();
                var token = m_TokenSource.Token;

                BaseAssetData[] resolutions;
                try
                {
                    var result = await m_Resolver.Resolve(assets,
                        importType,
                        importDestination ?? m_SettingsManager.DefaultImportLocation,
                        token);
                    resolutions = result?.ToArray() ?? Array.Empty<BaseAssetData>();

                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    processingOperation.Finish(OperationStatus.Success);
                    foreach (var operation in syncWithCloudOperations)
                    {
                        operation.Finish(OperationStatus.Success);
                    }
                }

                if (resolutions.Length > 0)
                {
                    if (Import(resolutions, importDestination, token))
                    {
                        // Isolate the assets to those from the original list;
                        // the versions may have changed so only compare part of the AssetIdentifier
                        return resolutions.Where(x => assets.Any(y =>
                            y.Identifier.OrganizationId == x.Identifier.OrganizationId &&
                            y.Identifier.ProjectId == x.Identifier.ProjectId &&
                            y.Identifier.AssetId == x.Identifier.AssetId));
                    }

                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task UpdateAllToLatestAsync(ProjectInfo project, CollectionInfo collection, CancellationToken token)
        {
            var insideCollection = collection != null && !string.IsNullOrEmpty(collection.Name);

            var assets = m_AssetDataManager.ImportedAssetInfos.Select(info => info.AssetData);

            if (insideCollection)
            {
                var collectionProjectId = collection.ProjectId;
                var collectionAssetIds = new HashSet<string>();

                m_ProgressManager.Start($"Getting assets from collection {collection.GetFullPath()}");

                // Get assets from the collection
                await foreach (var assetIdentifier in m_AssetsProvider.SearchLiteAsync(collection.OrganizationId,
                                   new List<string> { collectionProjectId },
                                   new AssetSearchFilter { Collection = collection.GetFullPath() },
                                   SortField.Name, SortingOrder.Ascending, 0, 0, token))
                {
                    collectionAssetIds.Add(assetIdentifier.AssetId);
                }

                if (collectionAssetIds.Count == 0)
                    return;

                m_ProgressManager.Stop();

                assets = assets.Where(info => info.Identifier.ProjectId == collectionProjectId && collectionAssetIds.Contains(info.Identifier.AssetId));
            }
            else if (project != null)
            {
                assets = assets.Where(info => info.Identifier.ProjectId == project.Id);
            }

            await UpdateAllToLatestAsync_Internal(assets, token);
        }

        public Task UpdateAllToLatestAsync(IEnumerable<BaseAssetData> assets, CancellationToken token)
        {
            assets = assets?.Where(a => m_AssetDataManager.ImportedAssetInfos.Any(i => i.Identifier == a.Identifier));
            return UpdateAllToLatestAsync_Internal(assets, token);
        }

        async Task UpdateAllToLatestAsync_Internal(IEnumerable<BaseAssetData> assetDatas, CancellationToken token)
        {
            if (assetDatas == null || !assetDatas.Any())
                return;

            m_ProgressManager.Start("Searching outdated assets...");

            await m_AssetsProvider.UpdateImportStatusAsync(assetDatas, token);

            var outdatedAssets = assetDatas.Where(x =>
                x.AssetDataAttributeCollection?.GetAttribute<ImportAttribute>().Status ==
                ImportAttribute.ImportStatus.OutOfDate).ToList();

            m_ProgressManager.Stop();

            if (outdatedAssets.Count > 0)
            {
                await StartImportAsync(outdatedAssets, ImportOperation.ImportType.UpdateToLatest);
            }
        }

        public void ShowInProject(AssetIdentifier identifier)
        {
            try
            {
                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(identifier);
                var primarySourceFile = importedInfo.AssetData.PrimarySourceFile;

                if (primarySourceFile != null)
                {
                    var fileInfo = importedInfo.FileInfos.Find(f => Utilities.ComparePaths(f.OriginalPath, primarySourceFile.Path));
                    m_AssetDatabaseProxy.PingAssetByGuid(fileInfo.Guid);
                }
            }
            catch (Exception)
            {
                Debug.LogError("Unable to find asset location");
            }
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

            FinalizeImport(importOperation, OperationStatus.Cancelled);
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
                    L10n.Tr("Remove the selected assets and all their exclusives dependencies?" + Environment.NewLine +
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
                    var files = FindAssetsAndLeftoverFolders(identifier);

                    if (files.Length == 0)
                    {
                        Debug.LogWarning(
                            $"Asset with Id '{identifier.AssetId}' was removed, but no files were deleted because they weren't found in the project.");
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

                if (!assetsAndFoldersToRemove.Any())
                {
                    return false;
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

        void ProcessImports(IList<ImportOperation> imports)
        {
            foreach (var import in imports)
            {
                m_ImportOperations.Remove(import.Identifier.AssetId);
            }

            m_UriToDownloadOperationMap.Clear();

            var filesToTrack = new Dictionary<ImportOperation, List<(string originalPath, string finalPath)>>();
            m_AssetDatabaseProxy.StartAssetEditing();

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
                tasks.Add(m_ImportedAssetsTracker.TrackAssets(kvp.Value, kvp.Key.AssetData));
            }
            TaskUtils.TrackException(Task.WhenAll(tasks));

            m_TokenSource?.Dispose();
            m_TokenSource = null;
        }

        List<(string originalPath, string finalPath)> MoveImportedFiles(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath)>();

            try
            {
                var cleanedUpAssets = new HashSet<AssetIdentifier>();

                foreach (var downloadRequest in importOperation.DownloadRequests)
                {
                    var downloadPath = downloadRequest.DownloadPath;

                    var finalPath = Path.GetRelativePath(importOperation.TempDownloadPath, downloadPath);

                    if (File.Exists(downloadPath))
                    {
                        // If multiple requests are targeting the same asset, we should only clean it up once, otherwise we run the risk of deleting newly downloaded files
                        if (cleanedUpAssets.Add(importOperation.Identifier))
                        {
                            var assetsAndFolders = FindAssetsAndLeftoverFolders(importOperation.Identifier);
                            foreach (var path in assetsAndFolders)
                            {
                                m_IOProxy.DeleteFile(path, true);
                                m_IOProxy.DeleteFile(path + MetafilesHelper.MetaFileExtension, true);
                            }
                        }

                        m_IOProxy.DeleteFile(finalPath);
                        m_IOProxy.FileMove(downloadPath, finalPath);

                        // Only refresh import of non-metadata files.
                        if (!finalPath.EndsWith(MetafilesHelper.MetaFileExtension))
                        {
                            m_AssetDatabaseProxy.ImportAsset(finalPath);
                        }
                    }

                    filesToTrack.Add((downloadRequest.OriginalPath, finalPath));
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

        void SendImportAnalytics(IEnumerable<BaseAssetData> allAssetData)
        {
            foreach (var assetData in allAssetData)
            {
                if (assetData != null)
                {
                    int fileCount = 0;
                    string fileExtension = string.Empty;

                    if (assetData.SourceFiles != null
                        && assetData.SourceFiles.Any())
                    {
                        fileCount = assetData.SourceFiles.Count();

                        var extensions = assetData.SourceFiles.Select(adf => Path.GetExtension(adf.Path));
                        fileExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions);
                        fileExtension = fileExtension?.TrimStart('.'); // remove the dot
                    }

                    AnalyticsSender.SendEvent(new ImportEvent(assetData.Identifier.AssetId, fileCount, fileExtension));
                }
            }
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

            return Path.Combine(importDestination,
                $"{Regex.Replace(assetData.Name, @"[\\\/:*?""<>|]", "").Trim()}");
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

            // If the user haven't forced the destination using "Import To", we try to find the imported paths
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

            var importOperation = new ImportOperation(assetData, m_IOProxy.GetUniqueTempPathInProject(), filePaths, importPath);

            m_AssetOperationManager.RegisterOperation(importOperation);

            m_ImportOperations[importOperation.Identifier.AssetId] = importOperation;
            return importOperation;
        }

        async Task StartImport(ImportOperation importOperation, CancellationToken token)
        {
            importOperation.Start();

            await k_DownloadFileSemaphore.WaitAsync(token);

            try
            {
                m_IOProxy.CreateDirectory(importOperation.TempDownloadPath);
                await importOperation.ImportAsync(token);
            }
            catch (TaskCanceledException)
            {
                importOperation.Finish(OperationStatus.Cancelled);
                throw;
            }
            catch (Exception)
            {
                importOperation.Finish(OperationStatus.Error);
                throw;
            }
            finally
            {
                k_DownloadFileSemaphore.Release();
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

        bool Import(IEnumerable<BaseAssetData> assetDataList, string importDestination, CancellationToken token)
        {
            if (!assetDataList.Any())
            {
                return false;
            }

            var forceDestination = false;
            if (importDestination == null)
            {
                importDestination = m_SettingsManager.DefaultImportLocation;
            }
            else
            {
                forceDestination = true;
            }

            var importOperations = new List<ImportOperation>();

            foreach (var assetData in assetDataList)
            {
                if (m_ImportOperations.Any(x => x.Key.Equals(assetData.Identifier.AssetId)))
                {
                    Utilities.DevLogError("Dupes");
                    continue; // Dupes, I'm not sure why distinct is not working properly
                }

                var path = GetDefaultDestinationPath(assetData, importDestination, out var cancelImport);

                if (cancelImport)
                {
                    return false;
                }

                var importOperation = CreateImportOperation(assetData, path, forceDestination);
                importOperations.Add(importOperation);
            }

            m_BulkImportOperation?.Remove();
            m_BulkImportOperation = new BulkImportOperation(importOperations);
            m_BulkImportOperation.Finished += _ =>
            {
                ProcessImports(importOperations);
            };

            m_BulkImportOperation.Start();

            foreach (var importOperation in importOperations)
            {
                TaskUtils.TrackException(StartImport(importOperation, token));
            }

            return true;
        }

        string[] FindAssetsAndLeftoverFolders(AssetIdentifier identifier)
        {
            var fileInfos = m_AssetDataManager.GetImportedAssetInfo(identifier)?.FileInfos;
            if (fileInfos == null || fileInfos.Count == 0)
            {
                return Array.Empty<string>();
            }

            try
            {
                const string assetsPath = "Assets";
                var filesToRemove = fileInfos.Select(fileInfo => m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.Guid))
                    .Where(f => !string.IsNullOrEmpty(f)).OrderByDescending(p => p).ToList();
                var foldersToRemove = new HashSet<string>();
                foreach (var file in filesToRemove)
                {
                    var path = Path.GetDirectoryName(file);

                    // We want to add an asset's parent folders all the way up to the `Assets` folder, because we don't want to leave behind
                    // empty folders after the assets are removed process.
                    while (!string.IsNullOrEmpty(path) && !foldersToRemove.Contains(path) &&
                           path.StartsWith(assetsPath) && path.Length > assetsPath.Length)
                    {
                        foldersToRemove.Add(path);
                        path = Path.GetDirectoryName(path);
                    }
                }

                var leftOverAssetsGuids =
                    m_AssetDatabaseProxy.FindAssets(string.Empty, foldersToRemove.ToArray()).ToHashSet();
                foreach (var guid in fileInfos.Select(i => i.Guid)
                             .Concat(foldersToRemove.Select(ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().AssetPathToGuid)))
                {
                    leftOverAssetsGuids.Remove(guid);
                }

                foreach (var assetPath in leftOverAssetsGuids.Select(i => m_AssetDatabaseProxy.GuidToAssetPath(i)))
                {
                    var path = Path.GetDirectoryName(assetPath);

                    if (!foldersToRemove.Any())
                        break;

                    // If after the removal process, there will still be some assets left behind, we want to make sure the folders containing
                    // left over assets are not removed
                    while (foldersToRemove.Contains(path))
                    {
                        foldersToRemove.Remove(path);
                        path = Path.GetDirectoryName(path);
                    }
                }

                // We order the folders to be removed so that child folders always come before their parent folders
                // This way DeleteAssets call won't try to remove parent folders first and fail to remove child folders
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
                var assetsAndFolders = FindAssetsAndLeftoverFolders(identifier);
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
                    if (File.Exists(file))
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
    }
}
