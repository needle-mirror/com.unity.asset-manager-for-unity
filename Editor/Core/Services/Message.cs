using System;

namespace Unity.AssetManager.Core.Editor
{
    enum RecommendedAction
    {
        OpenServicesSettingButton,
        OpenAssetManagerDashboardLink,
        EnableProject,
        OpenAssetManagerDocumentationPage,
        OpenUnityCloudConfigurationDocumentation,
        OpenTrackingFilesMigrationDocumentation,
        Retry,
        None
    }

    [Serializable]
    class Message
    {
        Guid m_MessageId;
        string m_Content;
        RecommendedAction m_RecommendedAction;
        bool m_Dismissable;

        public Guid MessageId => m_MessageId;
        public string Content => m_Content;
        public RecommendedAction RecommendedAction => m_RecommendedAction;
        /// <summary>
        /// If true, this message will not be cleared by ClearAllMessages and must be explicitly dismissed by the user.
        /// </summary>
        public bool Dismissable => m_Dismissable;

        public Message(string content, RecommendedAction recommendedAction = RecommendedAction.None, bool dismissable = false)
        {
            m_MessageId = Guid.NewGuid();

            m_Content = content;
            m_RecommendedAction = recommendedAction;
            m_Dismissable = dismissable;
        }
    }
}
