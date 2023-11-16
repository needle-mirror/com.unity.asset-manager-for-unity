using System.Collections.Generic;
using System.IO;
using System.Linq;
using Debug = UnityEngine.Debug;

namespace Unity.AssetManager.Editor
{
    internal interface ICacheEvictionManager : IService
    {
        void CheckEvictConditions(string filePathToAddToCache);
    }

    internal class CacheEvictionManager : BaseService<ICacheEvictionManager>, ICacheEvictionManager
    {
        double m_CurrentSizeMb;
        private readonly IFileInfoWrapper m_FileInfoWrapper;
        private readonly ISettingsManager m_SettingsManager;
        public CacheEvictionManager(IFileInfoWrapper fileInfoWrapper, ISettingsManager settingsManager)
        {
            m_FileInfoWrapper = RegisterDependency(fileInfoWrapper);
            m_SettingsManager = RegisterDependency(settingsManager);

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            CacheEvaluationEvent.evaluateCache += CheckEvictConditions;
        }

        public void CheckEvictConditions(string filePathToAddToCache)
        {
            var files = m_FileInfoWrapper.GetOldestFilesFromDirectory(m_SettingsManager
                .ThumbnailsCacheLocation);

            if (!files.Any())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(filePathToAddToCache))
            {
                m_CurrentSizeMb = m_FileInfoWrapper.GetFilesSizeMb(files);
                if (m_CurrentSizeMb < m_SettingsManager.MaxCacheSizeMb)
                {
                    return;
                }
            }
            else
            {
                m_CurrentSizeMb += m_FileInfoWrapper.GetFileLengthMb(filePathToAddToCache);
                if (m_CurrentSizeMb < m_SettingsManager.MaxCacheSizeMb) return;
            }

            Evict(files, m_CurrentSizeMb);
        }

        private double CalculateSizeToBeRemovedMb(double currentCacheSize)
        {
            if (m_SettingsManager.MaxCacheSizeMb == Constants.DefaultCacheSizeMb)
            {
                return currentCacheSize - (m_SettingsManager.MaxCacheSizeMb - Constants.ShrinkSizeInMb);
            }

            return Constants.ShrinkSizeInMb;
        }

        private void Evict(IEnumerable<FileInfo> files, double currentCacheSize)
        {
            var shrinkSize = CalculateSizeToBeRemovedMb(currentCacheSize);

            foreach (var file in files)
            {
                // we received the length in bytes so we transfer in Mb
                shrinkSize -= m_FileInfoWrapper.GetFileLengthMb(file);
                m_FileInfoWrapper.DeleteFile(file);
                if (shrinkSize <= 0)
                {
                    break;
                }
            }
        }
    }
}