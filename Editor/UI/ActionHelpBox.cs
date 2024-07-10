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

        readonly MessageActionButton m_MessageActionButton;

        MessageData m_MessageData;
        
        static readonly string k_NoConnectionMessage = L10n.Tr("You are offline.");
        static readonly string k_NoConnectionUploadPageMessage = L10n.Tr("Connect to the internet to upload your assets.");

        public ActionHelpBox(IUnityConnectProxy unityConnectProxy, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_MessageActionButton = new MessageActionButton(pageManager, projectOrganizationProvider,
                linksProxy);

            Add(m_MessageActionButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        public void Refresh()
        {
            messageType = HelpBoxMessageType.Info;
            m_MessageActionButton.visible = false;
            
            if (!m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Show(this);
                messageType = HelpBoxMessageType.Warning;
                text = m_PageManager.ActivePage is UploadPage ? $"{k_NoConnectionMessage} {k_NoConnectionUploadPageMessage}" : k_NoConnectionMessage;
                return;
            }

            if (m_MessageData == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            var hasErrorMessage = !string.IsNullOrEmpty(m_MessageData.Message);
            var isPageScope = m_MessageData.IsPageScope;
            
            if (!hasErrorMessage || isPageScope)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            text = m_MessageData.Message;
            m_MessageActionButton.SetRecommendedAction(m_MessageData.RecommendedAction);
            m_MessageActionButton.visible = true;
        
            UIElementsUtils.Show(this);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_PageManager.MessageThrown += OnPageManagerMessageThrown;
            m_ProjectOrganizationProvider.MessageThrown += OnProjectOrganizationProviderMessageThrown;
        }
        
        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_PageManager.MessageThrown -= OnPageManagerMessageThrown;
            m_ProjectOrganizationProvider.MessageThrown -= OnProjectOrganizationProviderMessageThrown;
        }
        
        void OnPageManagerMessageThrown(IPage _, MessageData messageData)
        {
            m_MessageData = messageData;
            Refresh();
        }
        
        void OnProjectOrganizationProviderMessageThrown(MessageData messageData)
        {
            m_MessageData = messageData;
            Refresh();
        }
        
        void OnActivePageChanged(IPage page) => Refresh();
    }
}
