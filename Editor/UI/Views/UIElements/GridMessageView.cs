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
        readonly IMessageManager m_MessageManager;

        readonly Label m_MessageLabel;
        readonly MessageActionButton m_MessageActionButton;
        readonly Button m_DismissButton;

        Message m_CurrentMessage;

        public GridMessageView(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IMessageManager messageManager)
        {
            m_MessageLabel = new Label();
            m_MessageActionButton =
                new MessageActionButton(pageManager, projectOrganizationProvider, linksProxy);

            m_DismissButton = new Button(OnDismissClicked)
            {
                text = "×",
                tooltip = L10n.Tr("Dismiss message")
            };
            m_DismissButton.AddToClassList("grid-message-dismiss-button");

            m_MessageManager = messageManager;

            // This is necessary to survive domain reloads
            if (m_MessageManager.GridViewMessage != null &&
                !string.IsNullOrEmpty(m_MessageManager.GridViewMessage.Content))
            {
                m_CurrentMessage = m_MessageManager.GridViewMessage;
            }

            Add(m_MessageLabel);
            Add(m_MessageActionButton);
            Add(m_DismissButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnDismissClicked()
        {
            m_MessageManager.DismissGridViewMessage();
        }

        // Returns true if error view is visible, otherwise returns false
        public bool Refresh()
        {
            if (m_CurrentMessage == null)
            {
                UIElementsUtils.Hide(this);

                return false;
            }

            DisplayMessage(m_CurrentMessage);

            return true;
        }

        void DisplayMessage(Message message)
        {
            UIElementsUtils.Show(this);

            m_MessageLabel.text = L10n.Tr(message.Content);
            m_MessageActionButton.SetRecommendedAction(message.RecommendedAction);

            // Only show dismiss button for dismissable messages
            m_DismissButton.visible = message.Dismissable;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_MessageManager.GridViewMessageSet += OnGridViewMessageSet;
            m_MessageManager.GridViewMessageCleared += OnGridViewMessageCleared;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_MessageManager.GridViewMessageSet -= OnGridViewMessageSet;
            m_MessageManager.GridViewMessageCleared -= OnGridViewMessageCleared;
        }

        void OnGridViewMessageSet(Message message)
        {
            m_CurrentMessage = message;
            Refresh();
        }

        void OnGridViewMessageCleared()
        {
            m_CurrentMessage = null;
            Refresh();
        }
    }
}
