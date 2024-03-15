using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class LocalAssetContextMenu : AssetContextMenu
    {
        public LocalAssetContextMenu(IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy) : base(assetDataManager, assetImporter, linksProxy, assetDatabaseProxy)
        {
        }
        
        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ShowInProjectEntry(evt);
        }
        
        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, Constants.ShowInProjectActionText, true,
                (_) =>
                {
                    EditorGUIUtility.PingObject(m_AssetDatabaseProxy.LoadAssetAtPath(m_AssetDatabaseProxy.GuidToAssetPath(TargetAssetData.identifier.assetId)));
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInProject));
                });
        }
    }
}