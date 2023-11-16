using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class ErrorView : VisualElement
    {
        IPageManager m_PageManager;

        Label m_ErrorMessageLabel;
        Button m_ErrorActionButton;

        public ErrorView(IPageManager pageManager)
        {
            m_PageManager = pageManager;
            AddToClassList("errorView");
        }

        internal bool Refresh(ErrorHandlingData errorHandling)
        {
            if (string.IsNullOrWhiteSpace(errorHandling.errorMessage))
            {
                UIElementsUtils.Hide(this);
                return false;
            }
            UIElementsUtils.Show(this);

            DisplayErrorMessage(errorHandling.errorMessage);
            DisplayErrorSuggestion(errorHandling.errorRecommendedAction);

            return true;
        }

        private void DisplayErrorMessage(string errorMessage)
        {
            if (m_ErrorMessageLabel == null)
            {
                m_ErrorMessageLabel = new Label();
                m_ErrorMessageLabel.AddToClassList("errorView-errorMessage-label");
            }
            m_ErrorMessageLabel.tooltip = L10n.Tr(errorMessage);
            m_ErrorMessageLabel.text = L10n.Tr(errorMessage);
            Add(m_ErrorMessageLabel);
        }

        private void DisplayErrorSuggestion(ErrorRecommendedAction action)
        {
            if (m_ErrorActionButton == null)
                m_ErrorActionButton = new Button();

            m_ErrorActionButton.clickable = null;

            switch (action)
            {
                case ErrorRecommendedAction.OpenServicesSettingButton:
                {
                    m_ErrorActionButton.RemoveFromClassList("errorView-action-button-link");
                    m_ErrorActionButton.AddToClassList("errorView-button");
                    m_ErrorActionButton.clicked += () => OpenServicesProjectSetting();

                    m_ErrorActionButton.tooltip = L10n.Tr("Open Project Settings");
                    m_ErrorActionButton.text = m_ErrorActionButton.tooltip;
                }
                break;
                case ErrorRecommendedAction.OpenAssetManagerDashboardLink:
                {
                    m_ErrorActionButton.RemoveFromClassList("errorView-button");
                    m_ErrorActionButton.AddToClassList("errorView-action-button-link");
                    m_ErrorActionButton.clicked += () => OpenAssetManagerDashboard();

                    m_ErrorActionButton.tooltip = L10n.Tr("Open the Asset Manager Dashboard");
                    m_ErrorActionButton.text = m_ErrorActionButton.tooltip;
                }
                break;
                case ErrorRecommendedAction.None:
                {
                    m_ErrorActionButton.RemoveFromClassList("errorView-action-button-link");
                    m_ErrorActionButton.AddToClassList("errorView-button");
                    m_ErrorActionButton.clicked += () =>
                    {
                        UIElementsUtils.Hide(this);//TODO: We need to rely on a DataChange event to refresh the UI
                        m_PageManager.activePage?.Clear(true);
                    };

                    m_ErrorActionButton.tooltip = L10n.Tr("Retry");
                    m_ErrorActionButton.text = m_ErrorActionButton.tooltip;
                }
                break;
            }

            Add(m_ErrorActionButton);
        }

        private void OpenAssetManagerDashboard()
        {
            Application.OpenURL("https://dashboard.unity3d.com/");
        }

        private void OpenServicesProjectSetting()
        {
            SettingsService.OpenProjectSettings("Project/Services");
        }
    }
}
