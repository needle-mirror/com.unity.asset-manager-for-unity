using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using Task = System.Threading.Tasks.Task;

namespace Unity.AssetManager.Editor
{
    interface IAssetImporter : IService
    {
        ImportOperation GetImportOperation(AssetIdentifier identifier);
        bool IsImporting(AssetIdentifier identifier);
        Task<bool> StartImportAsync(List<IAssetData> assetData);
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
        const int k_MaxNumberOfFilesForGroupedDownloadUrlFetch = 100;
        
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
        IDownloadManager m_DownloadManager;

        [SerializeReference]
        IEditorUtilityProxy m_EditorUtilityProxy;

        [SerializeReference]
        IImportedAssetsTracker m_ImportedAssetsTracker;

        [SerializeReference]
        IIOProxy m_IOProxy;

        readonly Dictionary<AssetIdentifier, ImportOperation> m_ImportOperations = new();
        readonly Dictionary<Uri, DownloadOperation> m_UriToDownloadOperationMap = new();

        [ServiceInjection]
        public void Inject(IDownloadManager downloadManager, IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker,
            IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager)
        {
            m_DownloadManager = downloadManager;
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_EditorUtilityProxy = editorUtilityProxy;
            m_ImportedAssetsTracker = importedAssetsTracker;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
        }

        public override void OnEnable() { }

        public override void OnDisable() { }

        public ImportOperation GetImportOperation(AssetIdentifier identifier)
        {
            return m_ImportOperations.TryGetValue(identifier, out var result) ? result : null;
        }

        public bool IsImporting(AssetIdentifier identifier)
        {
            var importOperation = GetImportOperation(identifier);
            return importOperation is { Status: OperationStatus.InProgress };
        }

        public async Task<bool> StartImportAsync(List<IAssetData> assetData)
        {
            SendImportAnalytics(assetData);

            // Hack Create a temporary import operation to track the (long) dependency loading
            var tasks = new List<Task<List<IAssetData>>>();
            foreach (var asset in assetData)
            {
                if (m_ImportOperations.ContainsKey(asset.Identifier))
                    continue;

                if (m_AssetDataManager.IsInProject(asset.Identifier) && !m_EditorUtilityProxy.DisplayDialog(
                        L10n.Tr("Overwrite Imported Files"),
                        L10n.Tr(
                            "All the files previously imported will be overwritten and changes to those files will be lost. Are you sure you want to continue?"),
                        L10n.Tr("Yes"), L10n.Tr("No")))
                {
                    continue;
                }

                tasks.Add(GetDependencies(asset));
            }

            await Task.WhenAll(tasks);

            var allAssetData = tasks.SelectMany(x => x.Result).ToHashSet();

            var pendingImports = allAssetData.Select(x =>
                new PendingImport
                {
                    AssetData = x
                }).ToList();

            Import(pendingImports, CancellationToken.None);

            return true;
        }

        public void ShowInProject(AssetIdentifier identifier)
        {
            try
            {
                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(identifier);

                var fileInfo = importedInfo.FileInfos[0];

                var assetPath = m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.Guid);
                var parentFolderPath = Path.GetDirectoryName(assetPath);

                // Ping the parent folder in the Unity Editor
                var parentFolder = AssetDatabase.LoadAssetAtPath<Object>(parentFolderPath);
                EditorGUIUtility.PingObject(parentFolder);
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

            if (!m_ImportOperations.TryGetValue(identifier, out var importOperation))
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
                if (!m_ImportOperations.TryGetValue(identifier, out var importOperation))
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

            m_ImportOperations.Remove(importOperation.AssetId);
            if (finalStatus is OperationStatus.Cancelled or OperationStatus.Error)
            {
                foreach (var download in importOperation.Downloads)
                {
                    m_DownloadManager.Cancel(download.Id);
                }
            }
        }

        void ProcessImports(IList<ImportOperation> imports)
        {
            foreach (var import in imports)
            {
                m_ImportOperations.Remove(import.AssetId);
            }

            m_UriToDownloadOperationMap.Clear();

            var filesToTrack = new Dictionary<ImportOperation, List<(string originalPath, string finalPath)>>();
            AssetDatabase.StartAssetEditing();

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
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            foreach (var kvp in filesToTrack)
            {
                m_ImportedAssetsTracker.TrackAssets(kvp.Value, kvp.Key.AssetData);
            }
        }

