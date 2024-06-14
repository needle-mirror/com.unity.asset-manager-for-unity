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

namespace Unity.AssetManager.Editor
{
    interface IAssetImporter : IService
    {
        ImportOperation GetImportOperation(AssetIdentifier identifier);
        bool IsImporting(AssetIdentifier identifier);
        Task<bool> StartImportAsync(List<IAssetData> assetData, ImportOperation.ImportType importType, string importDestination = null);
        bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false);
        bool RemoveBulkImport(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false);
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
            return AssetDatabase.GetTextMetaFilePathFromAssetPath(fileName);
        }
    }

    [Serializable]
    class AssetImporter : BaseService<IAssetImporter>, IAssetImporter
    {
        [Serializable]
        class PendingImport
        {
            [SerializeReference]
            public IAssetData AssetData;
        }

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

        readonly Dictionary<string, ImportOperation> m_ImportOperations = new();
        readonly Dictionary<Uri, DownloadOperation> m_UriToDownloadOperationMap = new();
        readonly Dictionary<string, string> m_ResolvedDestinationPaths = new();

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker,
            IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager,
            ISettingsManager settingsManager)
        {
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_EditorUtilityProxy = editorUtilityProxy;
            m_ImportedAssetsTracker = importedAssetsTracker;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
            m_SettingsManager = settingsManager;
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

        public async Task<bool> StartImportAsync(List<IAssetData> assetData, ImportOperation.ImportType importType, string importDestination = null)
        {
            SendImportAnalytics(assetData);

            // Hack Create a temporary import operation to track the (long) dependency loading
            var tasks = new List<Task<List<IAssetData>>>();
            foreach (var asset in assetData)
            {
                if (m_ImportOperations.ContainsKey(asset.Identifier.AssetId))
                    continue;

                if (m_AssetDataManager.IsInProject(asset.Identifier) && !m_EditorUtilityProxy.DisplayDialog(
                        L10n.Tr("Overwrite Imported Files"),
                        L10n.Tr(
                            "All the files previously imported will be overwritten and changes to those files will be lost. Are you sure you want to continue?"),
                        L10n.Tr("Yes"), L10n.Tr("No")))
                {
                    continue;
                }

                tasks.Add(ImportAssetAsync(asset, importType, CancellationToken.None));
            }

            await Task.WhenAll(tasks);

            var allAssetData = tasks.SelectMany(x => x.Result).ToHashSet();

            var pendingImports = allAssetData.Select(x =>
                new PendingImport
                {
                    AssetData = x
                }).ToList();

            return Import(pendingImports, importDestination ?? m_SettingsManager.DefaultImportLocation, CancellationToken.None);
        }

        public void ShowInProject(AssetIdentifier identifier)
        {
            try
            {
                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(identifier);

                var fileInfo = importedInfo.FileInfos[0];

                m_AssetDatabaseProxy.PingAssetByGuid(fileInfo.Guid);
            }
            catch (Exception)
            {
                Debug.LogError("Unable to find asset location");
            }
        }

        public void CancelImport(AssetIdentifier identifier, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr(Constants.CancelImportActionText),
                    L10n.Tr("Are you sure you want to cancel?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            if (!m_ImportOperations.TryGetValue(identifier.AssetId, out var importOperation))
                return;

            FinalizeImport(importOperation, OperationStatus.Cancelled);
        }

        public void CancelBulkImport(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr(Constants.CancelImportActionText),
                    L10n.Tr("Are you sure you want to cancel?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            foreach (var identifier in identifiers)
            {
                if (!m_ImportOperations.TryGetValue(identifier.AssetId, out var importOperation))
                    continue;

                FinalizeImport(importOperation, OperationStatus.Cancelled);
            }
        }

        public bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Asset"),
                    L10n.Tr("Remove the selected asset?" + Environment.NewLine +
                        "Any changes you made to this asset will be lost."), L10n.Tr("Remove"), L10n.Tr("Cancel")))
            {
                return false;
            }

            try
            {
                // Untrack asset even if there is no files locally
                m_ImportedAssetsTracker.UntrackAsset(identifier);

                var assetsAndFoldersToRemove = FindAssetsAndLeftoverFolders(identifier);
                if (!assetsAndFoldersToRemove.Any())
                {
                    m_AssetDataManager.RemoveImportedAssetInfo(identifier);
                    Debug.LogWarning(
                        "Asset was removed, but no files were deleted because they weren't found in the project.");
                    return false;
                }

                var pathsFailedToRemove = new List<string>();

                m_AssetDatabaseProxy.DeleteAssets(assetsAndFoldersToRemove, pathsFailedToRemove);

                if (pathsFailedToRemove.Any())
                {
                    var errorMessage = L10n.Tr("Failed to remove the following asset(s) and/or folder(s):");
                    foreach (var path in pathsFailedToRemove)
                    {
                        errorMessage += "\n" + path;
                    }

                    Debug.LogError(errorMessage);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }

            return true;
        }

        public bool RemoveBulkImport(List<AssetIdentifier> identifiers, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Assets"),
                    L10n.Tr("Remove the selected assets?" + Environment.NewLine +
                        "Any changes you made to those assets will be lost."), L10n.Tr("Remove"), L10n.Tr("Cancel")))
            {
                return false;
            }

            foreach (var identifier in identifiers)
            {
                RemoveImport(identifier, false);
            }

            return true;
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

            foreach (var kvp in filesToTrack)
            {
                m_ImportedAssetsTracker.TrackAssets(kvp.Value, kvp.Key.AssetData);
            }
        }

        List<(string originalPath, string finalPath)> MoveImportedFiles(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath)>();

            try
            {
                var cleanedUpAssets = new HashSet<AssetIdentifier>();

                foreach (var downloadRequest in importOperation.DownloadRequests)
                {
                    var originalPath = Path.GetRelativePath(importOperation.TempDownloadPath, downloadRequest.Key);
                    var keyToUse = originalPath;
                    var isMetaFile = MetafilesHelper.IsMetafile(originalPath);
                    if (isMetaFile)
                    {
                        keyToUse = MetafilesHelper.RemoveMetaExtension(originalPath);
                    }

                    var finalPath = Path.Combine(importOperation.DestinationPath, keyToUse);

                    if (isMetaFile)
                    {
                        finalPath += MetafilesHelper.MetaFileExtension;
                    }

                    if (File.Exists(downloadRequest.Key))
                    {
                        // If multiple requests are targeting the same asset, we should only clean it up once, otherwise we run the risk of deleting newly downloaded files
                        if (cleanedUpAssets.Add(importOperation.Identifier))
                        {
                            var assetsAndFolders = FindAssetsAndLeftoverFolders(importOperation.Identifier);
                            foreach (var path in assetsAndFolders)
                            {
                                m_IOProxy.DeleteFileIfExists(path, true);
                                m_IOProxy.DeleteFileIfExists(path + MetafilesHelper.MetaFileExtension, true);
                            }
                        }

                        m_IOProxy.DeleteFileIfExists(finalPath);
                        m_IOProxy.FileMove(downloadRequest.Key, finalPath);
                    }

                    filesToTrack.Add((originalPath, finalPath));
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

        void SendImportAnalytics(IEnumerable<IAssetData> allAssetData)
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
                        fileExtension = fileExtension?.Substring(1); // remove the dot
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

        string GetDestinationPath(IAssetData assetData, string importDestination)
        {
            if (!m_SettingsManager.IsSubfolderCreationEnabled)
                return importDestination;

            return Path.Combine(importDestination,
                $"{Regex.Replace(assetData.Name, @"[\\\/:*?""<>|]", "").Trim()}");
        }

        string GetDefaultDestinationPath(IAssetData assetData, string importDestination, out bool cancelImport)
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

            // Check if we already asked the user for this path
            if (m_ResolvedDestinationPaths.TryGetValue(destinationPath, out var resolvedPath))
            {
                destinationPath = resolvedPath;
            }
            else
            {
                destinationPath = AskUserForDestinationPath(destinationPath, out cancelImport);
                m_ResolvedDestinationPaths[destinationPath] = destinationPath;
            }

            if (existingImport != null)
            {
                MoveImportToAssetPath(tempPath, importDestination, tempFilesToTrack);
                m_IOProxy.DirectoryDelete(tempPath, true);
            }

            return destinationPath;
        }

        string AskUserForDestinationPath(string defaultDestinationPath, out bool cancelImport)
        {
            var destinationPath = defaultDestinationPath;
            cancelImport = false;

            var nonConflictingImportPath = GetNonConflictingImportPath(defaultDestinationPath);
            if (defaultDestinationPath != nonConflictingImportPath)
            {
                var destinationFolderName = new DirectoryInfo(defaultDestinationPath).Name;
                var newPathFolderName = new DirectoryInfo(nonConflictingImportPath).Name;
                var title = L10n.Tr("Import conflict");

                if (m_IOProxy.FileExists(defaultDestinationPath))
                {
                    var message = string.Format(L10n.Tr("A file named '{0}' already exists." +
                            "\nDo you want to create a new folder named '{1}' and import the selected assets into that folder?"),
                        destinationFolderName, newPathFolderName);

                    if (m_EditorUtilityProxy.DisplayDialog(title, message, L10n.Tr("Create new folder"),
                            L10n.Tr("Cancel")))
                    {
                        m_ResolvedDestinationPaths[defaultDestinationPath] = nonConflictingImportPath;
                        destinationPath = nonConflictingImportPath;
                    }

                    cancelImport = true;
                }
                else if (destinationFolderName != "Assets")
                {
                    var message = string.Format(L10n.Tr("A folder named '{0}' already exists." +
                            "\nDo you want to continue with the import and rename any conflicting files in '{0}'?" +
                            "\nOr do you want to create a new folder named '{1}' and import the selected assets into that folder?"),
                        destinationFolderName, newPathFolderName);

                    switch (m_EditorUtilityProxy.DisplayDialogComplex(title, message, L10n.Tr("Continue"),
                                L10n.Tr("Create new folder"), L10n.Tr("Cancel")))
                    {
                        case 1:
                            m_ResolvedDestinationPaths[defaultDestinationPath] = nonConflictingImportPath;
                            destinationPath = nonConflictingImportPath;
                            break;
                        case 2:
                            cancelImport = true;
                            break;
                    }
                }
            }

            return destinationPath;
        }

        ImportOperation CreateImportOperation(IAssetData assetData, string importPath)
        {
            if (m_ImportOperations.TryGetValue(assetData.Identifier.AssetId, out var existingImportOperation))
            {
                return existingImportOperation;
            }

            var importOperation = new ImportOperation(assetData)
            {
                DestinationPath = importPath,
                StartTime = DateTime.Now,
                TempDownloadPath = m_IOProxy.GetUniqueTempPathInProject()
            };

            m_AssetOperationManager.RegisterOperation(importOperation);

            m_ImportOperations[importOperation.Identifier.AssetId] = importOperation;
            return importOperation;
        }

        async Task StartImport(ImportOperation importOperation, CancellationToken token)
        {
            m_IOProxy.CreateDirectory(importOperation.TempDownloadPath);

            await importOperation.ImportAsync(token);
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

        async Task<List<IAssetData>> ImportAssetAsync(IAssetData asset, ImportOperation.ImportType importType, CancellationToken token)
        {
            // Sync before fetching dependencies to ensure you have the latest dependencies
            switch (importType)
            {
                case ImportOperation.ImportType.Import:
                    await asset.SyncWithCloudAsync(null, token);
                    break;

                default:
                    await asset.SyncWithCloudLatestAsync(null, token);
                    break;
            }

            // Fetch dependencies
            return await GetDependenciesAsync(asset);
        }

        async Task<List<IAssetData>> GetDependenciesAsync(IAssetData asset)
        {
            var allAssetData = new List<IAssetData>();
            var dependencyCollectionOperation = new ImportOperation(asset);
            m_AssetOperationManager.RegisterOperation(dependencyCollectionOperation);
            dependencyCollectionOperation.Start();
            allAssetData.Add(asset);
            await foreach (var dependencyAssetData in AssetDataDependencyHelper.LoadDependenciesAsync(asset,
                               true, CancellationToken.None))
            {
                if (dependencyAssetData.AssetData != null)
                {
                    allAssetData.Add(dependencyAssetData.AssetData);
                }
                else
                {
                    Debug.LogError($"Failed to import asset dependency '{dependencyAssetData.Identifier}'");
                }
            }

            // Hack Remove the temporary import operation and let a new one starts below
            dependencyCollectionOperation.Finish(OperationStatus.None);
            return allAssetData;
        }

        bool Import(List<PendingImport> pendingImports, string importDestination, CancellationToken token)
        {
            if (!pendingImports.Any())
            {
                return false;
            }

            var importOperations = new List<ImportOperation>();

            // Reset the resolved paths to ask the user again
            m_ResolvedDestinationPaths.Clear();

            foreach (var pendingImport in pendingImports)
            {
                if (m_ImportOperations.Any(x => x.Key.Equals(pendingImport.AssetData.Identifier.AssetId)))
                {
                    Debug.Log("Dupes");
                    continue; // Dupes, I'm not sure why distinct is not working properly
                }

                var path = GetDefaultDestinationPath(pendingImport.AssetData, importDestination, out var cancelImport);

                if (cancelImport)
                {
                    return false;
                }

                var importOperation = CreateImportOperation(pendingImport.AssetData, path);
                importOperations.Add(importOperation);
            }

            var bulkImportOperation = new BulkImportOperation(importOperations);
            bulkImportOperation.Finished += _ => ProcessImports(importOperations);
            bulkImportOperation.Start();

            foreach (var importOperation in importOperations)
            {
                _ = StartImport(importOperation, token);
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
                             .Concat(foldersToRemove.Select(i => m_AssetDatabaseProxy.AssetPathToGuid(i))))
                {
                    leftOverAssetsGuids.Remove(guid);
                }

                foreach (var assetPath in leftOverAssetsGuids.Select(i => m_AssetDatabaseProxy.GuidToAssetPath(i)))
                {
                    var path = Path.GetDirectoryName(assetPath);

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
    }
}
