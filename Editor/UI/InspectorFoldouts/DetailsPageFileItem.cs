using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class DetailsPageFileItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-file-item";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-file-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-file-item-label";
        const string k_DetailsPageThreeDotsItemUssStyle = "details-page-three-dots-item";
        const string k_DetailsPageInProjectItemUssStyle = "details-page-in-project-item";

        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        readonly Label m_FileName;
        readonly VisualElement m_Icon;
        readonly Button m_ThreeDots;
        readonly VisualElement m_InProjectIcon;
        readonly IPageManager m_PageManager;

        string m_Guid;
        GenericMenu m_ThreeDotsMenu;

        public DetailsPageFileItem(IAssetDatabaseProxy assetDatabaseProxy)
        {
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

        public void Refresh(string fileName, string guid, bool enabled)
        {
            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForFile(fileName);
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
            return GetAssetObject(m_Guid) != null;
        }

        void ShowInProjectBrowser()
        {
            var assetObject = GetAssetObject(m_Guid);

            if (assetObject != null)
            {
                EditorGUIUtility.PingObject(assetObject);
            }
        }

        Object GetAssetObject(string guid)
        {
            return m_AssetDatabaseProxy.LoadAssetAtPath(m_AssetDatabaseProxy.GuidToAssetPath(guid));
        }
    }
}
