using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class LoginPage : VisualElement
    {
        const string k_SignInUXMLName = "SignIn";

        private readonly IUnityConnectProxy m_UnityConnect;
        public LoginPage(IUnityConnectProxy unityConnect)
        {
            m_UnityConnect = unityConnect;

            VisualTreeAsset windowContent = UIElementsUtils.LoadUXML(k_SignInUXMLName);
            windowContent.CloneTree(this);

            UIElementsUtils.RemoveCustomStylesheets(this);
            UIElementsUtils.LoadCommonStyleSheet(this);

            UIElementsUtils.SetupLabel("lblTitle", L10n.Tr("Sign in"), this);
            UIElementsUtils.SetupLabel("lblSubtitle", L10n.Tr("Please sign in with your Unity ID"), this);
            UIElementsUtils.SetupButton("btnSignIn", OnSignInClicked, true, this, L10n.Tr("Sign in"));
        }

        void OnSignInClicked()
        {
            m_UnityConnect.ShowLogin();
        }
    }
}
