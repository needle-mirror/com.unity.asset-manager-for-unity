using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class DetailsPageFileItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-file-item";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-file-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-file-item-label";
        const string k_DetailsPageThreeDotsItemUssStyle = "details-page-three-dots-item";
        const string k_DetailsPageInProjectItemUssStyle = "details-page-in-project-item";
        const string k_IncompleteFileIcon = "incomplete-file-icon";

        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        readonly Label m_FileName;
        readonly VisualElement m_Icon;
        readonly VisualElement m_ErrorIcon;
        readonly Button m_ThreeDots;
        readonly VisualElement m_InProjectIcon;

        string m_Guid;
        GenericMenu m_ThreeDotsMenu;

        public DetailsPageFileItem(IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetDatabaseProxy = assetDatabaseProxy;

            m_FileName = new Label("");
            m_Icon = new VisualElement();
            m_ErrorIcon = new VisualElement();
            m_ThreeDots = new Button();
            m_InProjectIcon = new VisualElement();
            m_ThreeDots.ClearClassList();

            AddToClassList(k_DetailsPageFileItemUssStyle);
            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);

            m_ErrorIcon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_ErrorIcon.AddToClassList(k_IncompleteFileIcon);
            m_ErrorIcon.tooltip = L10n.Tr("This file is currently uploading or it failed to upload.");

            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);
            m_ThreeDots.AddToClassList(k_DetailsPageThreeDotsItemUssStyle);
            m_InProjectIcon.AddToClassList(k_DetailsPageInProjectItemUssStyle);
            m_InProjectIcon.tooltip = L10n.Tr("Imported");

            m_ThreeDotsMenu = new GenericMenu();
            m_ThreeDots.clicked += ShowAsContext;

            Add(m_Icon);
            Add(m_ErrorIcon);
            Add(m_FileName);
            Add(m_InProjectIcon);
            Add(m_ThreeDots);
        }

        public void Refresh(string fileName, string guid, bool enabled, bool uploaded)
        {
            var extension = string.IsNullOrEmpty(fileName) ? null : Path.GetExtension(fileName);

            if (uploaded)
            {
                UIElementsUtils.Hide(m_ErrorIcon);
                UIElementsUtils.Show(m_Icon);

                m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForExtension(extension);
                m_Icon.tooltip = extension;
            }
            else
            {
                UIElementsUtils.Hide(m_Icon);
                UIElementsUtils.Show(m_ErrorIcon);
            }

            m_FileName.text = fileName;
            m_Guid = guid;

            m_InProjectIcon.visible = IsShowInProjectEnabled();
            m_ThreeDots.visible = !MetafilesHelper.IsMetafile(fileName);

            SetEnabled(enabled);
        }

        void ShowAsContext()
        {
            m_ThreeDotsMenu = new GenericMenu();
            var guiContent = new GUIContent(Constants.ShowInProjectActionText);

            if (IsShowInProjectEnabled())
            {
                m_ThreeDotsMenu.AddItem(guiContent, false, ShowInProjectBrowser);
            }
            else
            {
                m_ThreeDotsMenu.AddDisabledItem(guiContent);
            }

            m_ThreeDotsMenu.ShowAsContext();
        }

        bool IsShowInProjectEnabled()
        {
            return m_AssetDatabaseProxy.CanPingAssetByGuid(m_Guid);
        }

        void ShowInProjectBrowser()
        {
            m_AssetDatabaseProxy.PingAssetByGuid(m_Guid);
        }
    }
}
