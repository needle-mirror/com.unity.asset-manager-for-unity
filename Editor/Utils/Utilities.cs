using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class Utilities
    {
        static readonly IDialogManager k_DefaultDialogManager = new DialogManager();

        static readonly string[] k_SizeSuffixes = { "B", "Kb", "Mb", "Gb", "Tb" };

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
            return (long)(value - Constants.UnixEpoch).TotalMilliseconds;
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
            return string.Equals(NormalizePathSeparators(path1), NormalizePathSeparators(path2), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Path normalization depends on the current OS
            var str = Application.platform == RuntimePlatform.WindowsEditor
                ? path.Replace('/', Path.DirectorySeparatorChar)
                : path.Replace('\\', Path.DirectorySeparatorChar);

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

        public static IEnumerable<string> GetValidAssetDependencyGuids(string assetGuid, bool recursive)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

            foreach (var dependencyPath in AssetDatabase.GetDependencies(assetPath, recursive))
            {
                if (!IsPathInsideAssetsFolder(dependencyPath))
                    continue;

                var dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);

                if (dependencyGuid == assetGuid)
                    continue;

                yield return dependencyGuid;
            }
        }

        public static bool IsGuidInsideAssetsFolder(string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return IsPathInsideAssetsFolder(assetPath);
        }

        public static bool IsPathInsideAssetsFolder(string assetPath)
        {
            return assetPath.Replace('\\', '/').ToLower().StartsWith("assets/");
        }
    }
}
