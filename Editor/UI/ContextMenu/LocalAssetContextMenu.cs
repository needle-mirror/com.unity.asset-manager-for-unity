using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class LocalAssetContextMenu : AssetContextMenu
    {
        public LocalAssetContextMenu(IUnityConnectProxy unityConnectProxy, IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy, IPageManager pageManager) : base(unityConnectProxy, assetDataManager, assetImporter, linksProxy, assetDatabaseProxy, pageManager)
        {
        }

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ShowInProjectEntry(evt);
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, Constants.ShowInProjectActionText, TargetAssetData != null,
                (_) =>
                {
                    if (TargetAssetData is { PrimarySourceFile: not null } && !string.IsNullOrEmpty(TargetAssetData.PrimarySourceFile.Guid))
                    {
                        m_AssetDatabaseProxy.PingAssetByGuid(TargetAssetData.PrimarySourceFile.Guid);
                    }

                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInProject));
                });
        }
    }
}
