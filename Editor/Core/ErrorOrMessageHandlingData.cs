using System;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    enum ErrorOrMessageRecommendedAction
    {
        OpenServicesSettingButton,
        OpenAssetManagerDashboardLink,
        EnableProject,
        Retry,
        None
    }

    [Serializable]
    class ErrorOrMessageHandlingData
    {
        public string Message;
        public ErrorOrMessageRecommendedAction ErrorOrMessageRecommendedAction;
    }
}
