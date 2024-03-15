using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine.Networking;
using Task = System.Threading.Tasks.Task;

namespace Unity.AssetManager.Editor
{
    interface IAssetImporter : IService
    {
        ImportOperation GetImportOperation(AssetIdentifier identifier);
        bool IsImporting(AssetIdentifier identifier);
        Task<bool> StartImportAsync(IAssetData assetData);
        bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false);
        void ShowInProject(AssetIdentifier identifier);
        void CancelImport(AssetIdentifier identifier, bool showConfirmationDialog = false);
    }

    internal class BulkImportOperation : BaseOperation
    {
        public override float Progress { get; }
        public override string OperationName { get; }
        public override string Description { get; }
        public List<ImportOperation> ImportOperations;

        public BulkImportOperation(List<ImportOperation> importOperations)
        {
            ImportOperations = importOperations;
        }

        public void OnImportCompleted()
        {
            if (ImportOperations.TrueForAll(x => x.Status == OperationStatus.Success))
            {
                Finish(OperationStatus.Success);
            }

            if (ImportOperations.Any(x => x.Status == OperationStatus.Error))
            {
                Finish(OperationStatus.Error);
            }

            if (ImportOperations.Any(x => x.Status == OperationStatus.Cancelled))
            {
                Finish(OperationStatus.Cancelled);
            }
        }
    }

    internal static class MetafilesHelper
    {
        public static readonly string MetaFileExtension = ".meta";

        public static bool IsOrphanMetafile(string fileName, ICollection<string> allFiles)
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
                return string.Empty;

            return IsMetafile(fileName) ? fileName[..^MetaFileExtension.Length] : fileName;
        }

        public static string AssetMetaFile(string fileName)
        {
            return AssetDatabase.GetTextMetaFilePathFromAssetPath(fileName);
        }
    }

    [Serializable]
    internal class AssetImporter : BaseService<IAssetImporter>, IAssetImporter, ISerializationCallbackReceiver
    {
        private readonly Dictionary<ulong, ImportOperation> m_DownloadIdToImportOperationsLookup = new();
        private readonly Dictionary<AssetIdentifier, ImportOperation> m_ImportOperations = new();

        [Serializable]
        private class PendingImport
        {
            [SerializeReference]
            public IAssetData assetData;
        }

        [SerializeField]
        private List<PendingImport> m_PendingImports = new();

        [SerializeField]
        private ImportOperation[] m_SerializedImportOperations;

        public ImportOperation GetImportOperation(AssetIdentifier identifier) => m_ImportOperations.TryGetValue(identifier, out var result) ? result : null;

        public bool IsImporting(AssetIdentifier identifier)
        {
            var importOperation = GetImportOperation(identifier);
            return importOperation is { Status: OperationStatus.InProgress };
        }

        [SerializeReference]
        IDownloadManager m_DownloadManager;

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;

        [SerializeReference]
        IEditorUtilityProxy m_EditorUtilityProxy;

        [SerializeReference]
        IImportedAssetsTracker m_ImportedAssetsTracker;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [ServiceInjection]
        public void Inject(IDownloadManager downloadManager, IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker, IAssetDataManager assetDataManager, IAssetOperationManager assetOperationManager)
        {
            m_DownloadManager = downloadManager;
            m_IOProxy = ioProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_EditorUtilityProxy = editorUtilityProxy;
            m_ImportedAssetsTracker = importedAssetsTracker;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
        }

        public override void OnEnable()
        {
            var importsToResume = m_PendingImports.ToArray();
            m_PendingImports.Clear();
            foreach (var item in importsToResume)
            {
                _ = StartImportAsync(item.assetData);
            }
        }

        private void OnDownloadProgress(DownloadOperation downloadOperation)
        {
            if (!m_DownloadIdToImportOperationsLookup.TryGetValue(downloadOperation.id, out var importOperation))
                return;

            importOperation.UpdateDownloadOperation(downloadOperation);

            importOperation.Report();
        }

        private void OnDownloadFinished(DownloadOperation downloadOperation)
        {
            if (!m_DownloadIdToImportOperationsLookup.TryGetValue(downloadOperation.id, out var importOperation))
                return;

            importOperation.UpdateDownloadOperation(downloadOperation);

            if (downloadOperation.Status is OperationStatus.Cancelled or OperationStatus.Error)
            {
                FinalizeImport(importOperation, downloadOperation.Status, downloadOperation.error);
            }
            else if (importOperation.downloads.All(i => i.Status == OperationStatus.Success))
            {
                FinalizeImport(importOperation, OperationStatus.Success);
            }
        }

        private void FinalizeImport(ImportOperation importOperation, OperationStatus finalStatus, string errorMessage = null)
        {
            switch (finalStatus)
            {
                case OperationStatus.Success:
                    AnalyticsSender.SendEvent(new ImportEndEvent(ImportEndStatus.Ok, importOperation.AssetId.ToString(), importOperation.startTime, DateTime.Now, importOperation.assetData?.sourceFiles.Count() ?? 0));
                    break;
                case OperationStatus.Error:
                    AnalyticsSender.SendEvent(new ImportEndEvent(ImportEndStatus.DownloadError, importOperation.AssetId.ToString(), importOperation.startTime, DateTime.Now, 0, errorMessage));
                    break;
                case OperationStatus.Cancelled:
                    AnalyticsSender.SendEvent(new ImportEndEvent(ImportEndStatus.Cancelled, importOperation.AssetId.ToString(), importOperation.startTime, DateTime.Now));
                    break;
            }

            importOperation.Finish(finalStatus);

            m_ImportOperations.Remove(importOperation.AssetId);
            foreach (var download in importOperation.downloads)
            {
                m_DownloadIdToImportOperationsLookup.Remove(download.id);
            }

            if (finalStatus is OperationStatus.Cancelled or OperationStatus.Error)
            {
                foreach (var download in importOperation.downloads)
                {
                    m_DownloadManager.Cancel(download.id);
                }
            }
        }

        private void ProcessImports(IList<ImportOperation> imports)
        {
            var filesToTrack = new Dictionary<ImportOperation, List<(string originalPath, string finalPath)>>();
            AssetDatabase.StartAssetEditing();
            foreach (var import in imports)
            {
                var files = MoveImportedFiles(import);
                filesToTrack[import] = files;
            }

            m_PendingImports.RemoveAll(pending =>
                imports.Any(import =>
                    pending.assetData.identifier == import.assetData.identifier));

            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            foreach (var kvp in filesToTrack)
            {
                m_ImportedAssetsTracker.TrackAssets(kvp.Value, kvp.Key.assetData);
            }
        }

        private List<(string originalPath, string finalPath)> MoveImportedFiles(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath)>();

            var importInformation = m_AssetDataManager.GetImportedAssetInfo(importOperation.AssetId);
            try
            {
                foreach (var download in importOperation.downloads)
                {
                    var originalPath = Path.GetRelativePath(importOperation.tempDownloadPath, download.path);
                    var keyToUse = originalPath;
                    var isMetaFile = MetafilesHelper.IsMetafile(originalPath);
                    if (isMetaFile)
                    {
                        keyToUse = MetafilesHelper.RemoveMetaExtension(originalPath);
                    }

                    var existingData = importInformation?.fileInfos?.Find(f => f.originalPath == keyToUse);
                    var existingPath = m_AssetDatabaseProxy.GuidToAssetPath(existingData?.guid);
                    var finalPath = string.IsNullOrEmpty(existingPath)
                        ? Path.Combine(importOperation.destinationPath, keyToUse)
                        : existingPath;

                    if (isMetaFile)
                    {
                        finalPath += MetafilesHelper.MetaFileExtension;
                    }

                    m_IOProxy.DeleteFileIfExists(finalPath);
                    m_IOProxy.FileMove(download.path, finalPath);
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
                m_IOProxy.DirectoryDelete(importOperation.tempDownloadPath, true);
            }

            return filesToTrack;
        }

        private string GetNonConflictingImportPath(string path)
        {
            if (!m_IOProxy.FileExists(path) && !m_IOProxy.DirectoryExists(path))
                return path;

            var extension = Path.GetExtension(path);
            var pathWithoutExtension = path[..^extension.Length];
            if (!string.IsNullOrEmpty(extension))
                path = pathWithoutExtension;

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

        private string GetDefaultDestinationPath(IAssetData assetData, out bool cancelImport)
        {
            cancelImport = false;
            var assetsPath = Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName);
            var tempPath = m_IOProxy.GetUniqueTempPathInProject();
            var tempFilesToTrack = new List<string>();
            var destinationPath = assetData.defaultImportPath;

            var existingImport = m_AssetDataManager.GetImportedAssetInfo(assetData.identifier);
            if (existingImport != null)
                tempFilesToTrack = MoveImportToTempPath(assetData.identifier, tempPath, assetsPath);

            var nonConflictingImportPath = GetNonConflictingImportPath(destinationPath);
            if (destinationPath != nonConflictingImportPath)
            {
                var destinationFolderName = new DirectoryInfo(destinationPath).Name;
                var newPathFolderName = new DirectoryInfo(nonConflictingImportPath).Name;
                var title = L10n.Tr("Import conflict");

                if (m_IOProxy.FileExists(destinationPath))
                {
                    var message = string.Format(L10n.Tr("A file named '{0}' already exists." +
                        "\nDo you want to create a new folder named '{1}' and import the selected assets into that folder?"), destinationFolderName, newPathFolderName);

                    if (m_EditorUtilityProxy.DisplayDialog(title, message, L10n.Tr("Create new folder"), L10n.Tr("Cancel")))
                    {
                        return nonConflictingImportPath;
                    }

                    cancelImport = true;
                }
                else
                {
                    var message = string.Format(L10n.Tr("A folder named '{0}' already exists." +
                        "\nDo you want to continue with the import and rename any conflicting files in '{0}'?" +
                        "\nOr do you want to create a new folder named '{1}' and import the selected assets into that folder?"), destinationFolderName, newPathFolderName);

                    switch (m_EditorUtilityProxy.DisplayDialogComplex(title, message, L10n.Tr("Continue"), L10n.Tr("Create new folder"), L10n.Tr("Cancel")))
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

        private ImportOperation CreateImportOperation(IAssetData assetData, string importPath)
        {
            if (m_ImportOperations.TryGetValue(assetData.identifier, out var existingImportOperation))
            {
                return existingImportOperation;
            }

            var importOperation = new ImportOperation(assetData)
            {
                destinationPath = importPath,
                startTime = DateTime.Now,
                tempDownloadPath = m_IOProxy.GetUniqueTempPathInProject()
            };

            m_AssetOperationManager.RegisterOperation(importOperation);

            m_ImportOperations[importOperation.AssetId] = importOperation;
            return importOperation;
        }

        private async Task StartImport(ImportOperation importOperation, CancellationToken token)
        {
            importOperation.Start();
            m_IOProxy.CreateDirectory(importOperation.tempDownloadPath);

            var tasks = new List<Task<DownloadOperation>>();

            // Update AssetData with the latest cloud data
            await importOperation.assetData.SyncWithCloudAsync(null, token);

            await foreach (var file in importOperation.assetData.GetSourceCloudFilesAsync(token))
            {
                if (AssetDataDependencyHelper.IsASystemFile(file.Descriptor.Path))
                    continue;

                tasks.Add(CreateDownloadOperation(file, importOperation.tempDownloadPath, token));
            }

            if (tasks.Count == 0)
            {
                Debug.LogWarning($"Asset {importOperation.assetData.name} has no files to download.");
                FinalizeImport(importOperation, OperationStatus.Error, "No files to download.");
            }

            await Task.WhenAll(tasks);

            var allFiles = tasks.Where(t => t.Result != null)
                .Select(d => d.Result.path)
                .ToList();

            var hasUndeterminedTotalSize = false;

            foreach (var task in tasks)
            {
                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                    continue;
                }

                var downloadOperation = task.Result;
                if (downloadOperation == null)
                    continue;

                if (downloadOperation.totalBytes <= 0)
                {
                    hasUndeterminedTotalSize = true;
                }

                if (MetafilesHelper.IsOrphanMetafile(downloadOperation.path, allFiles))
                    continue;

                importOperation.AddDownload(downloadOperation);
                m_DownloadIdToImportOperationsLookup[downloadOperation.id] = importOperation;
                m_DownloadManager.StartDownload(downloadOperation);
            }

            if (hasUndeterminedTotalSize)
            {
                Debug.LogWarning($"Some files in {importOperation.assetData.name} has undetermined size. Download progress might not be accurate.");
            }
        }

        async Task<DownloadOperation> CreateDownloadOperation(IFile file, string dstFolder, CancellationToken token)
        {
            var downloadUri = await file.GetDownloadUrlAsync(token);
            var downloadUrl = downloadUri?.ToString();

            if (string.IsNullOrEmpty(downloadUrl))
                return null;

            var path = Path.Combine(dstFolder, file.Descriptor.Path);

            var downloadOperation = m_DownloadManager.CreateDownloadOperation(downloadUrl, path);
            downloadOperation.totalBytes = file.SizeBytes;

            downloadOperation.Finished += _ => OnDownloadFinished(downloadOperation);
            downloadOperation.ProgressChanged += _ => OnDownloadProgress(downloadOperation);

            return downloadOperation;
        }

        public UnityWebRequestAsyncOperation SendWebRequest(string url, string path, bool append = false, string bytesRange = null)
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

        public async Task<bool> StartImportAsync(IAssetData assetData)
        {
            if (assetData == null || m_ImportOperations.ContainsKey(assetData.identifier))
                return false;

            if (m_AssetDataManager.IsInProject(assetData.identifier) && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Overwrite Imported Files"),
                    L10n.Tr("All the files previously imported will be overwritten and changes to those files will be lost. Are you sure you want to continue?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return false;

            // Hack Create a temporary import operation to track the (long) dependency loading
            var dependencyCollectionOperation = new ImportOperation(assetData);
            m_AssetOperationManager.RegisterOperation(dependencyCollectionOperation);
            dependencyCollectionOperation.Start();

            var allAssetData = new List<IAssetData> { assetData };
            await foreach (var dependencyAssetData in AssetDataDependencyHelper.LoadDependenciesAsync(assetData, true, CancellationToken.None))
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

            var pendingImports = allAssetData.Distinct().Select(x =>
                new PendingImport() {
                    assetData = x
                }).ToList();
            m_PendingImports.AddRange(pendingImports);
            Import(pendingImports, CancellationToken.None);

            return true;
        }

        void Import(List<PendingImport> pendingImports, CancellationToken token)
        {
            var importOperations = new List<ImportOperation>();

            foreach (var pendingImport in pendingImports)
            {
                var path = GetDefaultDestinationPath(pendingImport.assetData, out bool cancelImport);
                var importOperation = CreateImportOperation(pendingImport.assetData, path);
                importOperations.Add(importOperation);
            }

            var bulkImports = new BulkImportOperation(importOperations);
            bulkImports.Finished += _ => ProcessImports(importOperations);
            bulkImports.Start();

            foreach (var importOperation in importOperations)
            {
                importOperation.Finished += _ => bulkImports.OnImportCompleted();
                _ = StartImport(importOperation, token);
            }
        }

        public void ShowInProject(AssetIdentifier identifier)
        {
            try
            {
                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(identifier);

                var fileInfo = importedInfo.fileInfos[0];

                var assetPath = m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.guid);
                var parentFolderPath = Path.GetDirectoryName(assetPath);

                // Ping the parent folder in the Unity Editor
                var parentFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(parentFolderPath);
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

        public bool RemoveImport(AssetIdentifier identifier, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Asset"),
                    L10n.Tr("Remove the selected asset?" + Environment.NewLine + "Any changes you made to this asset will be lost."), L10n.Tr("Remove"), L10n.Tr("Cancel")))
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
                    Debug.LogWarning("Asset was removed but file were deleted because they were not found in the project.");
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

        private string[] FindAssetsAndLeftoverFolders(AssetIdentifier identifier)
        {
            var fileInfos = m_AssetDataManager.GetImportedAssetInfo(identifier)?.fileInfos;
            if (fileInfos == null || fileInfos.Count == 0)
                return Array.Empty<string>();

            try
            {
                const string assetsPath = "Assets";
                var filesToRemove = fileInfos.Select(fileInfo => m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.guid)).Where(f => !string.IsNullOrEmpty(f)).OrderByDescending(p => p).ToList();
                var foldersToRemove = new HashSet<string>();
                foreach (var file in filesToRemove)
                {
                    var path = Path.GetDirectoryName(file);

                    // We want to add an asset's parent folders all the way up to the `Assets` folder, because we don't want to leave behind
                    // empty folders after the assets are removed process.
                    while (!string.IsNullOrEmpty(path) && !foldersToRemove.Contains(path) && path.StartsWith(assetsPath) && path.Length > assetsPath.Length)
                    {
                        foldersToRemove.Add(path);
                        path = Path.GetDirectoryName(path);
                    }
                }

                var leftOverAssetsGuids = m_AssetDatabaseProxy.FindAssets(string.Empty, foldersToRemove.ToArray()).ToHashSet();
                foreach (var guid in fileInfos.Select(i => i.guid).Concat(foldersToRemove.Select(i => m_AssetDatabaseProxy.AssetPathToGuid(i))))
                    leftOverAssetsGuids.Remove(guid);

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
                    return new List<string>();

                if (!m_IOProxy.DirectoryExists(tempPath))
                    m_IOProxy.CreateDirectory(tempPath);

                var filesToMove = new List<string>();
                var foldersToRemove = new List<string>();
                foreach (var path in assetsAndFolders)
                {
                    if (m_IOProxy.FileExists(path))
                        filesToMove.Add(path);
                    else if (m_IOProxy.DirectoryExists(path))
                        foldersToRemove.Add(path);

                    var metaFile = path + ".meta";
                    if (m_IOProxy.FileExists(metaFile))
                        filesToMove.Add(metaFile);
                }

                var filesToTrack = new List<string>();
                foreach (var file in filesToMove)
                {
                    var originalPath = Path.GetRelativePath(assetPath, file);
                    var finalPath = Path.Combine(tempPath, originalPath);
                    m_IOProxy.FileMove(file, finalPath);
                    filesToTrack.Add(finalPath);
                }

                foreach (var path in foldersToRemove)
                    m_IOProxy.DirectoryDelete(path, true);

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

        public void OnBeforeSerialize()
        {
            foreach (var item in m_PendingImports)
                m_ImportOperations.Remove(item.assetData.identifier);
            m_SerializedImportOperations = m_ImportOperations.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var operation in m_SerializedImportOperations ?? Array.Empty<ImportOperation>())
            {
                m_ImportOperations[operation.AssetId] = operation;
                foreach (var download in operation.downloads)
                    m_DownloadIdToImportOperationsLookup[download.id] = operation;
            }
        }
    }
}
