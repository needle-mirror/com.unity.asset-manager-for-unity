using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class GridErrorOrMessageView : ScrollView
    {
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        Label m_ErrorMessageLabel;
        ErrorOrMessageActionButton m_ErrorOrMessageActionButton;

        public GridErrorOrMessageView(IPageManager pageManager,
            IProjectOrganizationProvider projectOrganizationProvider, ILinksProxy linksProxy)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_ErrorMessageLabel = new Label();
            m_ErrorOrMessageActionButton =
                new ErrorOrMessageActionButton(pageManager, projectOrganizationProvider, linksProxy);

            Add(m_ErrorMessageLabel);
            Add(m_ErrorOrMessageActionButton);
        }

        // Returns true if error view is visible, otherwise returns false
        public bool Refresh()
        {
            var isProjectError =
                !string.IsNullOrEmpty(m_ProjectOrganizationProvider.ErrorOrMessageHandlingData?.Message);
            var isInProject = m_PageManager.ActivePage is InProjectPage;
            ErrorOrMessageHandlingData errorHandlingData;
            if (isInProject || !isProjectError)
            {
                errorHandlingData = m_PageManager.ActivePage?.ErrorOrMessageHandlingData;
            }
            else
            {
                errorHandlingData = m_ProjectOrganizationProvider.ErrorOrMessageHandlingData;
            }

            if (string.IsNullOrWhiteSpace(errorHandlingData?.Message))
            {
                UIElementsUtils.Hide(this);
                return false;
            }

            UIElementsUtils.Show(this);

            m_ErrorMessageLabel.tooltip = L10n.Tr(errorHandlingData.Message);
            m_ErrorMessageLabel.text = L10n.Tr(errorHandlingData.Message);
            m_ErrorOrMessageActionButton.SetErrorSuggestion(errorHandlingData.ErrorOrMessageRecommendedAction,
                !isProjectError);

            return true;
        }
    }
}
