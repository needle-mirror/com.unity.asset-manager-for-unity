using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class Utilities
    {
        static readonly string[] k_SizeSuffixes = {"B", "Kb", "Mb", "Gb", "Tb"};

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

        public static async Task WaitForTasksAndHandleExceptions(IEnumerable<Task> tasks)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                foreach (var task in tasks)
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        Debug.LogException(task.Exception);
                    }
                }
            }
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

            if (baseList == null || extendedList == null || baseList.Count > extendedList.Count)
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

        public static string OpenFolderPanelInProject(string title, string defaultLocation)
        {
            var validPath = false;
            string importLocation = null;

            do
            {
                importLocation = EditorUtility.OpenFolderPanel(title, defaultLocation, string.Empty);

                validPath = importLocation.Contains(Application.dataPath) || string.IsNullOrEmpty(importLocation);

                if (!validPath)
                {
                    EditorUtility.DisplayDialog("Select a valid folder",
                        "The default import location must be located inside the Assets folder of your project.", "Ok");
                }
            } while (!validPath);

            return importLocation;
        }
    }
}
