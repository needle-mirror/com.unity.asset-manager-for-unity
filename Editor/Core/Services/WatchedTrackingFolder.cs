using System;

namespace Unity.AssetManager.Core.Editor
{
    class WatchedTrackingFolder : IDisposable
    {
        public string FolderPath { get; }

        readonly IFileWatcher m_Watcher;
        readonly EventHandler<FileEventArgs> m_OnModified;
        readonly EventHandler<FileEventArgs> m_OnRemoved;
        bool m_Started;

        public WatchedTrackingFolder(string folderPath, IFileWatcher watcher,
            EventHandler<FileEventArgs> onModified,
            EventHandler<FileEventArgs> onRemoved)
        {
            FolderPath = folderPath ?? string.Empty;
            m_Watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            m_OnModified = onModified;
            m_OnRemoved = onRemoved;
        }

        public void Start()
        {
            if (m_Started)
                return;
            m_Watcher.Initialize(FolderPath);
            m_Watcher.FileModified += m_OnModified;
            m_Watcher.FileRemoved += m_OnRemoved;
            m_Started = true;
        }

        public void Dispose()
        {
            if (!m_Started)
                return;
            m_Watcher.FileModified -= m_OnModified;
            m_Watcher.FileRemoved -= m_OnRemoved;
            m_Watcher.Dispose();
            m_Started = false;
        }
    }
}
