using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class ActionHelpBox : HelpBox
    {
        private ErrorOrMessageActionButton m_ErrorOrMessageActionButton;

        private readonly IPageManager m_PageManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public ActionHelpBox(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider, ILinksProxy linksProxy)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_ErrorOrMessageActionButton = new ErrorOrMessageActionButton(pageManager, linksProxy);

            messageType = HelpBoxMessageType.Info;
            Add(m_ErrorOrMessageActionButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        // Returns true if help box is visible, otherwise returns false
        public bool Refresh()
        {
            var notInProject = m_PageManager.activePage is not { pageType: PageType.InProject };
            var errorHandlingData = string.IsNullOrEmpty(m_ProjectOrganizationProvider.errorOrMessageHandlingData?.message) && notInProject
                ? m_PageManager.activePage?.errorOrMessageHandlingData
                : m_ProjectOrganizationProvider.errorOrMessageHandlingData;

            if (string.IsNullOrWhiteSpace(errorHandlingData?.message) || notInProject)
            {
                UIElementsUtils.Hide(this);
                return false;
            }
            UIElementsUtils.Show(this);

            text = errorHandlingData.message;
            m_ErrorOrMessageActionButton.SetErrorSuggestion(errorHandlingData.errorOrMessageRecommendedAction, string.IsNullOrEmpty(m_ProjectOrganizationProvider.errorOrMessageHandlingData?.message));

            return true;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += OnActivePageChanged;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
        }

        private void OnActivePageChanged(IPage page) => Refresh();
    }
}
