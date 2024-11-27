using System;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    interface IGridMessageView
    {
        bool Refresh();
    }

    class GridMessageView : ScrollView, IGridMessageView
    {
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        readonly Label m_MessageLabel;
        readonly MessageActionButton m_MessageActionButton;

        public GridMessageView(IPageManager pageManager,
            IProjectOrganizationProvider projectOrganizationProvider, ILinksProxy linksProxy)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_MessageLabel = new Label();
            m_MessageActionButton =
                new MessageActionButton(pageManager, projectOrganizationProvider, linksProxy);

            Add(m_MessageLabel);
            Add(m_MessageActionButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        // Returns true if error view is visible, otherwise returns false
        public bool Refresh()
        {
            // Prioritize ProjectOrganizationProvider message, if any
            var hasProjectOrganizationProviderMessage =
                m_ProjectOrganizationProvider.MessageData?.IsPageScope;

            if (!string.IsNullOrEmpty(m_ProjectOrganizationProvider.MessageData?.Message) &&
                hasProjectOrganizationProviderMessage != null &&
                (bool)hasProjectOrganizationProviderMessage)
            {
                DisplayMessage(m_ProjectOrganizationProvider.MessageData);
                return true;
            }

            // Then show PageManager message, if any
            var hasPageManagerMessage =
                m_PageManager.ActivePage?.MessageData.IsPageScope;

            if (!string.IsNullOrEmpty(m_PageManager.ActivePage?.MessageData.Message) &&
                hasPageManagerMessage != null &&
                (bool)hasPageManagerMessage)
            {
                DisplayMessage(m_PageManager.ActivePage?.MessageData);
                return true;
            }

            UIElementsUtils.Hide(this);
            return false;
        }

        void DisplayMessage(MessageData messageData)
        {
            UIElementsUtils.Show(this);

            m_MessageLabel.text = L10n.Tr(messageData.Message);
            m_MessageActionButton.SetRecommendedAction(messageData.RecommendedAction);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.MessageThrown += OnPageManagerMessageThrown;
            m_ProjectOrganizationProvider.MessageThrown += OnProjectOrganizationProviderMessageThrown;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.MessageThrown -= OnPageManagerMessageThrown;
            m_ProjectOrganizationProvider.MessageThrown -= OnProjectOrganizationProviderMessageThrown;
        }

        void OnPageManagerMessageThrown(IPage _, MessageData messageData)
        {
            if (messageData.IsPageScope)
            {
                DisplayMessage(messageData);
            }
        }

        void OnProjectOrganizationProviderMessageThrown(MessageData messageData)
        {
            if (messageData.IsPageScope)
            {
                DisplayMessage(messageData);
            }
        }

    }
}
