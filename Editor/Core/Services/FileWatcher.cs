using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class FileWatcher : IFileWatcher, ISerializationCallbackReceiver
    {

        [Serializable]
        struct PendingFileEvent
        {
            public FileEventType EventType;
            public string FullPath;
            public DateTime LastEventTime;

            public PendingFileEvent(FileEventType eventType, string fullPath)
            {
                EventType = eventType;
                FullPath = fullPath;
                LastEventTime = DateTime.UtcNow;
            }
        }

        FileSystemWatcher m_FileSystemWatcher;

        [SerializeReference]
        IIOProxy m_IOProxy;

        // FileSystemWatcher events are raised on a background thread
        // Queue the raw events to be processed on the main thread
        ConcurrentQueue<(WatcherChangeTypes changeType, string fullPath, string oldFullPath)> m_RawEventQueue = new();

        // Debounced events - Key: file path, Value: pending event
        // This merges multiple events for the same file within the debounce window
        Dictionary<string, PendingFileEvent> m_DebouncedEvents = new();

        // Debounce window in seconds - events within this window are merged
        internal const float k_DebounceWindowSeconds = 0.5f;

        [SerializeField]
        bool m_IsInitialized;

        [SerializeField]
        List<int> m_SerializedEventTypes = new();

        [SerializeField]
        List<string> m_SerializedFullPaths = new();

        [SerializeField]
        List<string> m_SerializedOldFullPaths = new();

        public event EventHandler<FileEventArgs> FileModified;
        public event EventHandler<FileEventArgs> FileRemoved;

        public FileWatcher(IIOProxy ioProxy)
        {
            m_IOProxy = ioProxy;
        }

        public void Initialize(string path)
        {
            if (m_IsInitialized)
            {
                Utilities.DevLogWarning("FileWatcher is already initialized for path.");
                return;
            }

            if (!m_IOProxy.DirectoryExists(path))
            {
                m_IOProxy.CreateDirectory(path);
            }

            // On Linux, adding CreationTime helps with inotify event detection
            // On Windows/macOS, it can interfere with event detection, so only use FileName and LastWrite
            var notifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            #if UNITY_EDITOR_LINUX
            notifyFilter |= NotifyFilters.CreationTime;
            #endif

            m_FileSystemWatcher = new FileSystemWatcher(path)
            {
                NotifyFilter = notifyFilter,
                IncludeSubdirectories = true,
                EnableRaisingEvents = false,
                InternalBufferSize = 65536  // 64KB maximum - helps prevent buffer overflow on Linux
            };

            m_FileSystemWatcher.Changed += OnFileChanged;
            m_FileSystemWatcher.Created += OnFileCreated;
            m_FileSystemWatcher.Deleted += OnFileDeleted;
            m_FileSystemWatcher.Renamed += OnFileRenamed;
            m_FileSystemWatcher.Error += OnError;
            m_FileSystemWatcher.EnableRaisingEvents = true;

            EditorApplication.update += ProcessMainThreadQueue;
            m_IsInitialized = true;
        }

        public void Dispose()
        {
            if (m_IsInitialized)
            {
                EditorApplication.update -= ProcessMainThreadQueue;
                m_IsInitialized = false;
            }

            if (m_FileSystemWatcher != null)
            {
                m_FileSystemWatcher.Changed -= OnFileChanged;
                m_FileSystemWatcher.Created -= OnFileCreated;
                m_FileSystemWatcher.Deleted -= OnFileDeleted;
                m_FileSystemWatcher.Renamed -= OnFileRenamed;
                m_FileSystemWatcher.Error -= OnError;
                m_FileSystemWatcher.Dispose();
                m_FileSystemWatcher = null;
            }

            m_RawEventQueue.Clear();
            m_DebouncedEvents.Clear();
            m_SerializedEventTypes.Clear();
            m_SerializedFullPaths.Clear();
            m_SerializedOldFullPaths.Clear();
        }

        void ProcessMainThreadQueue()
        {
            var now = DateTime.UtcNow;

            // Process raw events from FileSystemWatcher and add them to debounced events
            while (m_RawEventQueue.TryDequeue(out var rawEvent))
            {
                try
                {
                    ProcessRawEvent(rawEvent.changeType, rawEvent.fullPath, rawEvent.oldFullPath);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // Fire events for files that have passed the debounce window
            var eventsToFire = new List<(string path, PendingFileEvent evt)>();
            foreach (var kvp in m_DebouncedEvents)
            {
                var timeSinceLastEvent = (now - kvp.Value.LastEventTime).TotalSeconds;
                if (timeSinceLastEvent >= k_DebounceWindowSeconds)
                {
                    eventsToFire.Add((kvp.Key, kvp.Value));
                }
            }

            // Remove and fire the events
            foreach (var (path, evt) in eventsToFire)
            {
                m_DebouncedEvents.Remove(path);
                try
                {
                    FireEvent(evt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        void ProcessRawEvent(WatcherChangeTypes changeType, string fullPath, string oldFullPath)
        {
            switch (changeType)
            {
                case WatcherChangeTypes.Changed:
                case WatcherChangeTypes.Created:
                    // Both Changed and Created indicate the file exists and has content
                    // Update the timestamp - we'll determine the final state when firing
                    m_DebouncedEvents[fullPath] = new PendingFileEvent(FileEventType.Modified, fullPath);
                    break;

                case WatcherChangeTypes.Deleted:
                    // Mark as potentially removed, but we'll verify file existence before firing
                    // This handles cases where delete is part of an atomic write operation
                    m_DebouncedEvents[fullPath] = new PendingFileEvent(FileEventType.Removed, fullPath);
                    break;

                case WatcherChangeTypes.Renamed:
                    // Renamed files: old path is removed, new path is modified
                    if (!string.IsNullOrEmpty(oldFullPath))
                    {
                        m_DebouncedEvents[oldFullPath] = new PendingFileEvent(FileEventType.Removed, oldFullPath);
                    }
                    m_DebouncedEvents[fullPath] = new PendingFileEvent(FileEventType.Modified, fullPath);
                    break;
            }
        }

        void FireEvent(PendingFileEvent evt)
        {
            // Determine the final event type based on actual file existence
            // This handles atomic write operations where delete events occur mid-operation
            var fileExists = m_IOProxy.FileExists(evt.FullPath);

            // Only mark as removed if the file truly doesn't exist
            var finalEventType = fileExists ? FileEventType.Modified : FileEventType.Removed;

            // If the tracked event type doesn't match reality, log it for debugging
            if (evt.EventType != finalEventType)
            {
                var fileName = Path.GetFileName(evt.FullPath);
                Utilities.DevLog($"FileWatcher: Corrected event type for {fileName} from {evt.EventType} to {finalEventType} based on file existence", DevLogHighlightColor.Yellow);
            }

            var args = new FileEventArgs(finalEventType, evt.FullPath);

            switch (finalEventType)
            {
                case FileEventType.Modified:
                    FileModified?.Invoke(this, args);
                    break;

                case FileEventType.Removed:
                    FileRemoved?.Invoke(this, args);
                    break;
            }
        }

        void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            m_RawEventQueue.Enqueue((WatcherChangeTypes.Changed, e.FullPath, null));
        }

        void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            m_RawEventQueue.Enqueue((WatcherChangeTypes.Created, e.FullPath, null));
        }

        void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            m_RawEventQueue.Enqueue((WatcherChangeTypes.Deleted, e.FullPath, null));
        }

        void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            m_RawEventQueue.Enqueue((WatcherChangeTypes.Renamed, e.FullPath, e.OldFullPath));
        }

        void OnError(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            Utilities.DevLogError($"FileWatcher buffer overflow or error: {exception?.Message ?? "Unknown error"}. " +
                          "Some file system events may have been missed. " +
                          "This can happen on Linux systems under heavy file I/O load.");
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_SerializedEventTypes.Clear();
            m_SerializedFullPaths.Clear();
            m_SerializedOldFullPaths.Clear();

            while (m_RawEventQueue.TryDequeue(out var rawEvent))
            {
                m_SerializedEventTypes.Add((int)rawEvent.changeType);
                m_SerializedFullPaths.Add(rawEvent.fullPath);
                m_SerializedOldFullPaths.Add(rawEvent.oldFullPath ?? string.Empty);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_IsInitialized)
            {
                EditorApplication.update -= ProcessMainThreadQueue;
                EditorApplication.update += ProcessMainThreadQueue;
            }

            m_RawEventQueue = new ConcurrentQueue<(WatcherChangeTypes changeType, string fullPath, string oldFullPath)>();
            for (int i = 0; i < m_SerializedEventTypes.Count; i++)
            {
                var oldFullPath = string.IsNullOrEmpty(m_SerializedOldFullPaths[i]) ? null : m_SerializedOldFullPaths[i];
                m_RawEventQueue.Enqueue(((WatcherChangeTypes)m_SerializedEventTypes[i], m_SerializedFullPaths[i], oldFullPath));
            }

            m_DebouncedEvents = new Dictionary<string, PendingFileEvent>();

            m_SerializedEventTypes.Clear();
            m_SerializedFullPaths.Clear();
            m_SerializedOldFullPaths.Clear();
        }


    }
}

