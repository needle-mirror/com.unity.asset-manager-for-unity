using System;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    enum OperationStatus
    {
        None,
        InProgress,
        Success,
        Cancelled,
        Error
    }

    abstract class BaseOperation
    {
        int m_ProgressId;

        public abstract float Progress { get; }

        protected abstract string OperationName { get; }

        protected abstract string Description { get; }

        protected virtual bool StartIndefinite { get; } = false;

        protected virtual bool IsSticky { get; } = false;

        public OperationStatus Status { get; private set; } = OperationStatus.None;

        readonly BaseOperation m_Parent;

        protected BaseOperation(BaseOperation parent)
        {
            m_Parent = parent;
        }

        public void Start()
        {
            Status = OperationStatus.InProgress;
            var options = StartIndefinite ? UnityEditor.Progress.Options.Indefinite : UnityEditor.Progress.Options.None;

            if (!StartIndefinite && IsSticky)
            {
                options |= UnityEditor.Progress.Options.Sticky;
            }

            m_ProgressId = UnityEditor.Progress.Start(OperationName, Description, options, m_Parent?.m_ProgressId ?? -1);
        }

        public void Report()
        {
            if (m_ProgressId == 0)
            {
                Start();
            }

            var progress = Progress;

            if (StartIndefinite && progress > 0.0f && (UnityEditor.Progress.GetOptions(m_ProgressId) & UnityEditor.Progress.Options.Indefinite) != 0)
            {
                UnityEditor.Progress.Remove(m_ProgressId);
                m_ProgressId = UnityEditor.Progress.Start(OperationName, Description, IsSticky ? UnityEditor.Progress.Options.Sticky : UnityEditor.Progress.Options.None, m_Parent?.m_ProgressId ?? -1);
            }

            UnityEditor.Progress.Report(m_ProgressId, progress, Description);
        }

        public void Finish(OperationStatus status)
        {
            Status = status;
            UnityEditor.Progress.Finish(m_ProgressId, FromOperationStatus(status));
            m_ProgressId = 0;
        }

        static Progress.Status FromOperationStatus(OperationStatus status)
        {
            return status switch
            {
                OperationStatus.InProgress => UnityEditor.Progress.Status.Running,
                OperationStatus.Success => UnityEditor.Progress.Status.Succeeded,
                OperationStatus.Cancelled => UnityEditor.Progress.Status.Canceled,
                OperationStatus.Error => UnityEditor.Progress.Status.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }
    }
}