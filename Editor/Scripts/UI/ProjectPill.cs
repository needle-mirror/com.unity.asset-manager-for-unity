using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class ProjectPill : VisualElement
    {
        const string k_DarkPillClass = "pill-dark";
        const string k_LightPillClass = "pill-light";

        internal Action<ProjectInfo> ProjectPillClickAction;

        ProjectInfo m_ProjectInfo;
        VisualTreeAsset m_TreeAsset;
        ClickOrDragStartManipulator m_Manipulator;

        Image m_Icon;

        public ProjectPill(ProjectInfo projectInfo)
        {
            m_ProjectInfo = projectInfo;
            m_Manipulator = new ClickOrDragStartManipulator(this, OnClick, null);
            
            m_Icon = new Image();
            m_Icon.style.backgroundColor = ProjectIconDownloader.DefaultColor;
            m_Icon.pickingMode = PickingMode.Ignore;
            hierarchy.Add(m_Icon);

            var textLabel = new Label(projectInfo.name);
            textLabel.pickingMode = PickingMode.Ignore;
            hierarchy.Add(textLabel);

            AddToClassList(EditorGUIUtility.isProSkin ? k_DarkPillClass : k_LightPillClass);
        }

        public void SetIcon(Texture texture)
        {
            if (texture != null)
            {
                m_Icon.image = texture;
            }
            else
            {
                m_Icon.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ProjectIconName]);
                m_Icon.style.backgroundColor = ProjectIconDownloader.GetProjectIconColor(m_ProjectInfo.id);
            }
        }

        void OnClick()
        {
            ProjectPillClickAction?.Invoke(m_ProjectInfo);
        }
    }
}
