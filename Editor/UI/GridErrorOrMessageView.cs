using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class GridErrorOrMessageView : ScrollView
    {
        private Label m_ErrorMessageLabel;
        private ErrorOrMessageActionButton m_ErrorOrMessageActionButton;

        private readonly IPageManager m_PageManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public GridErrorOrMessageView(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider, ILinksProxy linksProxy)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_ErrorMessageLabel = new Label();
            m_ErrorOrMessageActionButton = new ErrorOrMessageActionButton(pageManager, linksProxy);

            Add(m_ErrorMessageLabel);
            Add(m_ErrorOrMessageActionButton);
        }

        // Returns true if error view is visible, otherwise returns false
        public bool Refresh()
        {
            var isProjectError = !string.IsNullOrEmpty(m_ProjectOrganizationProvider.errorOrMessageHandlingData?.message);
            var isInProject = m_PageManager.activePage.pageType == PageType.InProject;
            ErrorOrMessageHandlingData errorHandlingData;
            if (isInProject || !isProjectError)
                errorHandlingData = m_PageManager.activePage?.errorOrMessageHandlingData;
            else
                errorHandlingData = m_ProjectOrganizationProvider.errorOrMessageHandlingData;
            
            if (string.IsNullOrWhiteSpace(errorHandlingData?.message))
            {
                UIElementsUtils.Hide(this);
                return false;
            }
            UIElementsUtils.Show(this);

            m_ErrorMessageLabel.tooltip = L10n.Tr(errorHandlingData.message);
            m_ErrorMessageLabel.text = L10n.Tr(errorHandlingData.message);
            m_ErrorOrMessageActionButton.SetErrorSuggestion(errorHandlingData.errorOrMessageRecommendedAction, !isProjectError);

            return true;
        }
    }
}
