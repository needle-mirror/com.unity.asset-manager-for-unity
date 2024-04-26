using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class LocalAssetContextMenu : AssetContextMenu
    {
        public LocalAssetContextMenu(IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy, IPageManager pageManager) : base(assetDataManager, assetImporter, linksProxy, assetDatabaseProxy, pageManager)
        {
        }

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ShowInProjectEntry(evt);
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            var localAssetIdentifier = TargetAssetData.Identifier as LocalAssetIdentifier;

            AddMenuEntry(evt, Constants.ShowInProjectActionText, localAssetIdentifier != null,
                (_) =>
                {
                    if (localAssetIdentifier != null && localAssetIdentifier.IsIdValid())
                    {
                        EditorGUIUtility.PingObject(m_AssetDatabaseProxy.LoadAssetAtPath(m_AssetDatabaseProxy.GuidToAssetPath(localAssetIdentifier.Guid)));
                    }

                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInProject));
                });
        }
    }
}
