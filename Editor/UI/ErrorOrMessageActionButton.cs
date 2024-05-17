using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class ErrorOrMessageActionButton : Button
    {
        const string k_ButtonClassName = "errorOrMessageView-button";
        const string k_LinkClassName = "errorOrMessageView-action-button-link";
        readonly ILinksProxy m_LinksProxy;

        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public ErrorOrMessageActionButton(IPageManager pageManager,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy)
        {
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_LinksProxy = linksProxy;
        }

        public void SetErrorSuggestion(ErrorOrMessageRecommendedAction action, bool isPageError)
        {
            clickable = null;
            UIElementsUtils.Show(this);

            switch (action)
            {
                case ErrorOrMessageRecommendedAction.OpenServicesSettingButton:
                {
                    RemoveFromClassList(k_LinkClassName);
                    AddToClassList(k_ButtonClassName);
                    clicked += m_LinksProxy.OpenProjectSettingsServices;

                    tooltip = L10n.Tr("Open Project Settings");
                    text = tooltip;
                }
                    break;
                case ErrorOrMessageRecommendedAction.EnableProject:
                {
                    RemoveFromClassList(k_LinkClassName);
                    AddToClassList(k_ButtonClassName);
                    clicked += m_ProjectOrganizationProvider.EnableProjectForAssetManager;

                    tooltip = L10n.Tr("Enable Project");
                    text = tooltip;
                }
                    break;
                case ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink:
                {
                    RemoveFromClassList(k_ButtonClassName);
                    AddToClassList(k_LinkClassName);
                    clicked += m_LinksProxy.OpenAssetManagerDashboard;

                    tooltip = L10n.Tr("Open the Asset Manager Dashboard");
                    text = tooltip;
                }
                break;
                case ErrorOrMessageRecommendedAction.Retry when isPageError:
                {
                    RemoveFromClassList(k_LinkClassName);
                    AddToClassList(k_ButtonClassName);
                    clicked += ClearActivePage;

                    tooltip = L10n.Tr("Retry");
                    text = tooltip;
                }
                    break;
                default:
                    UIElementsUtils.Hide(this);
                    break;
            }
        }

        void ClearActivePage()
        {
            m_PageManager.ActivePage?.Clear(true);
        }
    }
}
