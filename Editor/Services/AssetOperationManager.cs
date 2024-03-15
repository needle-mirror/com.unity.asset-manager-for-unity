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

        void RegisterOperation(AssetDataOperation operation);
    }


    [Serializable]
    class AssetOperationManager : BaseService<IAssetOperationManager>, IAssetOperationManager
    {
        public event Action<AssetDataOperation> OperationProgressChanged;
        public event Action<AssetDataOperation> OperationFinished;

        readonly Dictionary<AssetIdentifier, AssetDataOperation> m_Operations = new ();

        [SerializeReference]
        IPageManager m_PageManager;

        [ServiceInjection]
        public void Inject(IPageManager pageManager)
        {
            m_PageManager = pageManager;
        }

        public override void OnEnable()
        {
            m_PageManager.onActivePageChanged += OnActivePageChanged;
        }

        public override void OnDisable()
        {
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
        }

        void OnActivePageChanged(IPage _)
        {
            ClearFinishedOperations();
        }

        public AssetDataOperation GetAssetOperation(AssetIdentifier identifier)
        {
            return m_Operations.TryGetValue(identifier, out var result) ? result : null;
        }

        public void ClearFinishedOperations()
        {
            foreach (var operation in m_Operations.Values.ToArray())
            {
                if (operation.Status != OperationStatus.InProgress)
                {
                    m_Operations.Remove(operation.AssetId);
                }
            }
        }

        public void RegisterOperation(AssetDataOperation operation)
        {
            if (m_Operations.TryGetValue(operation.AssetId, out var existingOperation) && operation == existingOperation)
                // The operation is already registered
                return;

            if (existingOperation is { IsSticky: false })
            {
                Debug.LogWarning("An operation for this asset were already existing");
            }

            operation.ProgressChanged += _ => OperationProgressChanged?.Invoke(operation);
            operation.Finished += _ =>
            {
                if (!operation.IsSticky)
                {
                    m_Operations.Remove(operation.AssetId);
                }

                OperationFinished?.Invoke(operation);
            };

            m_Operations[operation.AssetId] = operation;
        }
    }
}