using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Random = System.Random;

namespace Unity.AssetManager.UI.Editor
{
    enum TabType
    {
        Details,
        Versions
    }

    struct TabDetails
    {
        public Button TabButton { get; }
        public VisualElement TabContent { get; }
        public bool DisplayFooter { get; }
        public bool EnabledWhenDisconnected { get; }

        public TabDetails(Button tabButton, VisualElement tabContent, bool displayFooter, bool enabledWhenDisconnected)
        {
            TabButton = tabButton;
            TabContent = tabContent;
            DisplayFooter = displayFooter;
            EnabledWhenDisconnected = enabledWhenDisconnected;
        }
    }

    class AssetInspectorHeader : IPageComponent, IEditableComponent
    {
        readonly VisualElement m_BorderLine;
        readonly Label m_AssetName;
        readonly Label m_AssetVersion;
        readonly Image m_AssetDashboardLink;
        readonly AssetInspectorViewModel m_ViewModel;

        TextField m_TextField;

        IAssetDataManager m_AssetDataManager;

        // Tab container properties
        readonly VisualElement m_TabsContainer;
        readonly VisualElement m_Footer;

        readonly Dictionary<TabType, TabDetails> m_TabContents = new();

        readonly IUIPreferences m_UIPreferences;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        bool m_IsFooterVisible;

        TabType ActiveTabType
        {
            get => (TabType)m_UIPreferences.GetInt("AssetDetailsPageTabs.ActiveTab", 0);
            set => m_UIPreferences.SetInt("AssetDetailsPageTabs.ActiveTab", (int)value);
        }

        public bool IsEditingEnabled { get; private set; }

        public event Action<AssetFieldEdit> FieldEdited;

        public AssetInspectorHeader(VisualElement visualElement, IAssetDataManager assetDataManager, AssetInspectorViewModel viewModel, VisualElement footer, AssetInspectorMetadataTab metadataTab, AssetInspectorVersionsTab versionsTab)
        {
            m_ViewModel = viewModel;
            m_AssetDataManager  = assetDataManager;

            m_BorderLine = visualElement.Q<VisualElement>("asset-name-borderline");
            m_BorderLine.AddToClassList("asset-entry-borderline-style");

            m_AssetName = visualElement.Q<Label>("asset-name");
            m_AssetName.selection.isSelectable = true;
            m_AssetVersion = visualElement.Q<Label>("asset-version");

            m_TextField = visualElement.Q<TextField>("asset-name-edit-field");
            m_TextField.RegisterCallback<KeyUpEvent>(OnKeyUpEvent);
            m_TextField.RegisterCallback<FocusOutEvent>(_ => OnEntryEdited(m_TextField.value));
            m_TextField.style.display = DisplayStyle.None;

            m_AssetDashboardLink = visualElement.Q<Image>("asset-dashboard-link");
            m_AssetDashboardLink.tooltip = L10n.Tr(Constants.DashboardLinkTooltip);
            m_AssetDashboardLink.RegisterCallback<ClickEvent>(_ =>
            {
                m_ViewModel.LinkToDashboard();
            });

            // tab container
            m_TabsContainer = visualElement.Q("details-page-tabs");
            m_Footer = footer;

            var tabs = new[]
            {
                new { Type = TabType.Details, metadataTab.Root, IsFooterVisible = true, EnabledWhenDisconnected = true },
                new { Type = TabType.Versions, versionsTab.Root, IsFooterVisible = false, EnabledWhenDisconnected = false },
            };

            foreach (var tab in tabs)
            {
                var button = new Button
                {
                    text = L10n.Tr(tab.Type.ToString())
                };
                button.clicked += () =>
                {
                    SetActiveTab(tab.Type);
                };
                button.focusable = false;
                m_TabsContainer.Add(button);

                m_TabContents[tab.Type] = new TabDetails(button, tab.Root, tab.IsFooterVisible, tab.EnabledWhenDisconnected);
            }

            m_UnityConnectProxy = ServicesContainer.instance.Resolve<IUnityConnectProxy>();
            m_UIPreferences = ServicesContainer.instance.Resolve<IUIPreferences>();

            SetActiveTab(ActiveTabType);

            BindViewModelEvents();
        }

        void BindViewModelEvents()
        {
            m_ViewModel.LocalStatusUpdated += () => RefreshUI();
        }

