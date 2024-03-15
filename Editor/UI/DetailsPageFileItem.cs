using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    internal class DetailsPageFileItem : VisualElement
    {
        private const string k_DetailsPageFileItemUssStyle = "details-page-file-item";
        private const string k_DetailsPageFileIconItemUssStyle = "details-page-file-item-icon";
        private const string k_DetailsPageFileLabelItemUssStyle = "details-page-file-item-label";
        private const string k_DetailsPageThreeDotsItemUssStyle = "details-page-three-dots-item";
        private const string k_DetailsPageInProjectItemUssStyle = "details-page-in-project-item";
        private readonly string k_ShowInProjectText = L10n.Tr("Show in project");

        private readonly VisualElement m_Icon;
        private readonly Label m_FileName;
        private readonly Button m_ThreeDots;

        private readonly VisualElement m_InProjectIcon;
        private GenericMenu m_ThreeDotsMenu;

        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IPageManager m_PageManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        public DetailsPageFileItem(IAssetDataManager assetDataManager, IPageManager pageManager, IAssetImporter assetImporter, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetDataManager = assetDataManager;
            m_PageManager = pageManager;
            m_AssetImporter = assetImporter;
            m_AssetDatabaseProxy = assetDatabaseProxy;

            m_FileName = new Label("");
            m_Icon = new VisualElement();
            m_ThreeDots = new Button();
            m_InProjectIcon = new VisualElement();
            m_ThreeDots.ClearClassList();

            AddToClassList(k_DetailsPageFileItemUssStyle);
            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);
            m_ThreeDots.AddToClassList(k_DetailsPageThreeDotsItemUssStyle);
            m_InProjectIcon.AddToClassList(k_DetailsPageInProjectItemUssStyle);
            m_InProjectIcon.tooltip = L10n.Tr("Imported");

            m_ThreeDotsMenu = new GenericMenu();
            m_ThreeDots.clicked += ShowAsContext;

            Add(m_Icon);
            Add(m_FileName);
            Add(m_InProjectIcon);
            Add(m_ThreeDots);
        }

        public void Refresh(string fileName, ICollection<string> allFiles)
        {
            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForFile(fileName);
            m_FileName.text = fileName;
            m_InProjectIcon.visible = IsShowInProjectEnabled();
            m_ThreeDots.visible = !MetafilesHelper.IsMetafile(fileName);

            SetEnabled(!MetafilesHelper.IsOrphanMetafile(fileName, allFiles));
        }

        void ShowAsContext()
        {
            m_ThreeDotsMenu = new GenericMenu();
            if (IsShowInProjectEnabled())
                m_ThreeDotsMenu.AddItem(new GUIContent(k_ShowInProjectText), false, ShowInProjectBrowser);
            else
                m_ThreeDotsMenu.AddDisabledItem(new GUIContent(k_ShowInProjectText));

            m_ThreeDotsMenu.ShowAsContext();
        }

        bool IsShowInProjectEnabled()
        {
            var assetObject = GetAssetObject();
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            return assetObject != null && !m_AssetImporter.IsImporting(assetData.identifier);
        }

        void ShowInProjectBrowser()
        {
            var assetObject = GetAssetObject();
            if (assetObject == null)
                return;
            EditorGUIUtility.PingObject(assetObject);
        }

        Object GetAssetObject()
        {
            var selectedAsset = m_PageManager.activePage.selectedAssetId;
            if (!string.IsNullOrEmpty(selectedAsset.organizationId))
            {
                var assetData = m_AssetDataManager.GetAssetData(selectedAsset);

                if (assetData == null)
                    return null;

                var importedInfo = m_AssetDataManager.GetImportedAssetInfo(assetData.identifier);
                var importedFileInfo = importedInfo?.fileInfos?.Find(f => CompareAssetFileName(f.originalPath, MetafilesHelper.RemoveMetaExtension(m_FileName.text)));
                if (importedFileInfo != null)
                {
                    return m_AssetDatabaseProxy.LoadAssetAtPath(m_AssetDatabaseProxy.GuidToAssetPath(importedFileInfo.guid));
                }

                return null;
            }

            return m_AssetDatabaseProxy.LoadAssetAtPath(m_AssetDatabaseProxy.GuidToAssetPath(selectedAsset.assetId)); // TODO fix this once the AssetId will not be populated with GUID in case of Uploading
        }

        static bool CompareAssetFileName(string f1, string f2)
        {
            return f1.Replace("/", "\\").Equals(f2.Replace("/", "\\"), StringComparison.OrdinalIgnoreCase);
        }
    }
}
