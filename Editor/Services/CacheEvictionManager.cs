using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface ICacheEvictionManager : IService
    {
        void OnCheckEvictConditions(string filePathToAddToCache);
    }

    class CacheEvictionManager : BaseService<ICacheEvictionManager>, ICacheEvictionManager
    {
        double m_CurrentSizeMb;

        [SerializeReference]
        IFileInfoWrapper m_FileInfoWrapper;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        [ServiceInjection]
        public void Inject(IFileInfoWrapper fileInfoWrapper, ISettingsManager settingsManager)
        {
            m_FileInfoWrapper = fileInfoWrapper;
            m_SettingsManager = settingsManager;
        }

        public override void OnEnable()
        {
            SubscribeToEvents();
        }

        void SubscribeToEvents()
        {
            CacheEvaluationEvent.EvaluateCache += OnCheckEvictConditions;
        }

        public void OnCheckEvictConditions(string filePathToAddToCache)
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
                if (m_CurrentSizeMb < m_SettingsManager.MaxCacheSizeMb)
                    return;
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

        void Evict(IEnumerable<FileInfo> files, double currentCacheSize)
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
