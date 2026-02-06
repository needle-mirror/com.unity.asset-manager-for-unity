using System;
using System.IO;

namespace Unity.AssetManager.Core.Editor
{
    enum FileEventType
    {
        Modified,  // Changed, Created, or Renamed (destination)
        Removed    // Deleted or Renamed (source)
    }

    class FileEventArgs : EventArgs
    {
        public FileEventType EventType { get; }
        public string FullPath { get; }
        public string Directory { get; }
        public string FileName { get; }

        public FileEventArgs(FileEventType eventType, string fullPath)
        {
            EventType = eventType;
            FullPath = fullPath ?? string.Empty;
            Directory = string.IsNullOrEmpty(fullPath) ? string.Empty : Path.GetDirectoryName(fullPath) ?? string.Empty;
            FileName = string.IsNullOrEmpty(fullPath) ? string.Empty : Path.GetFileName(fullPath);
        }
    }

    interface IFileWatcher : IDisposable
    {
        event EventHandler<FileEventArgs> FileModified;
        event EventHandler<FileEventArgs> FileRemoved;

        void Initialize(string path);
    }
}

