using System;
using System.IO;
using System.Linq;
using System.Security;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface ICachePathHelper: IService
    {
        IDirectoryInfoProxy EnsureDirectoryExists(string path);
        CacheLocationValidationResult EnsureBaseCacheLocation(string cacheLocation);
        string CreateAssetManagerCacheLocation(string path);
        IDirectoryInfoProxy GetDefaultCacheLocation();
    }
    
    internal class CachePathHelper: BaseService<ICachePathHelper>, ICachePathHelper
    {
        internal const string k_UnsupportedPlatformError = "Unsupported platform";

        [SerializeReference]
        IIOProxy m_IOProxy;
        
        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;
        
        [SerializeReference]
        IDirectoryInfoFactory m_DirectoryInfoFactory;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IApplicationProxy applicationProxy, IDirectoryInfoFactory directoryInfoFactory)
        {
            m_IOProxy = ioProxy;
            m_ApplicationProxy = applicationProxy;
            m_DirectoryInfoFactory = directoryInfoFactory;
        }

        public IDirectoryInfoProxy GetDefaultCacheLocation()
        {
            switch (m_ApplicationProxy.platform)
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
            var validationResult = new CacheLocationValidationResult()
            {
                success = false
            };
            
            try
            {
                var directoryInfo = EnsureDirectoryExists(cacheLocation);
                var filePath = Path.Combine(directoryInfo.FullName, Path.GetRandomFileName());
                using var fs = m_IOProxy.Create(filePath, 1, FileOptions.DeleteOnClose);

                validationResult.success = true;
            }
            catch (DirectoryNotFoundException)
            {
                validationResult.errorType = CacheValidationResultError.DirectoryNotFound;
            }
            catch (PathTooLongException)
            {
                validationResult.errorType = CacheValidationResultError.PathTooLong;
            }
            catch (ArgumentException)
            {
                validationResult.errorType = CacheValidationResultError.InvalidPath;
            }
            catch (SecurityException)
            {
                validationResult.errorType = CacheValidationResultError.CannotWriteToDirectory;
            }
            catch (Exception)
            {
                validationResult.errorType = CacheValidationResultError.CannotWriteToDirectory;
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
                m_IOProxy.CreateDirectory(cacheLocation);

            return cacheLocation;
        }

        IDirectoryInfoProxy GetWindowsLocation()
        {
            return ManageCacheLocation(new string[] { "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy GetMacLocation()
        {
            return ManageCacheLocation(new string[] { "Library", "Unity", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy GetLinuxLocation()
        {
            return ManageCacheLocation(new string[] { ".config", "unity3d", "cache", Constants.AssetManagerCacheLocationFolder });
        }

        IDirectoryInfoProxy ManageCacheLocation(string[] paths)
        {
            var location = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile), paths.Aggregate(Path.Combine));

            return EnsureDirectoryExists(location);
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
    }
}
