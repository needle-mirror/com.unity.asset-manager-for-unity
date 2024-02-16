using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Unity.AssetManager.Editor
{
    internal static class Utilities
    {
        public static string[] k_SizeSuffixes = new string[] {"B", "Kb", "Mb", "Gb", "Tb"};

        // Sometimes when trying to display high resolution images at a small size they will look too sharp
        // This function makes them look normal at any size
        internal static string BytesToReadableString(double bytes)
        {
            if (bytes == 0)
                return $"0 {k_SizeSuffixes[0]}";
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            var value = Math.Sign(bytes) * num;

            return place >= k_SizeSuffixes.Length ? $"{bytes} {k_SizeSuffixes[0]}" : $"{value} {k_SizeSuffixes[place]}";
        }

        internal static string NormalizePath(string path)
        {
            return EscapeBackslashes(Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        internal static string EscapeBackslashes(this string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace(@"\", @"\\");
        }

        internal static bool DeleteAllFilesAndFoldersFromDirectory(string path)
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

        internal static long DatetimeToTimestamp(DateTime value)
        {
            return (long) (value - Constants.UnixEpoch).TotalMilliseconds;
        }

        internal static string PascalCaseToSentence(this string input)
        {
            return Regex.Replace(input, "(\\B[A-Z])", " $1");
        }
    }
}