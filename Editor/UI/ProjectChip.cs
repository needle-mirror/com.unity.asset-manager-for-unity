using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class ProjectChip : VisualElement
    {
        Image m_Icon;
        ProjectInfo m_ProjectInfo;
        VisualTreeAsset m_TreeAsset;

        internal event Action<ProjectInfo> ProjectChipClickAction;

        public ProjectChip(ProjectInfo projectInfo)
        {
            m_ProjectInfo = projectInfo;

            m_Icon = new Image();
            m_Icon.style.backgroundColor = ProjectIconDownloader.DefaultColor;
            m_Icon.pickingMode = PickingMode.Ignore;
            hierarchy.Add(m_Icon);

            var textLabel = new Label(projectInfo.Name);
            textLabel.pickingMode = PickingMode.Ignore;
            hierarchy.Add(textLabel);

            tooltip = projectInfo.Name;

            RegisterCallback<ClickEvent>(OnClick);
        }

        public void SetIcon(Texture texture)
        {
            if (texture != null)
            {
                m_Icon.image = texture;
            }
            else
            {
                m_Icon.AddToClassList("icon-default-project");
                m_Icon.style.backgroundColor = ProjectIconDownloader.GetProjectIconColor(m_ProjectInfo.Id);
            }
        }

        void OnClick(ClickEvent evt)
        {
            ProjectChipClickAction?.Invoke(m_ProjectInfo);
        }
    }
}
