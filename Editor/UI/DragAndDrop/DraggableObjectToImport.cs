using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class DraggableObjectToImport : ScriptableObject
    {
        [SerializeField]
        AssetIdentifier m_AssetIdentifier;

        public AssetIdentifier AssetIdentifier
        {
            get => m_AssetIdentifier;
            set => m_AssetIdentifier = value;
        }
    }
}