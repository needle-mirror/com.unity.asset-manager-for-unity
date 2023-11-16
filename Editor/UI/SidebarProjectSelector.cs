using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SidebarProjectSelector : VisualElement
    {
        private DropdownField m_ProjectsDropdown;
        private VisualElement m_NoProjectsFoundContainer;

        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public SidebarProjectSelector(IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            InitializeLayout();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void InitializeLayout()
        {
            m_ProjectsDropdown = new DropdownField();
            m_ProjectsDropdown.AddToClassList("ProjectsDropdown");
            m_ProjectsDropdown.RegisterValueChangedCallback(evt =>
            {
                m_ProjectOrganizationProvider.selectedProject = m_ProjectOrganizationProvider.organization?.projectInfos.FirstOrDefault(proj => proj.name == evt.newValue);
            });

            var warningIcon = new VisualElement();
            warningIcon.AddToClassList("warning-icon");
            m_NoProjectsFoundContainer = new VisualElement();
            m_NoProjectsFoundContainer.AddToClassList("NoProjectsWarning");
            m_NoProjectsFoundContainer.Add(warningIcon);
            m_NoProjectsFoundContainer.Add(new Label { text = L10n.Tr("No project found") });

            Add(m_ProjectsDropdown);
            Add(m_NoProjectsFoundContainer);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Refresh();
            m_ProjectOrganizationProvider.onProjectSelectionChanged += OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ProjectOrganizationProvider.onProjectSelectionChanged -= OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;
        }

        private void OnProjectSelectionChanged(ProjectInfo _)
        {
            Refresh();
        }

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
        {
            Refresh();
        }

        private void Refresh()
        {
            var projectInfos = m_ProjectOrganizationProvider.organization?.projectInfos as IList<ProjectInfo> ?? Array.Empty<ProjectInfo>();
            if (projectInfos.Count == 0)
            {
                UIElementsUtils.Hide(m_ProjectsDropdown);
                UIElementsUtils.Show(m_NoProjectsFoundContainer);
                return;
            }
            UIElementsUtils.Show(m_ProjectsDropdown);
            UIElementsUtils.Hide(m_NoProjectsFoundContainer);

            if (m_ProjectOrganizationProvider.selectedProject == null)
            {
                m_ProjectOrganizationProvider.selectedProject = projectInfos.FirstOrDefault();
                return;
            }

            var projectName = m_ProjectOrganizationProvider.selectedProject.name;
            m_ProjectsDropdown.choices = projectInfos.Select(i => i.name).ToList();
            m_ProjectsDropdown.value = projectName;
            m_ProjectsDropdown.tooltip = projectName;
        }
    }
}