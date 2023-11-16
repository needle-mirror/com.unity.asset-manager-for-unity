using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

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

        private VisualElement m_Icon;
        private Label m_FileName;
        private Button m_ThreeDots;

        private VisualElement InProjectIcon;
        private GenericMenu m_ThreeDotsMenu;
        
        private IAssetDataManager m_AssetDataManager;
        private IPageManager m_PageManager;
        private IAssetImporter m_AssetImporter;
        public DetailsPageFileItem(IAssetDataManager assetDataManager, IPageManager pageManager, IAssetImporter assetImporter)
        {
            m_AssetDataManager = assetDataManager;
            m_PageManager = pageManager;
            m_AssetImporter = assetImporter;
            
            m_FileName = new Label("");
            m_Icon = new VisualElement();
            m_ThreeDots = new Button();
            InProjectIcon = new VisualElement();
            m_ThreeDots.ClearClassList();

            AddToClassList(k_DetailsPageFileItemUssStyle);
            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);
            m_ThreeDots.AddToClassList(k_DetailsPageThreeDotsItemUssStyle);
            InProjectIcon.AddToClassList(k_DetailsPageInProjectItemUssStyle);
            InProjectIcon.tooltip = L10n.Tr("Imported");
            
            m_ThreeDotsMenu = new GenericMenu();
            m_ThreeDots.clicked += () => { ShowAsContext(); };

            Add(m_Icon);
            Add(m_FileName);
            Add(InProjectIcon);
            Add(m_ThreeDots);
        }

        public void Refresh(string fileName)
        {
            m_Icon.style.backgroundImage = InternalEditorUtility.GetIconForFile(fileName);
            m_FileName.text = fileName;
            InProjectIcon.visible = IsShowInProjectEnabled();
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
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            var importedInfo = m_AssetDataManager.GetImportedAssetInfo(assetData.id);
            var importedFileInfo = importedInfo?.fileInfos?.FirstOrDefault(f => f.originalPath.Equals(m_FileName.text));
            if (importedFileInfo != null)
            {
                var assetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(importedFileInfo.guid));
                return assetObject != null && !m_AssetImporter.IsImporting(assetData.id);    
            }

            return false;
        }

        void ShowInProjectBrowser()
        {
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            var importedInfo = m_AssetDataManager.GetImportedAssetInfo(assetData.id);
            var importedFileInfo = importedInfo?.fileInfos?.FirstOrDefault(f => f.originalPath.Equals(m_FileName.text));
            if (importedFileInfo != null)
            {
                var assetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(importedFileInfo.guid));
                ProjectWindowUtil.ShowCreatedAsset(assetObject);
            }
        }
    }
}
