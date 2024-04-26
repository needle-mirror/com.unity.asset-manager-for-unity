using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class UploadContextMenu : AssetContextMenu
    {
        public UploadContextMenu(IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy, IPageManager pageManager)
            : base(assetDataManager, assetImporter, linksProxy, assetDatabaseProxy, pageManager) { }

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            IgnoreAssetEntry(evt);
            ShowInProjectEntry(evt);
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, L10n.Tr(Constants.ShowInProjectActionText), true,
                (_) =>
                {
                    var uploadAssetData = (UploadAssetData)TargetAssetData;
                    EditorGUIUtility.PingObject(m_AssetDatabaseProxy.LoadAssetAtPath(uploadAssetData.AssetPath));
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInProject));
                });
        }

        void IgnoreAssetEntry(ContextualMenuPopulateEvent evt)
        {
            var uploadAssetData = (UploadAssetData)TargetAssetData;
            AddMenuEntry(evt, !uploadAssetData.IsIgnored ? L10n.Tr(Constants.IgnoreAsset) : L10n.Tr(Constants.IncludeAsset), true,
                (_) =>
                {
                    ServicesContainer.instance.Resolve<IPageManager>().ActivePage.ToggleAsset(uploadAssetData, uploadAssetData.IsIgnored);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.IgnoreUploadedAsset));
                });
        }
    }
}
