using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Cloud.CommonEmbedded;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Highlight color for info-level dev logs (green/cyan = added/modified, yellow = corrected/migration, red = removed).
    /// </summary>
    internal enum DevLogHighlightColor
    {
        None,
        Cyan,   // added/modified (info)
        Yellow, // corrected event, migration detection
        Red     // removed
    }

    static class Utilities
    {
        static readonly string[] k_SizeSuffixes = {"B", "Kb", "Mb", "Gb", "Tb"};
        internal const string k_DevLogPrefix = "<color=#00CED1>[AM4U_DEV]</color>";
        const string k_DevLogHighlightColor = "#00CED1";   // cyan — info highlight
        const string k_DevLogWarningHighlightColor = "#FFD700"; // yellow/gold — warning highlight
        const string k_DevLogErrorHighlightColor = "#FF0000";   // red — error highlight
        const string k_DevLogTagColor = "#C58AF9";              // light purple — custom subsystem tag

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

        public static long DatetimeToTimestamp(DateTime value)
        {
            return (long) (value - AssetManagerCoreConstants.UnixEpoch).TotalMilliseconds;
        }

        public static string DatetimeToString(DateTime? value)
        {
            return value?.ToLocalTime().ToString("G");
        }

        /// <summary>
        /// Formats a DateTime as relative time (e.g., "now", "2h ago").
        /// Returns "now" if within 5 minutes.
        /// </summary>
        public static string FormatRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 5)
                return "now";
            if (timeSpan.TotalHours < 1)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalDays < 1)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays}d ago";

            return $"{(int)(timeSpan.TotalDays / 30)}mo ago";
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

        /// <summary>
        /// Logs a dev message. Use <see cref="DevLogWarning"/> or <see cref="DevLogError"/> for the appropriate severity.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="highlight">When true, colors the message (cyan) for priority information.</param>
        /// <param name="tag">Optional subsystem tag (rendered in purple after the AM4U_DEV prefix) for grep-friendly categorization.</param>
        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLog(string message, bool highlight = false, string tag = null)
        {
            var body = highlight ? $"<color={k_DevLogHighlightColor}>{message}</color>" : message;
            Debug.Log($"{FormatPrefix(tag)} {body}");
        }

        /// <summary>
        /// Logs a dev message at info level with a highlight color (e.g. red for removed, yellow for migration/corrected, cyan for added/modified).
        /// Use for informational file/move/migration messages that should not appear as warnings or errors.
        /// </summary>
        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLog(string message, DevLogHighlightColor highlightColor)
        {
            if (highlightColor == DevLogHighlightColor.None)
            {
                Debug.Log($"{k_DevLogPrefix} {message}");
                return;
            }
            var color = highlightColor == DevLogHighlightColor.Red ? k_DevLogErrorHighlightColor
                : highlightColor == DevLogHighlightColor.Yellow ? k_DevLogWarningHighlightColor
                : k_DevLogHighlightColor;
            var body = $"<color={color}>{message}</color>";
            Debug.Log($"{k_DevLogPrefix} {body}");
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevAssert(bool condition, string message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                Debug.Assert(condition, k_DevLogPrefix);
            }
            else
            {
                Debug.Assert(condition, $"{k_DevLogPrefix} {message}");
            }
        }

        /// <summary>
        /// Logs a dev error message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="highlight">When true, colors the message (red) for priority information.</param>
        /// <param name="tag">Optional subsystem tag (rendered in purple after the AM4U_DEV prefix) for grep-friendly categorization.</param>
        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogError(string message, bool highlight = false, string tag = null)
        {
            var body = highlight ? $"<color={k_DevLogErrorHighlightColor}>{message}</color>" : message;
            Debug.LogError($"{FormatPrefix(tag)} {body}");
        }

        /// <summary>
        /// Returns a short, human-readable message suitable for UI or logs. Avoids full exception payloads
        /// (e.g. ServiceError) and uses Detail/Title or the first line of Message when appropriate.
        /// </summary>
        public static string GetUserFacingErrorMessage(Exception ex)
        {
            if (ex == null)
                return "An error occurred.";

            if (ex is ServiceException se)
            {
                var detailMessage = TryGetDetailErrorMessage(se);
                if (!string.IsNullOrWhiteSpace(detailMessage))
                    return detailMessage.Trim();
                if (!string.IsNullOrWhiteSpace(se.Detail))
                    return se.Detail.Trim();
                if (!string.IsNullOrWhiteSpace(se.Title))
                    return se.Title.Trim();
                return "Validation failed.";
            }

            var msg = ex.Message?.Trim() ?? "";
            if (string.IsNullOrEmpty(msg))
                return "An error occurred. Please try again.";
            if (msg.IndexOf('\n') < 0 && msg.Length <= 200)
                return msg;
            var firstLine = msg.Split('\n')[0].Trim();
            return firstLine.Length <= 200 ? firstLine : firstLine.Substring(0, 197) + "...";
        }

        /// <summary>
        /// Tries to extract errorMessage from ServiceException Details or Message (handles JSON-style and C#-style serialization).
        /// </summary>
        static string TryGetDetailErrorMessage(ServiceException se)
        {
            var sources = new List<string>();
            if (se.Details != null)
            {
                foreach (var d in se.Details)
                {
                    var s = d?.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) sources.Add(s);
                }
            }
            if (sources.Count == 0 && !string.IsNullOrWhiteSpace(se.Message))
                sources.Add(se.Message);

            foreach (var text in sources)
            {
                var value = MatchErrorMessageValue(text);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return null;
        }

        static string MatchErrorMessageValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            // Double-quoted JSON: "errorMessage": "Name must not start with whitespace" (allow escaped \" in value)
            var m = Regex.Match(text, @"""errorMessage""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.IgnoreCase);
            if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                return m.Groups[1].Value.Replace("\\\"", "\"").Trim();
            // Single-quoted
            m = Regex.Match(text, @"'errorMessage'\s*:\s*'([^']*)'", RegexOptions.IgnoreCase);
            if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                return m.Groups[1].Value.Trim();
            m = Regex.Match(text, @"""errorMessage""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
            if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                return m.Groups[1].Value.Trim();
            return null;
        }

        /// <summary>
        /// Logs a dev warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="highlight">When true, colors the message (yellow) for priority information.</param>
        /// <param name="tag">Optional subsystem tag (rendered in purple after the AM4U_DEV prefix) for grep-friendly categorization.</param>
        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogWarning(string message, bool highlight = false, string tag = null)
        {
            var body = highlight ? $"<color={k_DevLogWarningHighlightColor}>{message}</color>" : message;
            Debug.LogWarning($"{FormatPrefix(tag)} {body}");
        }

        [System.Diagnostics.Conditional("AM4U_DEV")]
        public static void DevLogException(Exception e)
        {
            Debug.LogError($"{k_DevLogPrefix} Exception: {e.GetType().Name}");
            Debug.LogException(e);
        }

        static string FormatPrefix(string tag)
        {
            return string.IsNullOrEmpty(tag)
                ? k_DevLogPrefix
                : $"{k_DevLogPrefix}<color={k_DevLogTagColor}>[{tag}]</color>";
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

            var application = ServicesContainer.instance.Resolve<IApplicationProxy>();
            var relativePath = Path.GetRelativePath(application.DataPath, assetPath);
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

        public static bool IsSubdirectoryOrSame(string subdirectoryPath, string directoryPath)
        {
            if (string.IsNullOrEmpty(subdirectoryPath))
                return false;

            var directory = new DirectoryInfo(directoryPath);
            var subdirectory = new DirectoryInfo(subdirectoryPath);

            return subdirectory.FullName.StartsWith(directory.FullName);
        }
        public static string NormalizePathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var application = ServicesContainer.instance.Resolve<IApplicationProxy>();

            // Path normalization depends on the current OS
            var str = application.Platform == RuntimePlatform.WindowsEditor ?
                path.Replace('/', Path.DirectorySeparatorChar) :
                path.Replace('\\', Path.DirectorySeparatorChar);

            var pattern = Path.DirectorySeparatorChar == '\\' ? "\\\\+" : "/+";
            return Regex.Replace(str, pattern, Path.DirectorySeparatorChar.ToString());
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
    }
}
