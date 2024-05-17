using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    abstract class AssetContextMenu
    {
        internal readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        internal readonly IAssetDataManager m_AssetDataManager;
        internal readonly IAssetImporter m_AssetImporter;
        internal readonly ILinksProxy m_LinksProxy;
        internal readonly IPageManager m_PageManager;
        internal readonly IUnityConnectProxy m_UnityConnectProxy;

        IAssetData m_TargetAssetData;

        public IAssetData TargetAssetData
        {
            get => m_TargetAssetData;
            set => m_TargetAssetData = value;
        }

        protected AssetContextMenu(IUnityConnectProxy unityConnectProxy, IAssetDataManager assetDataManager, IAssetImporter assetImporter,
            ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy, IPageManager pageManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_LinksProxy = linksProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_PageManager = pageManager;
        }

        public abstract void SetupContextMenuEntries(ContextualMenuPopulateEvent evt);

        protected static void AddMenuEntry(ContextualMenuPopulateEvent evt, string actionName, bool enabled,
            Action<DropdownMenuAction> action)

        {
            evt.menu.InsertAction(0, actionName, action,
                enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }
    }
}
