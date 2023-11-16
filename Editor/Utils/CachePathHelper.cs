using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal static class CachePathHelper
    {
        internal const string k_EmptyFolderError = "Path is empty.";
        internal const string k_DirectoryDoesIsReadonly = "Directory is readonly.";

        internal static string GetDefaultCacheLocation()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return GetWindowsLocation();
                case RuntimePlatform.OSXEditor:
                    return GetMacLocation();
                case RuntimePlatform.LinuxEditor:
                    return GetLinuxLocation();
                default:
                    return string.Empty;
            }
        }

        internal static CacheLocationValidationResult ValidateBaseCacheLocation(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new CacheLocationValidationResult
                {
                    Message = k_EmptyFolderError,
                    Success = false
                };
            }

            var directoryInfo = new DirectoryInfo(path);

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
            {
                return new CacheLocationValidationResult
                {
                    Message = k_DirectoryDoesIsReadonly,
                    Success = false
                };
            }

            // todo check if there is enough cache size
            //var freeSpace = Utils.GetTotalFreeSpaceOnDisk()

            return new CacheLocationValidationResult
            {
                Success = true
            };
        }

        internal static string CreateAssetManagerCacheLocation(string path)
        {
            string cacheLocation;

            var directory = new DirectoryInfo(path).Name;
            // the base cache location already contains a folder for the asset manager
            if (string.Equals(directory, Constants.AssetManagerCacheLocationFolder))
            {
                cacheLocation = path;
            }
            else
            {
                cacheLocation = Path.Combine(path, Constants.AssetManagerCacheLocationFolder);
            }

            if (!Directory.Exists(cacheLocation))
                Directory.CreateDirectory(cacheLocation);

            return cacheLocation;
        }

        static string GetWindowsLocation()
        {
            return ManageCacheLocation(new string[] { "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        static string GetMacLocation()
        {
            return ManageCacheLocation(new string[] { "Library", "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        static string GetLinuxLocation()
        {
            return ManageCacheLocation(new string[] { ".config", "unity3d", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        static string ManageCacheLocation(string[] paths)
        {
            var location = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), paths.Aggregate(Path.Combine));

            CreateFolderIfNotExist(location);
            return location;
        }

        static void CreateFolderIfNotExist(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
