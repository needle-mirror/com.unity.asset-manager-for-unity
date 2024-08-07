using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    enum RecommendedAction
    {
        OpenServicesSettingButton,
        OpenAssetManagerDashboardLink,
        EnableProject,
        OpenAssetManagerDocumentationPage,
        Retry,
        None
    }

    [Serializable]
    class MessageData
    {
        public string Message { get; set; }
        public RecommendedAction RecommendedAction { get; set; }
        public bool IsPageScope { get; set; }
        public HelpBoxMessageType MessageType { get; set; }
    }
}
