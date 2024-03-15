using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IContextMenuBuilder : IService
    {
        void RegisterContextMenu(Type assetDataType, Type typeContextMenu);
        bool IsContextMenuRegistered(Type assetDataType);
        object BuildContextMenu(Type assetDataType);
        public bool IsContextMenuMatchingAssetDataType(Type assetDataType, Type typeContextMenu);
    }
    
    [Serializable]
    internal class ContextMenuBuilder : BaseService<IContextMenuBuilder>, IContextMenuBuilder
    {
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;
        [SerializeReference]
        IAssetImporter m_AssetImporter;
        [SerializeReference]
        ILinksProxy m_LinksProxy;
        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;

        private readonly Dictionary<Type, Type> m_AssetDataTypeToContextMenuType = new();

        [ServiceInjection]
        public void Inject(IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_LinksProxy = linksProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
        }

        public object BuildContextMenu(Type assetDataType)
        {
            if (!m_AssetDataTypeToContextMenuType.TryGetValue(assetDataType, out var contextType))
            {
                Debug.LogError("No context menu registered for asset data type: " + assetDataType);
                return null;
            }
            
            return Activator.CreateInstance(contextType,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance,
                null, new object[] {m_AssetDataManager, m_AssetImporter, m_LinksProxy, m_AssetDatabaseProxy},
                null);
        }

        public void RegisterContextMenu(Type assetDataType, Type typeContextMenu)
        {
            m_AssetDataTypeToContextMenuType.TryAdd(assetDataType, typeContextMenu);
        }
        
        public bool IsContextMenuRegistered(Type assetDataType)
        {
            return m_AssetDataTypeToContextMenuType.ContainsKey(assetDataType);
        }

        public bool IsContextMenuMatchingAssetDataType(Type assetDataType, Type typeContextMenu)
        {
            if (m_AssetDataTypeToContextMenuType.TryGetValue(assetDataType, out var value))
            {
                return value == typeContextMenu;
            }

            return false;
        }
    }
}