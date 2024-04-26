using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Editor
{
    interface IDownloadManager : IService
    {
        event Action<DownloadOperation> DownloadProgress;
        event Action<DownloadOperation> DownloadFinalized;

        DownloadOperation CreateDownloadOperation(string url, string path);

        void StartDownload(DownloadOperation operation);

        void Cancel(ulong downloadId);
    }

    [Serializable]
    class DownloadManager : BaseService<IDownloadManager>, IDownloadManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        ulong m_LastDownloadOperationId;

        [SerializeField]
        List<DownloadOperation> m_PendingDownloads = new();

        [SerializeField]
        List<ulong> m_PendingCancellations = new();

        [SerializeField]
        DownloadOperation[] m_PendingResume = Array.Empty<DownloadOperation>();

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        IWebRequestProxy m_WebRequestProxy;

        readonly List<DownloadOperation> m_DownloadInProgress = new();
        readonly Dictionary<ulong, IWebRequestItem> m_WebRequests = new();

        const int k_MaxConcurrentDownloads = 30;

        public event Action<DownloadOperation> DownloadProgress = delegate { };
        public event Action<DownloadOperation> DownloadFinalized = delegate { };

        [ServiceInjection]
        public void Inject(IWebRequestProxy webRequestProxy, IIOProxy ioProxy)
        {
            m_WebRequestProxy = webRequestProxy;
            m_IOProxy = ioProxy;
        }

        public override void OnEnable()
        {
            EditorApplication.update += Update;
        }

        public override void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        public DownloadOperation CreateDownloadOperation(string url, string path)
        {
            return new DownloadOperation
            {
                Id = ++m_LastDownloadOperationId,
                Url = url,
                Path = path
            };
        }

        public void StartDownload(DownloadOperation operation)
        {
            if (m_PendingDownloads.Contains(operation)
                || m_DownloadInProgress.Contains(operation)
                || m_PendingCancellations.Exists(id => id == operation.Id))
            {
                return;
            }

            m_PendingDownloads.Add(operation);
        }

        // We put cancellation and new downloads in `pending` list to be processed in the next update because
        // we don't want to accidentally modify the `m_DownloadInProgress` list when it's being iterated on
        public void Cancel(ulong downloadId)
        {
            m_PendingCancellations.Add(downloadId);
        }

        public void OnBeforeSerialize()
        {
            m_PendingResume = m_DownloadInProgress.ToArray();
        }

        public void OnAfterDeserialize() { }

        void Update()
        {
            HandleResume();
            HandleCancellation();

            var numDownloadsToAdd = Math.Min(k_MaxConcurrentDownloads - m_DownloadInProgress.Count,
                m_PendingDownloads.Count);
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
            {
                UpdateOperation(operation);
            }

            m_DownloadInProgress.RemoveAll(o => o.Status != OperationStatus.InProgress);
        }

        void HandleResume()
        {
            if (m_PendingResume == null || m_PendingResume.Length == 0)
                return;

            foreach (var operation in m_PendingResume)
            {
                var fileSizeInBytes = m_IOProxy.GetFileSizeInBytes(operation.Path);
                if (fileSizeInBytes <= 0 || fileSizeInBytes > operation.TotalBytes)
                {
                    InitializeOperation(operation);
                }
                else if (fileSizeInBytes == operation.TotalBytes)
                {
                    FinalizeOperation(operation, null, OperationStatus.Success);
                }
                else
                {
                    InitializeOperation(operation, true, $"bytes={fileSizeInBytes}-");
                }
            }

            m_PendingResume = null;
        }

        void HandleCancellation()
        {
            if (m_PendingCancellations == null || m_PendingCancellations.Count == 0)
                return;

            foreach (var downloadId in m_PendingCancellations)
            {
                var downloadOperation = m_PendingDownloads.Concat(m_DownloadInProgress)
                    .FirstOrDefault(i => i.Id == downloadId);
                if (downloadOperation == null)
                    continue;

                m_PendingDownloads.RemoveAll(i => i.Id == downloadId);
                m_DownloadInProgress.RemoveAll(i => i.Id == downloadId);
                if (m_WebRequests.TryGetValue(downloadId, out var request) && !request.IsDone)
                {
                    request.Abort();
                }

                FinalizeOperation(downloadOperation, request, OperationStatus.Cancelled);
            }

            m_PendingCancellations.Clear();
        }

        void InitializeOperation(DownloadOperation operation, bool append = false, string bytesRange = null)
        {
            var newRequest = m_WebRequestProxy.SendWebRequest(operation.Url, operation.Path, append, bytesRange);
            m_WebRequests[operation.Id] = newRequest;
            m_DownloadInProgress.Add(operation);
            operation.Start();
        }

        void FinalizeOperation(DownloadOperation operation, IWebRequestItem request, OperationStatus finalStatus,
            string errorMessage = null)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError(
                    $"Encountered error while downloading {Path.GetFileName(operation.Path)}: {errorMessage}");
            }

            if (finalStatus == OperationStatus.Success)
            {
                operation.SetProgress(1f);
            }
            
            operation.Finish(finalStatus);
            operation.Error = errorMessage ?? string.Empty;
            m_WebRequests.Remove(operation.Id);
            request?.Dispose();
            DownloadFinalized.Invoke(operation);
        }

        void UpdateOperation(DownloadOperation operation)
        {
            if (!m_WebRequests.TryGetValue(operation.Id, out var request))
                return;

            if (!string.IsNullOrEmpty(request.Error))
            {
                FinalizeOperation(operation, request, OperationStatus.Error, request.Error);
                return;
            }

            switch (request.Result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    FinalizeOperation(operation, request, OperationStatus.Error,
                        "Failed to communicate with the server.");
                    return;
                case UnityWebRequest.Result.ProtocolError:
                    FinalizeOperation(operation, request, OperationStatus.Error,
                        "The server returned an error response.");
                    return;
                case UnityWebRequest.Result.DataProcessingError:
                    FinalizeOperation(operation, request, OperationStatus.Error, "Error processing data.");
                    return;
                case UnityWebRequest.Result.InProgress:
                case UnityWebRequest.Result.Success:
                default:
                    break;
            }

            if (request.IsDone)
            {
                FinalizeOperation(operation, request, OperationStatus.Success);
                return;
            }

            var progressUpdate = request.DownloadProgress - operation.Progress;

            // We are reducing how often we are reporting download progress to avoid expensive frequent UI refreshes.
            if (progressUpdate >= 0.05 || progressUpdate * operation.TotalBytes > 1024 * 1024)
            {
                operation.SetProgress(request.DownloadProgress);
                DownloadProgress?.Invoke(operation);
            }
        }
    }
}
