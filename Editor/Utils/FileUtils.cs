/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.IO;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class FileUtils
    {
        public const char WinSeparator = '\\';
        public const char UnixSeparator = '/';


        internal static string GetAssetFullPath(string assetPath)
        {
            if (assetPath.StartsWith("/Assets") || assetPath.StartsWith("Assets") || assetPath.StartsWith(@"\Assets"))
            {
                var basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var normalized = NormalizePathSeparators(assetPath);
                var combined = CombineWithSeparator(basePath, normalized);
                var path = Path.GetFullPath(combined);
                return path;
            }
            return assetPath;
        }

        internal static string CombineWithSeparator(string folder, string path)
        {
            if (Path.IsPathRooted(path))
            {
                path = path.TrimStart(Path.DirectorySeparatorChar);
                path = path.TrimStart(Path.AltDirectorySeparatorChar);
            }

            return Path.Combine(folder, path);
        }
        internal static string NormalizePathSeparators(this string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (Path.DirectorySeparatorChar == WinSeparator)
                path = path.Replace(UnixSeparator, WinSeparator);
            if (Path.DirectorySeparatorChar == UnixSeparator)
                path = path.Replace(WinSeparator, UnixSeparator);

            return path.Replace(string.Concat(WinSeparator, WinSeparator), WinSeparator.ToString());
        }

        internal static string NormalizeWindowsToUnix(this string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            return path.Replace(WinSeparator, UnixSeparator);
        }

        internal static bool IsFileInProjectRootDirectory(string fileName)
        {
            var relative = MakeRelativeToProjectPath(fileName);
            if (string.IsNullOrEmpty(relative))
                return false;

            return relative == Path.GetFileName(relative);
        }

        public static string MakeAbsolutePath(this string path)
        {
            if (string.IsNullOrEmpty(path)) { return string.Empty; }
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        }
        
        // returns null if outside of the project scope
        internal static string MakeRelativeToProjectPath(string fileName)
        {
            var basePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            fileName = NormalizePathSeparators(fileName);

            if (!Path.IsPathRooted(fileName))
                fileName = Path.Combine(basePath, fileName);

            if (!fileName.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return null;

            return fileName
                .Substring(basePath.Length)
                .Trim(Path.DirectorySeparatorChar);
        }
    }
}
