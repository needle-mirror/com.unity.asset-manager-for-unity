using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal enum ErrorRecommendedAction
    {
        OpenServicesSettingButton,
        OpenAssetManagerDashboardLink,
        None
    }

    [Serializable]
    internal class ErrorHandlingData
    {
        public string errorMessage;

        public ErrorRecommendedAction errorRecommendedAction;
    }
}
