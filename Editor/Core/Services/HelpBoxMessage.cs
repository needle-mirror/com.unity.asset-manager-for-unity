using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class HelpBoxMessage : Message
    {
        HelpBoxMessageType m_MessageType;
        MessageCategory m_Category;

        public HelpBoxMessageType MessageType => m_MessageType;
        public MessageCategory Category => m_Category;

        public HelpBoxMessage(string content, RecommendedAction recommendedAction = RecommendedAction.None,
            HelpBoxMessageType messageType = HelpBoxMessageType.None, bool dismissable = false,
            MessageCategory category = MessageCategory.General)
            : base(content, recommendedAction, dismissable)
        {
            m_MessageType = messageType;
            m_Category = category;
        }
    }
}