        public void OnSelection()
        {
            if (m_ViewModel.AssetIsLocal)
            {
                SetActiveTab(TabType.Details);
                UIElementsUtils.Hide(m_TabsContainer);
            }
            else
            {
                // This is not a local asset, you can show the tabs
                UIElementsUtils.Show(m_TabsContainer);
            }
        }

        public void RefreshUI(bool isLoading = false)
        {
            m_AssetName.text = m_ViewModel.AssetName;
            m_TextField.value = m_ViewModel.AssetName;

            UIElementsUtils.SetSequenceNumberText(m_AssetVersion, m_ViewModel.SelectedAssetData);
            UIElementsUtils.SetDisplay(m_AssetDashboardLink, m_ViewModel.AssetHasValidDashboardLink());

            UpdateStyling();

            //tab container logic
            foreach (var kvp in m_TabContents)
            {
                var button = kvp.Value.TabButton;
                if (m_UnityConnectProxy.AreCloudServicesReachable || kvp.Value.EnabledWhenDisconnected)
                {
                    button.SetEnabled(kvp.Key != ActiveTabType);
                    kvp.Value.TabButton.RemoveFromClassList("details-page-tabs-button--disabled");
                }
                else
                {
                    button.SetEnabled(false);
                    kvp.Value.TabButton.AddToClassList("details-page-tabs-button--disabled");
                }
            }

            if(!m_UnityConnectProxy.AreCloudServicesReachable && !m_TabContents[ActiveTabType].EnabledWhenDisconnected)
            {
                foreach (var kvp in m_TabContents)
                {
                    if (!kvp.Value.EnabledWhenDisconnected) continue;

                    SetActiveTab(kvp.Key);
                    return;
                }

                // If no tab is enabled when disconnected, hide the active content
                UIElementsUtils.Hide(m_TabContents[ActiveTabType].TabContent);
            }
            else if (m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Show(m_TabContents[ActiveTabType].TabContent);
            }
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress)
        {
            m_AssetDashboardLink.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));

            UIElementsUtils.SetDisplay(m_Footer, m_IsFooterVisible && enabled.HasFlag(UIEnabledStates.CanImport));
        }

        void SetActiveTab(TabType activeTabType)
        {
            ActiveTabType = activeTabType;

            foreach (var kvp in m_TabContents)
            {
                var isActive = activeTabType == kvp.Key;
                kvp.Value.TabButton.SetEnabled(!isActive && (m_UnityConnectProxy.AreCloudServicesReachable || kvp.Value.EnabledWhenDisconnected));
                UIElementsUtils.SetDisplay(kvp.Value.TabContent, isActive);

                if (isActive)
                {
                    m_IsFooterVisible = kvp.Value.DisplayFooter;
                    UIElementsUtils.SetDisplay(m_Footer, m_IsFooterVisible);
                }
            }
        }

        public void EnableEditing(bool enable)
        {
            if (enable == IsEditingEnabled)
                return;

            m_TextField.value = m_AssetName.text;

            m_AssetName.style.display = enable ? DisplayStyle.None : DisplayStyle.Flex;
            m_TextField.style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;

            IsEditingEnabled = enable;
        }

        void OnKeyUpEvent(KeyUpEvent evt)
        {
            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                OnEntryEdited(m_TextField.value);
        }

        void OnEntryEdited(string newValue)
        {
            if (newValue == m_AssetName.text)
                return;

            // Empty values are not supported
            if (string.IsNullOrWhiteSpace(newValue))
            {
                m_TextField.value = m_AssetName.text;
                return;
            }

            m_AssetName.text = newValue;

            var fieldEdit = new AssetFieldEdit(m_ViewModel.AssetIdentifier, EditField.Name, newValue);
            FieldEdited?.Invoke(fieldEdit);

            UpdateStyling();
        }

        void UpdateStyling()
        {
            var isEdited = m_ViewModel.IsNameEdited(m_AssetName.text);
            if (isEdited)
            {
                m_BorderLine.style.backgroundColor = UssStyle.EditedBorderColor;
                m_TextField.AddToClassList(UssStyle.DetailsPageEntryValueEdited);
            }
            else
            {
                m_BorderLine.style.backgroundColor = Color.clear;
                m_TextField.RemoveFromClassList(UssStyle.DetailsPageEntryValueEdited);
            }
        }
    }
}
