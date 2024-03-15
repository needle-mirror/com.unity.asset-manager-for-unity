using System;

namespace Unity.AssetManager.Editor
{
    internal enum ErrorOrMessageRecommendedAction
    {
        OpenServicesSettingButton,
        OpenAssetManagerDashboardLink,
        EnableProject,
        Retry,
        None
    }

    [Serializable]
    internal class ErrorOrMessageHandlingData
    {
        public string message;

        public ErrorOrMessageRecommendedAction errorOrMessageRecommendedAction;
    }
}
