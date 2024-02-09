using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    internal interface IDownloadManager : IService
    {
        event Action<DownloadOperation> onDownloadProgress;
        event Action<DownloadOperation> onDownloadFinalized;

        DownloadOperation StartDownload(string url, string path);

        DownloadOperation CreateDownloadOperation(string url, string path);
        void StartDownload(DownloadOperation operation);
        
        void Cancel(ulong downloadId);
    }

    [Serializable]
    internal class DownloadManager : BaseService<IDownloadManager>, IDownloadManager, ISerializationCallbackReceiver
    {
        private const int k_MaxConcurrentDownloads = 30;
        public event Action<DownloadOperation> onDownloadProgress = delegate {};
        public event Action<DownloadOperation> onDownloadFinalized = delegate {};

        private readonly Dictionary<ulong, IWebRequestItem> m_WebRequests = new();

        [SerializeField]
        private ulong m_LastDownloadOperationId = 0;
        [SerializeField]
        private List<DownloadOperation> m_PendingDownloads = new();
        [SerializeField]
        private List<ulong> m_PendingCancellations = new();
        [SerializeField]
        private DownloadOperation[] m_PendingResume = Array.Empty<DownloadOperation>();

        private readonly List<DownloadOperation> m_DownloadInProgress = new();

        private readonly IWebRequestProxy m_WebRequestProxy;
        private readonly IIOProxy m_IOProxy;
        public DownloadManager(IWebRequestProxy webRequestProxy, IIOProxy ioProxy)
        {
            m_WebRequestProxy = RegisterDependency(webRequestProxy);
            m_IOProxy = RegisterDependency(ioProxy);
        }

        public DownloadOperation StartDownload(string url, string path)
        {
            var operation = CreateDownloadOperation(url, path);
            StartDownload(operation);
            return operation;
        }
        
        public DownloadOperation CreateDownloadOperation(string url, string path)
        {
            return new DownloadOperation
            {
                id = ++m_LastDownloadOperationId,
                url = url,
                path = path,
                status = OperationStatus.InProgress
            };
        }
        
        public void StartDownload(DownloadOperation operation)
        {
            m_PendingDownloads.Add(operation);
        }

        // We put cancellation and new downloads in `pending` list to be processed in the next update because
        // we don't want to accidentally modify the `m_DownloadInProgress` list when it's being iterated on
        public void Cancel(ulong downloadId)
        {
            m_PendingCancellations.Add(downloadId);
        }

        public override void OnEnable()
        {
            EditorApplication.update += Update;
        }

        public override void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            HandleResume();
            HandleCancellation();

            var numDownloadsToAdd = Math.Min(k_MaxConcurrentDownloads - m_DownloadInProgress.Count, m_PendingDownloads.Count);
            if (numDownloadsToAdd > 0)
            {
                var initializedOperations = new List<DownloadOperation>();
                try
                {
                    foreach (var operation in m_PendingDownloads.Take(numDownloadsToAdd))
                    {
                        InitializeOperation(operation);
                        initializedOperations.Add(operation);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                m_PendingDownloads.RemoveAll(op => initializedOperations.Contains(op));
            }

            if (m_DownloadInProgress.Count == 0)
                return;

            foreach (var operation in m_DownloadInProgress)
                UpdateOperation(operation);

            m_DownloadInProgress.RemoveAll(o => o.status != OperationStatus.InProgress);
        }

        private void HandleResume()
        {
            if (m_PendingResume == null || m_PendingResume.Length == 0)
                return;

            foreach (var operation in m_PendingResume)
            {
                var fileSizeInBytes = m_IOProxy.GetFileSizeInBytes(operation.path);
                if (fileSizeInBytes <= 0 || fileSizeInBytes > operation.totalBytes)
                    InitializeOperation(operation);
                else if (fileSizeInBytes == operation.totalBytes)
                {
                    operation.progress = 1.0f;
                    FinalizeOperation(operation, null, OperationStatus.Success);
                }
                else
                    InitializeOperation(operation, true, $"bytes={fileSizeInBytes}-");
            }
            m_PendingResume = null;
        }

        private void HandleCancellation()
        {
            if (m_PendingCancellations == null || m_PendingCancellations.Count == 0)
                return;

            foreach (var downloadId in m_PendingCancellations)
            {
                var downloadOperation = m_PendingDownloads.Concat(m_DownloadInProgress).FirstOrDefault(i => i.id == downloadId);
                if (downloadOperation == null)
                    continue;
                m_PendingDownloads.RemoveAll(i => i.id == downloadId);
                m_DownloadInProgress.RemoveAll(i => i.id == downloadId);
                if (m_WebRequests.TryGetValue(downloadId, out var request) && !request.isDone)
                    request.Abort();
                FinalizeOperation(downloadOperation, request, OperationStatus.Cancelled);
            }
            m_PendingCancellations.Clear();
        }

        private void InitializeOperation(DownloadOperation operation, bool append = false, string bytesRange = null)
        {
            var newRequest = m_WebRequestProxy.SendWebRequest(operation.url, operation.path, append, bytesRange);
            m_WebRequests[operation.id] = newRequest;
            operation.status = OperationStatus.InProgress;
            m_DownloadInProgress.Add(operation);
        }

        private void FinalizeOperation(DownloadOperation operation, IWebRequestItem request, OperationStatus finalStatus, string errorMessage = null)
        {
            if (!string.IsNullOrEmpty(errorMessage))
                Debug.LogError($"Encountered error while downloading {Path.GetFileName(operation.path)}: {errorMessage}");

            operation.status = finalStatus;
            operation.error = errorMessage ?? string.Empty;
            m_WebRequests.Remove(operation.id);
            request?.Dispose();
            onDownloadFinalized.Invoke(operation);
        }

        private void UpdateOperation(DownloadOperation operation)
        {
            if (!m_WebRequests.TryGetValue(operation.id, out var request))
                return;

            if (!string.IsNullOrEmpty(request.error))
            {
                FinalizeOperation(operation, request, OperationStatus.Error, request.error);
                return;
            }

            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    FinalizeOperation(operation, request, OperationStatus.Error, "Failed to communicate with the server.");
                    return;
                case UnityWebRequest.Result.ProtocolError:
                    FinalizeOperation(operation, request, OperationStatus.Error, "The server returned an error response.");
                    return;
                case UnityWebRequest.Result.DataProcessingError:
                    FinalizeOperation(operation, request, OperationStatus.Error, "Error processing data.");
                    return;
                case UnityWebRequest.Result.InProgress:
                case UnityWebRequest.Result.Success:
                default:
                    break;
            }

            if (request.isDone)
            {
                operation.progress = request.downloadProgress;
                FinalizeOperation(operation, request, OperationStatus.Success);
                return;
            }

            var progressUpdate = request.downloadProgress - operation.progress;
            // We are reducing how often we are reporting download progress to avoid expensive frequent UI refreshes.
            if (progressUpdate >= 0.05 || progressUpdate * operation.totalBytes > 1024 * 1024)
            {
                operation.progress = request.downloadProgress;
                onDownloadProgress?.Invoke(operation);
            }
        }

        public void OnBeforeSerialize()
        {
            m_PendingResume = m_DownloadInProgress.ToArray();
        }

        public void OnAfterDeserialize()
        {
        }
    }
}