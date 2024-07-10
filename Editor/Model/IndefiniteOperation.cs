using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class IndefiniteOperation : AssetDataOperation
    {
        public override float Progress => 0.0f;
        public override string OperationName => "Processing";
        public override string Description => "Processing";
        public override AssetIdentifier Identifier => m_AssetData.Identifier;
        public override bool StartIndefinite => true;

        [SerializeReference]
        IAssetData m_AssetData;

        public IndefiniteOperation(IAssetData assetData)
        {
            m_AssetData = assetData;
        }
    }
}