using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.AssetManager.Core.Editor
{
    static class Utilities
    {
        static readonly string[] k_SizeSuffixes = { "B", "Kb", "Mb", "Gb", "Tb" };
        static readonly int k_MD5_bufferSize = 4096;
        static readonly List<string> k_IgnoreExtensions = new() { ".meta", ".am4u_dep", ".am4u_guid" };

        static readonly IDialogManager k_DefaultDialogManager = new DialogManager();

        internal static string BytesToReadableString(double bytes)
        {
            if (bytes == 0)
            {
                return $"0 {k_SizeSuffixes[0]}";
            }

            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            var value = Math.Sign(bytes) * num;

            return place >= k_SizeSuffixes.Length ? $"{bytes} {k_SizeSuffixes[0]}" : $"{value} {k_SizeSuffixes[place]}";
        }

        public static string EscapeBackslashes(string str)
        {
            return string.IsNullOrWhiteSpace(str) ? str : str.Replace(@"\", @"\\");
        }

        public static bool DeleteAllFilesAndFoldersFromDirectory(string path)
        {
            var directory = new DirectoryInfo(path);
            var success = true;

            foreach (var file in directory.EnumerateFiles())
            {
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    success = false;
                }
            }

            foreach (var directoryInfo in directory.EnumerateDirectories())
            {
                try
                {
                    directoryInfo.Delete(true);
                }
                catch (IOException)
                {
                    success = false;
                }
            }

            return success;
        }

        public static long DatetimeToTimestamp(DateTime value)
        {
            return (long)(value - AssetManagerCoreConstants.UnixEpoch).TotalMilliseconds;
        }

        public static string DatetimeToString(DateTime? value)
        {
            return value?.ToLocalTime().ToString("G");
        }

        public static int ConvertTo12HourTime(int hour24)
        {
            return hour24 == 12 ? 12 : hour24 % 12;
        }

        public static int ConvertTo24HourTime(int hour12, bool isPm)
        {
            if (isPm)
            {
                return hour12 % 12 + 12;
            }

            if (hour12 == 12)
            {
                return 0;
            }

            return hour12;
        }

        public static string PascalCaseToSentence(this string input)
        {
            return Regex.Replace(input, "(\\B[A-Z])", " $1");
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLog(string message)
        {
            Debug.Log(message);
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevAssert(bool condition, string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                Debug.Assert(condition);
            }
            else
            {
                Debug.Assert(condition, message);
            }
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogError(string message)
        {
            Debug.LogError(message);
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogException(Exception e)
        {
            Debug.LogException(e);
        }

        public static string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return string.Empty;
            }

            var cleanFullName = Regex.Replace(fullName, @"[^\p{L}\p{Z}-]+", " ").Trim();
            cleanFullName = Regex.Replace(cleanFullName, @"\s*(Jr|Sr|[IVX]+)\.?$", "", RegexOptions.IgnoreCase).Trim();

            var words = cleanFullName.Split(new char[]
            {
                ' '
            }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 1)
            {
                return words[0][..1].ToUpperInvariant();
            }

            var initials = new StringBuilder();
            initials.Append(words[0][..1]);
            initials.Append(words[^1][..1]);

            return initials.ToString().ToUpperInvariant();
        }

        public static int DivideRoundingUp(int x, int y)
        {
            // TODO: Define behaviour for negative numbers
            var quotient = Math.DivRem(x, y, out var remainder);
            return remainder == 0 ? quotient : quotient + 1;
        }

        public static bool CompareListsBeginnings(IList baseList, IList extendedList)
        {
            if (baseList == null && extendedList == null)
            {
                return true;
            }

            if (baseList == null || extendedList == null || baseList.Count > extendedList.Count || baseList.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < baseList.Count; i++)
            {
                var baseListObject = baseList[i];
                var extendedListObject = extendedList[i];
                if (
                    (baseListObject == null && extendedListObject != null) ||
                    (baseListObject != null && extendedListObject == null) ||
                    baseListObject != null && !baseListObject.Equals(extendedListObject))
                {
                    return false;
                }
            }

            return true;
        }

        public static string GetPathRelativeToAssetsFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            var relativePath = Path.GetRelativePath(Application.dataPath, assetPath);
            return NormalizePathSeparators(relativePath);
        }

        public static string GetPathRelativeToAssetsFolderIncludeAssets(string assetPath)
        {
            var str = GetPathRelativeToAssetsFolder(assetPath);

            if (string.IsNullOrEmpty(str))
                return null;

            return Path.Combine("Assets", str);
        }

        public static bool ComparePaths(string path1, string path2)
        {
            return string.Equals(NormalizePathSeparators(path1), NormalizePathSeparators(path2),
                StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Path normalization depends on the current OS
            var str = Application.platform == RuntimePlatform.WindowsEditor ?
                path.Replace('/', Path.DirectorySeparatorChar) :
                path.Replace('\\', Path.DirectorySeparatorChar);

            var pattern = Path.DirectorySeparatorChar == '\\' ? "\\\\+" : "/+";
            return Regex.Replace(str, pattern, Path.DirectorySeparatorChar.ToString());
        }

        public static string OpenFolderPanelInDirectory(string title, string directory, IDialogManager dialogManager = null)
        {
            bool isValidPath;
            string importLocation;

            do
            {
                dialogManager ??= k_DefaultDialogManager;

                importLocation = dialogManager.OpenFolderPanel(title, directory, string.Empty);

                isValidPath = string.IsNullOrEmpty(importLocation) ||
                              IsPathSubdirectoryOfSecondPath(importLocation, directory);

                if (!isValidPath)
                {
                    dialogManager.DisplayDialog("Select a valid folder",
                        "The default import location must be located inside the Assets folder of your project.", "Ok");
                }
            } while (!isValidPath);

            return importLocation;
        }

        static bool IsPathSubdirectoryOfSecondPath(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1))
                return false;

            var dataFolderDirectory = new DirectoryInfo(path2);
            var inputPathDirectory = new DirectoryInfo(path1);

            while (inputPathDirectory.Parent != null)
            {
                if (inputPathDirectory.Parent.FullName == dataFolderDirectory.FullName)
                {
                    return true;
                }

                inputPathDirectory = inputPathDirectory.Parent;
            }

            return false;
        }

        public static string GetUniqueFilename(ICollection<string> allFilenames, string filename)
        {
            var uniqueFilename = filename;
            var counter = 1;

            while (allFilenames.Contains(uniqueFilename))
            {
                var extension = Path.GetExtension(filename);
                var fileWithoutExtension = string.IsNullOrEmpty(extension) ? filename : filename[..^extension.Length];

                uniqueFilename = $"{fileWithoutExtension} ({counter}){extension}";
                ++counter;
            }

            return uniqueFilename;
        }

        public static string ExtractCommonFolder(ICollection<string> filePaths)
        {
            if (filePaths.Count == 0)
            {
                return string.Empty;
            }

            var sanitizedPaths = filePaths.Select(NormalizePathSeparators).ToList();

            var reference = sanitizedPaths[0]; // We can optimize this by selecting the shortest path

            if (filePaths.Count == 1)
            {
                return reference[..^Path.GetFileName(reference).Length];
            }

            var folders = reference.Split(Path.DirectorySeparatorChar);

            if (folders.Length == 0)
            {
                return string.Empty;
            }

            var result = string.Empty;

            foreach (var folder in folders)
            {
                var attempt = result + folder + Path.DirectorySeparatorChar;

                if (sanitizedPaths.TrueForAll(p => p.StartsWith(attempt, StringComparison.OrdinalIgnoreCase)))
                {
                    result = attempt;
                }
                else
                {
                    break;
                }
            }

            if (result.Length < 2) // Avoid returning empty folders
            {
                return string.Empty;
            }

            return NormalizePathSeparators(result);
        }

        public static void SaveAssetIfDirty(string path)
        {
            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            var asset = assetDatabaseProxy.LoadAssetAtPath(path);
            if (asset == null)
                return;

            if (EditorUtility.IsDirty(asset))
            {
                assetDatabaseProxy.SaveAssetIfDirty(asset);
            }
            else if (asset is SceneAsset)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }

        public static bool IsFileDirty(string path)
        {
            // Check dirty flag
            var asset = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().LoadAssetAtPath(path);
            if (asset != null && EditorUtility.IsDirty(asset))
            {
                return true;
            }

            // Check if the file is a scene and it is dirty
            if (asset is SceneAsset)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isDirty)
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<bool> IsLocallyModifiedIgnoreDependenciesAsync(List<string> sourceFiles, ImportedAssetInfo importedAssetInfo, CancellationToken token = default)
        {
            if (importedAssetInfo == null)
            {
                // Un-imported asset cannot have modified files by definition
                return false;
            }

            // If the number of files is different, then the asset was modified
            if (sourceFiles.Count != importedAssetInfo.FileInfos.Count)
            {
                return true;
            }

            // If the files are different, or their path has changed, then the asset was modified
            foreach (var importedFileInfo in importedAssetInfo.FileInfos)
            {
                if (!sourceFiles.Exists(f => ComparePaths(f, importedFileInfo.OriginalPath)))
                {
                    return true;
                }
            }

            // Otherwise, check if the files are identical
            if (await HasLocallyModifiedFilesAsync(importedAssetInfo, token))
            {
                return true;
            }

            return false;
        }

        public static bool CompareDependencies(List<AssetIdentifier> dependencies, List<AssetIdentifier> otherDependencies)
        {
            // Check if the number of dependencies is different
            if (dependencies.Count != otherDependencies.Count)
            {
                return true;
            }

            // Check if the dependencies are different
            if (dependencies.Exists(dependency => !otherDependencies.Contains(dependency)))
            {
                return true;
            }

            return false;
        }

        static async Task<bool> HasLocallyModifiedFilesAsync(ImportedAssetInfo importedAssetInfo, CancellationToken token = default)
        {
            if (importedAssetInfo == null)
            {
                // Un-imported asset cannot have modified files by definition
                return false;
            }

            // Otherwise, check if the files are identical
            foreach (var importedFileInfo in importedAssetInfo.FileInfos)
            {
                var path = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().GuidToAssetPath(importedFileInfo.Guid);

                if (await FileWasModified(path, importedFileInfo.Timestamp, importedFileInfo.Checksum, token))
                {
                    return true;
                }

                // Check if the meta file was modified
                var metaPath = MetafilesHelper.AssetMetaFile(path);
                if (File.Exists(metaPath) && await FileWasModified(metaPath, importedFileInfo.MetalFileTimestamp, importedFileInfo.MetaFileChecksum, token))
                {
                    return true;
                }
            }

            return false;
        }

        static async Task<bool> FileWasModified(string path, long expectedTimestamp, string expectedChecksum, CancellationToken token)
        {
            // Locally modified files are always considered dirty
            if (IsFileDirty(path))
            {
                return true;
            }

            // Check if the file has the same modified date, in which case we know it wasn't modified
            if (IsSameTimestamp(expectedTimestamp, path))
            {
                return false;
            }

            // Check if we have checksum information, in which case, a similar checksum means the file wasn't modified
            if (await IsSameFileChecksumAsync(expectedChecksum, path, token))
            {
                return false;
            }

            // In case we can't determine if the file was modified, we assume it was to avoid blocking the re-upload
            return true;
        }

        public static async Task<IEnumerable<BaseAssetDataFile>> GetModifiedFilesAsync(AssetIdentifier identifier, IEnumerable<BaseAssetDataFile> files, IAssetDataManager assetDataManager = null, CancellationToken token = default)
        {
            var modifiedFiles = new List<BaseAssetDataFile>();

            assetDataManager ??= ServicesContainer.instance.Resolve<IAssetDataManager>();

            var importedAssetInfo = assetDataManager.GetImportedAssetInfo(identifier);

            if (importedAssetInfo == null)
            {
                return modifiedFiles;
            }

            foreach (var file in files.Where(f => !k_IgnoreExtensions.Contains(Path.GetExtension(f.Path))))
            {
                try
                {
                    var importedFileInfo = importedAssetInfo.FileInfos.Find(f => ComparePaths(f.OriginalPath, file.Path));

                    if (importedFileInfo != null && await FileWasModified(AssetDatabase.GUIDToAssetPath(importedFileInfo.Guid), importedFileInfo.Timestamp, importedFileInfo.Checksum, token))
                    {
                        modifiedFiles.Add(file);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            return modifiedFiles;
        }

        public static async Task<string> CalculateMD5ChecksumAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                var stream = new FileStream(path, FileMode.Open);
                var checksum = await CalculateMD5ChecksumAsync(stream, cancellationToken);
                stream.Close();
                return checksum;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<string> CalculateMD5ChecksumAsync(Stream stream, CancellationToken cancellationToken)
        {
            var position = stream.Position;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable S4790 //Using weak hashing algorithms is security-sensitive
                using (var md5 = MD5.Create())
#pragma warning restore S4790
                {
                    var result = new TaskCompletionSource<bool>();
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await CalculateMD5ChecksumInternalAsync(md5, stream, cancellationToken);
                        }
                        finally
                        {
                            result.SetResult(true);
                        }
                    }, cancellationToken);
                    await result.Task;
                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                }
            }
            finally
            {
                stream.Position = position;
            }
        }

        static async Task CalculateMD5ChecksumInternalAsync(MD5 md5, Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[k_MD5_bufferSize];
            int bytesRead;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                bytesRead = await stream.ReadAsync(buffer, 0, k_MD5_bufferSize, cancellationToken);
                if (bytesRead > 0)
                {
                    md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
            } while (bytesRead > 0);

            md5.TransformFinalBlock(buffer, 0, 0);
            await Task.CompletedTask;
        }

        static long GetLastModifiedDate(string path)
        {
            return ((DateTimeOffset)File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
        }

        static bool IsSameTimestamp(long timestamp, string path)
        {
            if (timestamp == 0L)
            {
                return false;
            }

            return timestamp == GetLastModifiedDate(path);
        }

        static async Task<bool> IsSameFileChecksumAsync(string checksum, string path, CancellationToken token)
        {
            if (string.IsNullOrEmpty(checksum))
            {
                return false;
            }

            var localChecksum = await CalculateMD5ChecksumAsync(path, token);
            return checksum == localChecksum;
        }
    }
}
