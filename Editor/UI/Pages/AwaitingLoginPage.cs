using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AwaitingLoginPage : VisualElement
    {
        const string k_SignInUXMLName = "SignIn";

        public AwaitingLoginPage()
        {
            VisualTreeAsset windowContent = UIElementsUtils.LoadUXML(k_SignInUXMLName);
            windowContent.CloneTree(this);

            UIElementsUtils.RemoveCustomStylesheets(this);
            UIElementsUtils.LoadCommonStyleSheet(this);

            if (!string.IsNullOrEmpty(CloudProjectSettings.accessToken) && !CloudProjectSettings.userName.ToLowerInvariant().Equals("anonymous"))
            {
                UIElementsUtils.SetupLabel("lblTitle", L10n.Tr("Awaiting Cloud Services Authorization"), this);
                UIElementsUtils.SetupLabel("lblSubtitle", L10n.Tr("Please wait until the operation is completed.\n\nIf you are not automatically signed in, consider restarting the Unity Hub and relaunch your Unity Editor project."), this);
            }
            else
            {
                UIElementsUtils.SetupLabel("lblTitle", L10n.Tr("Awaiting Unity Hub User Session"), this);
                UIElementsUtils.SetupLabel("lblSubtitle", L10n.Tr($"Please sign in the Unity Hub to start a new session.\n\nIf you are already signed in and the issue persists, consider restarting the Unity Hub and relaunch your Unity Editor project."), this);
            }
        }
    }
}
