using System;
using System.IO;
using System.Linq;
using System.Security;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface ICachePathHelper : IService
    {
        IDirectoryInfoProxy EnsureDirectoryExists(string path);
        CacheLocationValidationResult EnsureBaseCacheLocation(string cacheLocation);
        string CreateAssetManagerCacheLocation(string path);
        IDirectoryInfoProxy GetDefaultCacheLocation();
    }

    class CachePathHelper : BaseService<ICachePathHelper>, ICachePathHelper
    {
        const string k_UnsupportedPlatformError = "Unsupported platform";

        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;

        [SerializeReference]
        IDirectoryInfoFactory m_DirectoryInfoFactory;

        [SerializeReference]
        IIOProxy m_IOProxy;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IApplicationProxy applicationProxy, IDirectoryInfoFactory directoryInfoFactory)
        {
            m_IOProxy = ioProxy;
            m_ApplicationProxy = applicationProxy;
            m_DirectoryInfoFactory = directoryInfoFactory;
        }

        public IDirectoryInfoProxy GetDefaultCacheLocation()
        {
            switch (m_ApplicationProxy.Platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return GetWindowsLocation();
                case RuntimePlatform.OSXEditor:
                    return GetMacLocation();
                case RuntimePlatform.LinuxEditor:
                    return GetLinuxLocation();
                default:
                    throw new Exception(k_UnsupportedPlatformError);
            }
        }

        public CacheLocationValidationResult EnsureBaseCacheLocation(string cacheLocation)
        {
            var validationResult = new CacheLocationValidationResult
            {
                Success = false
            };

            try
            {
                var directoryInfo = EnsureDirectoryExists(cacheLocation);
                var filePath = Path.Combine(directoryInfo.FullName, Path.GetRandomFileName());
                using var fs = m_IOProxy.Create(filePath, 1, FileOptions.DeleteOnClose);

                validationResult.Success = true;
            }
            catch (DirectoryNotFoundException)
            {
                validationResult.ErrorType = CacheValidationResultError.DirectoryNotFound;
            }
            catch (PathTooLongException)
            {
                validationResult.ErrorType = CacheValidationResultError.PathTooLong;
            }
            catch (ArgumentException)
            {
                validationResult.ErrorType = CacheValidationResultError.InvalidPath;
            }
            catch (SecurityException)
            {
                validationResult.ErrorType = CacheValidationResultError.CannotWriteToDirectory;
            }
            catch (Exception)
            {
                validationResult.ErrorType = CacheValidationResultError.CannotWriteToDirectory;
            }

            return validationResult;
        }

        public string CreateAssetManagerCacheLocation(string path)
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

            if (!m_IOProxy.DirectoryExists(cacheLocation))
            {
                m_IOProxy.CreateDirectory(cacheLocation);
            }

            return cacheLocation;
        }

        public IDirectoryInfoProxy EnsureDirectoryExists(string path)
        {
            var directoryInfo = m_DirectoryInfoFactory.Create(path);

            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            return directoryInfo;
        }

        IDirectoryInfoProxy GetWindowsLocation()
        {
            return ManageCacheLocation(new[] { "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy GetMacLocation()
        {
            return ManageCacheLocation(new[]
                { "Library", "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy GetLinuxLocation()
        {
            return ManageCacheLocation(new[]
                { ".config", "unity3d", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy ManageCacheLocation(string[] paths)
        {
            var location = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), paths.Aggregate(Path.Combine));

            return EnsureDirectoryExists(location);
        }
    }
}
