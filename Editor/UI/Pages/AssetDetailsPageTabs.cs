using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class AssetDetailsPageTabs : IPageComponent
    {
        public enum TabType
        {
            Details,
            Versions
        }

        struct TabDetails
        {
            public Button TabButton { get; }
            public VisualElement TabContent { get; }
            public bool DisplayFooter { get; }

            public TabDetails(Button tabButton, VisualElement tabContent, bool displayFooter)
            {
                TabButton = tabButton;
                TabContent = tabContent;
                DisplayFooter = displayFooter;
            }
        }

        readonly VisualElement m_TabsContainer;
        readonly VisualElement m_Footer;

        readonly Dictionary<TabType, TabDetails> m_TabContents = new();

        readonly IUIPreferences m_UIPreferences;

        bool m_IsFooterVisible;

        public AssetDetailsPageTabs(VisualElement visualElement, VisualElement footer, IEnumerable<AssetTab> assetTabs)
        {
            m_TabsContainer = visualElement.Q("details-page-tabs");
            m_Footer = footer;

            foreach (var tab in assetTabs)
            {
                var button = new Button
                {
                    text = L10n.Tr(tab.Type.ToString())
                };
                button.clicked += () => { SetActiveTab(tab.Type); };
                m_TabsContainer.Add(button);

                m_TabContents[tab.Type] = new TabDetails(button, tab.Root, tab.IsFooterVisible);
            }

            m_UIPreferences = ServicesContainer.instance.Resolve<IUIPreferences>();

            var activeTab = (TabType) m_UIPreferences.GetInt("AssetDetailsPageTabs.ActiveTab", 0);

            SetActiveTab(activeTab);
        }

        public void OnSelection(IAssetData assetData, bool isLoading)
        {
            if (!String.IsNullOrEmpty(assetData.Identifier.AssetId)
                && String.IsNullOrEmpty(assetData.Identifier.PrimarySourceFileGuid))
            {
                // This is not a local asset, you can show the tabs
                UIElementsUtils.Show(m_TabsContainer);
            }
            else
            {
                SetActiveTab(TabType.Details);
                UIElementsUtils.Hide(m_TabsContainer);
            }
        }

        public void RefreshUI(IAssetData assetData, bool isLoading = false) { }

        public void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress)
        {
            UIElementsUtils.SetDisplay(m_Footer, m_IsFooterVisible && enabled.HasFlag(UIEnabledStates.CanImport));
        }

        void SetActiveTab(TabType activeTabType)
        {
            m_UIPreferences.SetInt("AssetDetailsPageTabs.ActiveTab", (int) activeTabType);

            foreach (var kvp in m_TabContents)
            {
                var isActive = activeTabType == kvp.Key;
                kvp.Value.TabButton.SetEnabled(!isActive);
                UIElementsUtils.SetDisplay(kvp.Value.TabContent, isActive);

                if (isActive)
                {
                    m_IsFooterVisible = kvp.Value.DisplayFooter;
                    UIElementsUtils.SetDisplay(m_Footer, m_IsFooterVisible);
                }
            }
        }
    }
}
