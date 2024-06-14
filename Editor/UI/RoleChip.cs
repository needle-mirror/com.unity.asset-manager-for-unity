using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    public class RoleChip : VisualElement
    {
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IPermissionsManager m_PermissionsManager;

        Label m_Label;

        static readonly string k_UssClassName = "unity-role-chip";
        static readonly string k_DocumentationUrl = "https://docs.unity.com/cloud/en-us/asset-manager/org-project-roles";

        internal RoleChip(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            IPermissionsManager permissionsManager)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PermissionsManager = permissionsManager;

            AddToClassList(k_UssClassName);

            m_Label = new Label();
            m_Label.pickingMode = PickingMode.Ignore;
            m_Label.text = m_PermissionsManager.Role.ToString();
            Add(m_Label);

            UpdateVisibility();

            RegisterCallback<ClickEvent>(OnClicked);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnClicked(ClickEvent evt)
        {
            Application.OpenURL(k_DocumentationUrl);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
        }

        async void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo _)
        {
            if (projectInfo != null)
            {
                await SetProjectRole(projectInfo.Id);
            }
        }

        void OnActivePageChanged(IPage page)
        {
            UpdateVisibility();
        }

        async Task SetProjectRole(string projectId)
        {
            UIElementsUtils.SetDisplay(this, false);
            if (string.IsNullOrEmpty(projectId))
                return;

            var role = await m_PermissionsManager.FetchRoleAsync(projectId);
            m_Label.text = role.ToString();
            UpdateVisibility();
        }

        void UpdateVisibility()
        {
            var isVisible = m_PageManager.ActivePage is CollectionPage or UploadPage;
            UIElementsUtils.SetDisplay(this, isVisible && m_PermissionsManager.Role != Role.None);
        }
    }
}
