using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    enum OperationStatus
    {
        None,
        InProgress,
        Success,
        Cancelled,
        Error,
        Paused
    }

    [Serializable]
    abstract class AssetDataOperation : BaseOperation
    {
        public abstract AssetIdentifier Identifier { get; }
    }

    abstract class BaseOperation
    {
        int m_ProgressId;

        public event Action<OperationStatus> Finished;

        public event Action<float> ProgressChanged;

        public abstract float Progress { get; }

        public abstract string OperationName { get; }

        public abstract string Description { get; }

        public virtual bool StartIndefinite => false;

        public virtual bool IsSticky => false;

        public OperationStatus Status { get; private set; } = OperationStatus.None;

        public void Start()
        {
            Status = OperationStatus.InProgress;
            var options = StartIndefinite ? UnityEditor.Progress.Options.Indefinite : UnityEditor.Progress.Options.None;

            m_ProgressId = UnityEditor.Progress.Start(OperationName, Description, options);
            ProgressChanged?.Invoke(0.0f);
        }

        protected void Report()
        {
            var progress = Progress;

            if (StartIndefinite && progress > 0.0f &&
                (UnityEditor.Progress.GetOptions(m_ProgressId) & UnityEditor.Progress.Options.Indefinite) != 0)
            {
                UnityEditor.Progress.Remove(m_ProgressId);
                m_ProgressId = UnityEditor.Progress.Start(OperationName, Description,
                    IsSticky ? UnityEditor.Progress.Options.Sticky : UnityEditor.Progress.Options.None);
            }

            UnityEditor.Progress.Report(m_ProgressId, progress, Description);

            ProgressChanged?.Invoke(progress);
        }

        public void Finish(OperationStatus status)
        {
            Status = status;
            UnityEditor.Progress.Finish(m_ProgressId, FromOperationStatus(status));

            Finished?.Invoke(status);
        }

        public void Pause()
        {
            Status = OperationStatus.Paused;
            ProgressChanged?.Invoke(0.0f);
        }

        public void Resume()
        {
            Status = OperationStatus.InProgress;
            Report();
        }

        static Progress.Status FromOperationStatus(OperationStatus status)
        {
            return status switch
            {
                OperationStatus.InProgress => UnityEditor.Progress.Status.Running,
                OperationStatus.Success => UnityEditor.Progress.Status.Succeeded,
                OperationStatus.Cancelled => UnityEditor.Progress.Status.Canceled,
                OperationStatus.Error => UnityEditor.Progress.Status.Failed,
                OperationStatus.None => UnityEditor.Progress.Status.Succeeded,
                OperationStatus.Paused => UnityEditor.Progress.Status.Paused,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }
    }
}
