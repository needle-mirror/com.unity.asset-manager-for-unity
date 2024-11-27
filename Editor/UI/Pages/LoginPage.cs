using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class LoginPage : VisualElement
    {
        const string k_SignInUXMLName = "SignIn";

        public LoginPage()
        {
            VisualTreeAsset windowContent = UIElementsUtils.LoadUXML(k_SignInUXMLName);
            windowContent.CloneTree(this);

            UIElementsUtils.RemoveCustomStylesheets(this);
            UIElementsUtils.LoadCommonStyleSheet(this);

            UIElementsUtils.SetupLabel("lblTitle", L10n.Tr("Sign in"), this);
            UIElementsUtils.SetupLabel("lblSubtitle", L10n.Tr("Please sign in with your Unity ID"), this);
        }
    }
}
