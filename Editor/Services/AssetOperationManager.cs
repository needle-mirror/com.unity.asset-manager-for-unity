using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IAssetOperationManager : IService
    {
        event Action<AssetDataOperation> OperationProgressChanged;
        event Action<AssetDataOperation> OperationFinished;

        AssetDataOperation GetAssetOperation(AssetIdentifier identifier);
        void PauseAllOperations();
        void ResumeAllOperations();
        void RegisterOperation(AssetDataOperation operation);
        void ClearFinishedOperations();
    }

    [Serializable]
    class AssetOperationManager : BaseService<IAssetOperationManager>, IAssetOperationManager
    {
        readonly Dictionary<TrackedAssetIdentifier, AssetDataOperation> m_Operations = new();

        [SerializeReference]
        IPageManager m_PageManager;

        public event Action<AssetDataOperation> OperationProgressChanged;
        public event Action<AssetDataOperation> OperationFinished;

        [ServiceInjection]
        public void Inject(IPageManager pageManager)
        {
            m_PageManager = pageManager;
        }

        public override void OnEnable()
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
        }

        public override void OnDisable()
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
        }

        public AssetDataOperation GetAssetOperation(AssetIdentifier identifier)
        {
            return m_Operations.GetValueOrDefault(new TrackedAssetIdentifier(identifier));
        }

        public void PauseAllOperations()
        {
            foreach (var assetOperation in m_Operations.Values)
            {
                assetOperation.Pause();
            }
        }

        public void ResumeAllOperations()
        {
            foreach (var assetOperation in m_Operations.Values)
            {
                assetOperation.Resume();
            }
        }

        public void RegisterOperation(AssetDataOperation operation)
        {
            var identifier = new TrackedAssetIdentifier(operation.Identifier);

            if (m_Operations.TryGetValue(identifier, out var existingOperation) &&
                operation == existingOperation)

                // The operation is already registered
            {
                return;
            }

            if (existingOperation is { IsSticky: false })
            {
                Debug.LogWarning("An operation for this asset were already existing");
            }

            operation.ProgressChanged += _ => OperationProgressChanged?.Invoke(operation);
            operation.Finished += _ =>
            {
                if (!operation.IsSticky)
                {
                    m_Operations.Remove(identifier);
                }

                OperationFinished?.Invoke(operation);
            };

            m_Operations[identifier] = operation;
        }

        void OnActivePageChanged(IPage _)
        {
            ClearFinishedOperations();
        }

        public void ClearFinishedOperations()
        {
            foreach (var operation in m_Operations.Values.ToArray())
            {
                if (operation.Status != OperationStatus.InProgress)
                {
                    m_Operations.Remove(new TrackedAssetIdentifier(operation.Identifier));
                }
            }
        }
    }
}