        List<(string originalPath, string finalPath)> MoveImportedFiles(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath)>();

            var importInformation = m_AssetDataManager.GetImportedAssetInfo(importOperation.AssetId);
            try
            {
                foreach (var download in importOperation.Downloads)
                {
                    var originalPath = Path.GetRelativePath(importOperation.TempDownloadPath, download.Path);
                    var keyToUse = originalPath;
                    var isMetaFile = MetafilesHelper.IsMetafile(originalPath);
                    if (isMetaFile)
                    {
                        keyToUse = MetafilesHelper.RemoveMetaExtension(originalPath);
                    }

                    var existingData = importInformation?.FileInfos?.Find(f => f.OriginalPath == keyToUse);
                    var existingPath = m_AssetDatabaseProxy.GuidToAssetPath(existingData?.Guid);
                    var finalPath = string.IsNullOrEmpty(existingPath) ?
                        Path.Combine(importOperation.DestinationPath, keyToUse) :
                        existingPath;

                    if (isMetaFile)
                    {
                        finalPath += MetafilesHelper.MetaFileExtension;
                    }

                    if (File.Exists(download.Path))
                    {
                        m_IOProxy.DeleteFileIfExists(finalPath);
                        m_IOProxy.FileMove(download.Path, finalPath);
                    }

                    filesToTrack.Add((originalPath, finalPath));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                // TODO Delete already moved files?
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

                    if(assetData.SourceFiles != null
                       && assetData.SourceFiles.Any())
                    {
                        fileCount = assetData.SourceFiles.Count();

                        var extensions = assetData.SourceFiles.Select(adf => Path.GetExtension(adf.Path));
                        fileExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions);
                        fileExtension = fileExtension.Substring(1); // remove the dot
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

        static string GetDestinationPath(IAssetData assetData)
        {
            return Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName,
                $"{Regex.Replace(assetData.Name, @"[\\\/:*?""<>|]", "").Trim()}");
        }

        string GetDefaultDestinationPath(IAssetData assetData, out bool cancelImport)
        {
            cancelImport = false;
            var assetsPath = Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName);
            var tempPath = m_IOProxy.GetUniqueTempPathInProject();
            var tempFilesToTrack = new List<string>();
            var destinationPath = GetDestinationPath(assetData);

            var existingImport = m_AssetDataManager.GetImportedAssetInfo(assetData.Identifier);
            if (existingImport != null)
            {
                tempFilesToTrack = MoveImportToTempPath(assetData.Identifier, tempPath, assetsPath);
            }

            var nonConflictingImportPath = GetNonConflictingImportPath(destinationPath);
            if (destinationPath != nonConflictingImportPath)
            {
                var destinationFolderName = new DirectoryInfo(destinationPath).Name;
                var newPathFolderName = new DirectoryInfo(nonConflictingImportPath).Name;
                var title = L10n.Tr("Import conflict");

                if (m_IOProxy.FileExists(destinationPath))
                {
                    var message = string.Format(L10n.Tr("A file named '{0}' already exists." +
                            "\nDo you want to create a new folder named '{1}' and import the selected assets into that folder?"),
                        destinationFolderName, newPathFolderName);

                    if (m_EditorUtilityProxy.DisplayDialog(title, message, L10n.Tr("Create new folder"),
                            L10n.Tr("Cancel")))
                    {
                        return nonConflictingImportPath;
                    }

                    cancelImport = true;
                }
                else
                {
                    var message = string.Format(L10n.Tr("A folder named '{0}' already exists." +
                            "\nDo you want to continue with the import and rename any conflicting files in '{0}'?" +
                            "\nOr do you want to create a new folder named '{1}' and import the selected assets into that folder?"),
                        destinationFolderName, newPathFolderName);

                    switch (m_EditorUtilityProxy.DisplayDialogComplex(title, message, L10n.Tr("Continue"),
                                L10n.Tr("Create new folder"), L10n.Tr("Cancel")))
                    {
                        case 1:
                            return nonConflictingImportPath;
                        case 2:
                            cancelImport = true;
                            break;
                    }
                }
            }

            if (existingImport != null)
            {
                MoveImportToAssetPath(tempPath, assetsPath, tempFilesToTrack);
                m_IOProxy.DirectoryDelete(tempPath, true);
            }

            return destinationPath;
        }

