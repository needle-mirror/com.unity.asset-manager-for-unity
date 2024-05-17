using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class ActionHelpBox : HelpBox
    {
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        ErrorOrMessageActionButton m_ErrorOrMessageActionButton;

        static readonly string k_NoConnectionMessage = L10n.Tr("No network connection. Please check your internet connection.");
        
        public ActionHelpBox(IUnityConnectProxy unityConnectProxy, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_ErrorOrMessageActionButton = new ErrorOrMessageActionButton(pageManager, projectOrganizationProvider,
                linksProxy);

            messageType = HelpBoxMessageType.Info;
            Add(m_ErrorOrMessageActionButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        // Returns true if help box is visible, otherwise returns false
        public bool Refresh()
        {
            var notInProject = m_PageManager.ActivePage is not InProjectPage;
            var errorHandlingData =
                string.IsNullOrEmpty(m_ProjectOrganizationProvider.ErrorOrMessageHandlingData?.Message) &&
                notInProject ?
                    m_PageManager.ActivePage?.ErrorOrMessageHandlingData :
                    m_ProjectOrganizationProvider.ErrorOrMessageHandlingData;

            if (!m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Show(this);

                text = k_NoConnectionMessage;
                m_ErrorOrMessageActionButton.SetErrorSuggestion(ErrorOrMessageRecommendedAction.None, false);
                return true;
            }

            if (string.IsNullOrWhiteSpace(errorHandlingData?.Message) || notInProject)
            {
                UIElementsUtils.Hide(this);
                return false;
            }

            UIElementsUtils.Show(this);

            text = errorHandlingData.Message;
            m_ErrorOrMessageActionButton.SetErrorSuggestion(errorHandlingData.ErrorOrMessageRecommendedAction,
                string.IsNullOrEmpty(m_ProjectOrganizationProvider.ErrorOrMessageHandlingData?.Message));

            return true;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
        }

        void OnActivePageChanged(IPage page) => Refresh();
    }
}
