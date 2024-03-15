using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class Utilities
    {
        static readonly string[] k_SizeSuffixes = { "B", "Kb", "Mb", "Gb", "Tb" };

        internal static string BytesToReadableString(double bytes)
        {
            if (bytes == 0)
                return $"0 {k_SizeSuffixes[0]}";
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            var value = Math.Sign(bytes) * num;

            return place >= k_SizeSuffixes.Length ? $"{bytes} {k_SizeSuffixes[0]}" : $"{value} {k_SizeSuffixes[place]}";
        }

        public static string NormalizePath(string path)
        {
            return EscapeBackslashes(Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public static string EscapeBackslashes(this string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace(@"\", @"\\");
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

        public static bool IsDevMode => EditorPrefs.GetBool("DeveloperMode", false);

        public static void DevLog(string message)
        {
            if (IsDevMode)
            {
                Debug.Log(message);
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
    }
}