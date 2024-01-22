using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Unity.AssetManager.Editor
{
    internal interface IAssetImporter : IService
    {
        event Action<ImportOperation> onImportProgress;
        event Action<ImportOperation> onImportFinalized;

        ImportOperation GetImportOperation(AssetIdentifier assetId);
        bool IsImporting(AssetIdentifier assetId);
        void StartImportAsync(IAssetData assetData, ImportAction importAction);
        void RemoveImport(IAssetData assetData, bool showConfirmationDialog = false);
        void CancelImport(AssetIdentifier assetId, bool showConfirmationDialog = false);
    }

    [Serializable]
    internal class AssetImporter : BaseService<IAssetImporter>, IAssetImporter, ISerializationCallbackReceiver
    {
        public event Action<ImportOperation> onImportProgress = delegate {};
        public event Action<ImportOperation> onImportFinalized = delegate {};

        private readonly Dictionary<ulong, ImportOperation> m_DownloadIdToImportOperationsLookup = new ();
        private readonly Dictionary<AssetIdentifier, ImportOperation> m_ImportOperations = new ();


        [Serializable]
        private class PendingImport
        {
            [SerializeReference]
            public IAssetData assetData;
            public ImportAction importAction;
        }

        [SerializeField]
        private List<PendingImport> m_PendingImports = new ();

        [SerializeField]
        private ImportOperation[] m_SerializedImportOperations;

        public ImportOperation GetImportOperation(AssetIdentifier assetId) => m_ImportOperations.TryGetValue(assetId, out var result) ? result : null;
        public bool IsImporting(AssetIdentifier assetId) => m_ImportOperations.TryGetValue(assetId, out var result) && result.status == OperationStatus.InProgress;

        private readonly IAssetsProvider m_AssetsProvider;
        private readonly IDownloadManager m_DownloadManager;
        private readonly IAnalyticsEngine m_AnalyticsEngine;
        private readonly IIOProxy m_IOProxy;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        private readonly IEditorUtilityProxy m_EditorUtilityProxy;
        private readonly IImportedAssetsTracker m_ImportedAssetsTracker;
        private readonly IAssetDataManager m_AssetDataManager;
        public AssetImporter(IAssetsProvider assetsProvider, IDownloadManager downloadManager,
            IAnalyticsEngine analyticsEngine, IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy,
            IEditorUtilityProxy editorUtilityProxy, IImportedAssetsTracker importedAssetsTracker, IAssetDataManager assetDataManager)
        {
            m_AssetsProvider = RegisterDependency(assetsProvider);
            m_DownloadManager = RegisterDependency(downloadManager);
            m_AnalyticsEngine = RegisterDependency(analyticsEngine);
            m_IOProxy = RegisterDependency(ioProxy);
            m_AssetDatabaseProxy = RegisterDependency(assetDatabaseProxy);
            m_EditorUtilityProxy = RegisterDependency(editorUtilityProxy);
            m_ImportedAssetsTracker = RegisterDependency(importedAssetsTracker);
            m_AssetDataManager = RegisterDependency(assetDataManager);
        }

        public override void OnEnable()
        {
            m_DownloadManager.onDownloadFinalized += OnDownloadFinalized;
            m_DownloadManager.onDownloadProgress += OnDownloadProgress;
            m_AssetDataManager.onAssetDataChanged += OnAssetDataChanged;

            var importsToResume = m_PendingImports.ToArray();
            m_PendingImports.Clear();
            foreach (var item in importsToResume)
                StartImportAsync(item.assetData, item.importAction);
        }

        public override void OnDisable()
        {
            m_DownloadManager.onDownloadFinalized -= OnDownloadFinalized;
            m_DownloadManager.onDownloadProgress -= OnDownloadProgress;
            m_AssetDataManager.onAssetDataChanged += OnAssetDataChanged;
        }

        private void OnDownloadProgress(DownloadOperation downloadOperation)
        {
            if (!m_DownloadIdToImportOperationsLookup.TryGetValue(downloadOperation.id, out var importOperation))
                return;
            importOperation.UpdateDownloadOperation(downloadOperation);
            onImportProgress?.Invoke(importOperation);
        }

        private void OnDownloadFinalized(DownloadOperation downloadOperation)
        {
            if (!m_DownloadIdToImportOperationsLookup.TryGetValue(downloadOperation.id, out var importOperation))
                return;
            importOperation.UpdateDownloadOperation(downloadOperation);

            if (downloadOperation.status is OperationStatus.Cancelled or OperationStatus.Error)
                FinalizeImport(importOperation, downloadOperation.status, downloadOperation.error);
            else if (importOperation.downloads.All(i => i.status == OperationStatus.Success))
                FinalizeImport(importOperation, OperationStatus.Success);
            else
                onImportProgress?.Invoke(importOperation);
        }

        private void FinalizeImport(ImportOperation importOperation, OperationStatus finalStatus, string errorMessage = null)
        {
            importOperation.status = finalStatus;
            switch (finalStatus)
            {
                case OperationStatus.Success:
                    MoveFilesToAssetsAndKeepTrack(importOperation);
                    m_AnalyticsEngine.SendImportEndEvent(ImportEndStatus.Ok, importOperation.assetId.ToString(), importOperation.importAction.ToString(), new DateTime(importOperation.startTimeTicks), DateTime.Now);
                    break;
                case OperationStatus.Error:
                    m_AnalyticsEngine.SendImportEndEvent(ImportEndStatus.DownloadError, importOperation.assetId.ToString(), importOperation.importAction.ToString(), new DateTime(importOperation.startTimeTicks), DateTime.Now, errorMessage);
                    break;
                case OperationStatus.Cancelled:
                    m_AnalyticsEngine.SendImportEndEvent(ImportEndStatus.Cancelled, importOperation.assetId.ToString(), importOperation.importAction.ToString(), new DateTime(importOperation.startTimeTicks), DateTime.Now);
                    break;
            }

            m_ImportOperations.Remove(importOperation.assetId);
            foreach (var download in importOperation.downloads)
                m_DownloadIdToImportOperationsLookup.Remove(download.id);

            if (finalStatus is OperationStatus.Cancelled or OperationStatus.Error)
                foreach (var download in importOperation.downloads)
                    m_DownloadManager.Cancel(download.id);

            onImportFinalized?.Invoke(importOperation);
        }

        private void MoveFilesToAssetsAndKeepTrack(ImportOperation importOperation)
        {
            var filesToTrack = new List<(string originalPath, string finalPath)>();
            foreach (var download in importOperation.downloads)
            {
                var originalPath = Path.GetRelativePath(importOperation.tempDownloadPath, download.path);
                var finalPath = GetNonConflictingImportPath(Path.Combine(importOperation.destinationPath, originalPath));

                m_IOProxy.DeleteFileIfExists(finalPath);
                m_IOProxy.FileMove(download.path, finalPath);
                filesToTrack.Add((originalPath, finalPath));
            }
            m_IOProxy.DirectoryDelete(importOperation.tempDownloadPath, true);
            m_AssetDatabaseProxy.Refresh();
            m_ImportedAssetsTracker.TrackAssets(filesToTrack, importOperation.assetId);
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
                    pathToCheck = $"{pathWithoutNumber} {number}";

                if (m_IOProxy.FileExists(pathToCheck + extension) || m_IOProxy.DirectoryExists(pathToCheck + extension))
                    number++;
                else
                    return pathToCheck += extension;
            }
        }

        private string GetImportDestinationPathAndRemoveImportWhenReset(IAssetData assetData, out bool cancelImport)
        {
            cancelImport = false;
            var assetsPath = Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName);
            var tempPath = m_IOProxy.GetUniqueTempPathInProject();
            var tempFilesToTrack = new List<string>();
            var destinationPath = assetData.defaultImportPath;

            var existingImport = m_AssetDataManager.GetImportedAssetInfo(assetData.id);
            if (existingImport != null)
                tempFilesToTrack = MoveImportToTempPath(assetData, tempPath, assetsPath);

            var nonConflictingImportPath = GetNonConflictingImportPath(destinationPath);
            if (destinationPath != nonConflictingImportPath)
            {
                var destinationFolderName = new DirectoryInfo(destinationPath).Name;
                var newPathFolderName = new DirectoryInfo(nonConflictingImportPath).Name;
                var title = L10n.Tr("Import conflict");
                
                if (m_IOProxy.FileExists(destinationPath))
                {
                    var message = string.Format(L10n.Tr("A file named \"{0}\" already exists." +
                                  "\nDo you want to create a new folder named \"{1}\" and import the selected assets into that folder?"), destinationFolderName, newPathFolderName);
                    
                    if (m_EditorUtilityProxy.DisplayDialog(title, message, L10n.Tr("Create new folder"), L10n.Tr("Cancel")))
                        return nonConflictingImportPath;
                    else
                        cancelImport = true;
                }
                else
                {
                    var message = string.Format(L10n.Tr("A folder named \"{0}\" already exists." +
                                  "\nDo you want to continue with the import and rename any conflicting files in \"{0}\"?" +
                                  "\nOr do you want to create a new folder named \"{1}\" and import the selected assets into that folder?"), destinationFolderName, newPathFolderName);

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
                MoveImportToAssetPath(assetData, tempPath, assetsPath, tempFilesToTrack);
                m_IOProxy.DirectoryDelete(tempPath, true);

                if (!cancelImport)
                    RemoveImport(assetData);
            }

            return destinationPath;
        }

        private ImportOperation CreateImportOperation(IAssetData assetData, ImportAction importAction, string importPath)
        {
            var importOperation = new ImportOperation
            {
                assetId = assetData.id,
                destinationPath = importPath,
                importAction = importAction,
                startTimeTicks = DateTime.Now.Ticks,
                tempDownloadPath = m_IOProxy.GetUniqueTempPathInProject()
            };
            m_ImportOperations[importOperation.assetId] = importOperation;
            return importOperation;
        }

        private void StartImport(IAssetData assetData, ImportOperation importOperation)
        {
            m_IOProxy.CreateDirectory(importOperation.tempDownloadPath);
            var uniqueFileNames = new HashSet<string>();
            

            // Meta files for folders causes a lot of issues when it comes to tracking and removing, hence we want to avoid importing folder meta files and just
            // let the Unity Editor generate them. In this process we also filter out orphan .meta files.
            var metaFilesToKeep = new HashSet<string>();
            foreach (var file in assetData.files)
            {
                if (file.path.EndsWith(".meta", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                metaFilesToKeep.Add(file.path.ToLower() + ".meta");
            }

            foreach (var file in assetData.files)
            {
                if (string.IsNullOrEmpty(file.downloadUrl))
                    continue;

                var fileNameLower = file.path.ToLower();
                if (fileNameLower.EndsWith(".meta") && !metaFilesToKeep.Contains(fileNameLower))
                    continue;

                var downloadOperation = m_DownloadManager.StartDownload(file.downloadUrl, Path.Combine(importOperation.tempDownloadPath, GetUniqueFileName(uniqueFileNames, file.path)));
                downloadOperation.totalBytes = file.fileSize;
                importOperation.downloads.Add(downloadOperation);
                m_DownloadIdToImportOperationsLookup[downloadOperation.id] = importOperation;
            }
        }

        private static string GetUniqueFileName(HashSet<string> uniqueNames, string originalName)
        {
            var numTries = 0;
            var newName = originalName;
            while (uniqueNames.Contains(newName))
            {
                numTries++;
                newName = $"{Path.GetFileNameWithoutExtension(originalName)} {numTries}{Path.GetExtension(originalName)}";
            }
            uniqueNames.Add(newName);
            return newName;
        }

        public void StartImportAsync(IAssetData assetData, ImportAction importAction)
        {
            if (assetData == null || m_ImportOperations.ContainsKey(assetData.id))
                return;

            if (m_AssetDataManager.IsInProject(assetData.id) && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Overwrite Imported Files"),
                    L10n.Tr("All the files previously imported will be overwritten and changes to those files will be lost. Are you sure you want to continue?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            var pendingImport = new PendingImport { assetData = assetData, importAction = importAction };
            m_PendingImports.Add(pendingImport);

            var source = new CancellationTokenSource();
            Import(assetData, pendingImport);
            source.Dispose();
        }

        private void Import(IAssetData assetData, PendingImport pendingImport)
        {
            if (pendingImport == null) 
                return;

            var path = GetImportDestinationPathAndRemoveImportWhenReset(assetData, out bool cancelImport);
            var importOperation = CreateImportOperation(assetData, pendingImport.importAction, path);
            try
            {
                if (cancelImport)
                    FinalizeImport(importOperation, OperationStatus.Cancelled);
                else if (assetData.files.Any())
                    StartImport(assetData, importOperation);
                else
                {
                    var errorMessage = "No files to download for asset: " + assetData.name;
                    FinalizeImport(importOperation, OperationStatus.Error, errorMessage);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                FinalizeImport(importOperation, OperationStatus.Error, e.Message);
            }
            finally
            {
                m_PendingImports.RemoveAll(i => !AssetData.AssetDataFactory.IsDifferent(i.assetData as AssetData, assetData as AssetData));
            }
        }
        
        private void OnAssetDataChanged(AssetChangeArgs args)
        {
            var allIds = args.added.Concat(args.removed).Concat(args.updated);
            if (!allIds.Any(a => m_PendingImports.Any(i => a.Equals(i.assetData.id))))
                return;
            foreach (var id in allIds)
            {
                var pendingImport = m_PendingImports.Find(i => i.assetData.id.Equals(id));
                if (pendingImport != null)
                    Import(pendingImport.assetData, pendingImport);
            }
        }

        public void CancelImport(AssetIdentifier assetId, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Cancel Import"),
                    L10n.Tr("Are you sure you want to cancel?"), L10n.Tr("Yes"), L10n.Tr("No")))
                return;

            if (!m_ImportOperations.TryGetValue(assetId, out var importOperation))
                    return;

            FinalizeImport(importOperation, OperationStatus.Cancelled);
        }

        public void RemoveImport(IAssetData assetData, bool showConfirmationDialog = false)
        {
            if (showConfirmationDialog && !m_EditorUtilityProxy.DisplayDialog(L10n.Tr("Remove Imported Asset"),
                    L10n.Tr("Remove the selected asset?" + Environment.NewLine + "Any changes you made to this asset will be lost."), L10n.Tr("Remove"), L10n.Tr("Cancel")))
                return;
            
            try
            {
                var assetsAndFoldersToRemove = FindAssetsAndLeftoverFolders(assetData);
                if (!assetsAndFoldersToRemove.Any())
                    return;

                var pathsFailedToRemove = new List<string>();

                m_AssetDatabaseProxy.DeleteAssets(assetsAndFoldersToRemove, pathsFailedToRemove);

                if (pathsFailedToRemove.Any())
                {
                    var errorMessage = L10n.Tr("Failed to remove the following asset(s) and/or folder(s):");
                    foreach (var path in pathsFailedToRemove)
                        errorMessage += "\n" + path;
                    Debug.LogError(errorMessage);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        private string[] FindAssetsAndLeftoverFolders(IAssetData assetData)
        {
            var fileInfos = m_AssetDataManager.GetImportedAssetInfo(assetData.id)?.fileInfos;
            if (fileInfos == null || fileInfos.Count == 0)
                return new string[0];

            try
            {
                const string assetsPath = "Assets";
                var filesToRemove = fileInfos.Select(fileInfo => m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.guid)).
                    Where(f => !string.IsNullOrEmpty(f)).OrderByDescending(p => p).ToList();
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

        public List<string> MoveImportToTempPath(IAssetData assetData, string tempPath, string assetPath)
        {
            try
            {
                var assetsAndFolders = FindAssetsAndLeftoverFolders(assetData);
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

        public void MoveImportToAssetPath(IAssetData assetData, string tempPath, string assetPath, List<string> filesToTrack)
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
                m_ImportOperations.Remove(item.assetData.id);
            m_SerializedImportOperations = m_ImportOperations.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var operation in m_SerializedImportOperations ?? Array.Empty<ImportOperation>())
            {
                m_ImportOperations[operation.assetId] = operation;
                foreach (var download in operation.downloads)
                    m_DownloadIdToImportOperationsLookup[download.id] = operation;
            }
        }
    }
}