        ImportOperation CreateImportOperation(IAssetData assetData, string importPath)
        {
            if (m_ImportOperations.TryGetValue(assetData.Identifier, out var existingImportOperation))
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

            m_ImportOperations[importOperation.AssetId] = importOperation;
            return importOperation;
        }

        async Task StartImport(ImportOperation importOperation, CancellationToken token)
        {
            m_IOProxy.CreateDirectory(importOperation.TempDownloadPath);

            var assetData = (AssetData)importOperation.AssetData;
            var asset = assetData.Asset;

            var sourceDataset = await asset.GetSourceDatasetAsync(token);
            var sourceDatasetId = sourceDataset.Descriptor.DatasetId.ToString();
            
            var files = new List<IFile>();
            await foreach (var file in asset.ListFilesAsync(Range.All, token))
            {
                files.Add(file);
            }

            if (files.Count > k_MaxNumberOfFilesForGroupedDownloadUrlFetch)
            {
                var fetchDownloadUrlsOperation = new FetchDownloadUrlsOperation();
                fetchDownloadUrlsOperation.Start();
                
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var filepath = file.Descriptor.Path;
                    
                    fetchDownloadUrlsOperation.SetDescription(Path.GetFileName(filepath));
                    
                    if (MetafilesHelper.IsOrphanMetafile(filepath, files.Select(x => x.Descriptor.Path)))
                        continue;

                    if (AssetDataDependencyHelper.IsASystemFile(filepath))
                        continue;
                    
                    var url = await file.GetDownloadUrlAsync(token);
                                           
                    if (!url.ToString().Contains(sourceDatasetId))
                        continue;

                    var downloadOperation = GetDownloadOperation(url, filepath, importOperation);

                    importOperation.AddDownload(downloadOperation);
                    fetchDownloadUrlsOperation.SetProgress((float)i/files.Count);
                }

                fetchDownloadUrlsOperation.Finish(OperationStatus.Success);
                importOperation.Start();
                
                foreach (var downloadOperation in importOperation.Downloads)
                {
                    if (downloadOperation.Status == OperationStatus.None)
                    {
                        m_DownloadManager.StartDownload(downloadOperation);
                    }
                    else if (downloadOperation.Status == OperationStatus.Error)
                    {
                        importOperation.Finish(OperationStatus.Error);
                        return;
                    }
                }
            }
            else
            {
                importOperation.Start();
                
                var urls = await asset.GetAssetDownloadUrlsAsync(token);
                
                foreach (var url in urls)
                {
                    if (!url.ToString().Contains(sourceDatasetId))
                        continue;
                    
                    if (MetafilesHelper.IsOrphanMetafile(url.Key, urls.Keys))
                        continue;

                    if (AssetDataDependencyHelper.IsASystemFile(url.Key))
                        continue;

                    var downloadOperation = GetDownloadOperation(url.Value, url.Key, importOperation);

                    importOperation.AddDownload(downloadOperation);
                
                    if (downloadOperation.Status == OperationStatus.None)
                    {
                        m_DownloadManager.StartDownload(downloadOperation);
                    }
                    else if (downloadOperation.Status == OperationStatus.Error)
                    {
                        importOperation.Finish(OperationStatus.Error);
                        return;
                    }
                }
            }

            if (importOperation.Downloads.All(x => x.Status == OperationStatus.Success))
            {
                importOperation.Finish(OperationStatus.Success);
            }
        }

        DownloadOperation GetDownloadOperation(Uri uri, string fileName, ImportOperation importOperation)
        {
            if (m_UriToDownloadOperationMap.TryGetValue(uri, out var downloadOperation))
            {
                return downloadOperation;
            }

            var path = Path.Combine(importOperation.TempDownloadPath, fileName);
            downloadOperation = m_DownloadManager.CreateDownloadOperation(uri.ToString(), path);

            m_UriToDownloadOperationMap.TryAdd(uri, downloadOperation);
            return downloadOperation;
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

        async Task<List<IAssetData>> GetDependencies(IAssetData asset)
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

        void Import(List<PendingImport> pendingImports, CancellationToken token)
        {
            var importOperations = new List<ImportOperation>();

            foreach (var pendingImport in pendingImports)
            {
                if (m_ImportOperations.Any(x => x.Key.Equals(pendingImport.AssetData.Identifier)))
                {
                    Debug.Log("Dupes");
                    continue; // Dupes, I'm not sure why distinct is not working properly
                }

                var path = GetDefaultDestinationPath(pendingImport.AssetData, out var cancelImport);
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
