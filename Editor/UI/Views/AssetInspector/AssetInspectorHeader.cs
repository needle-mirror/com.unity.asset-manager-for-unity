using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    enum TabType
    {
        Details,
        Versions,
        Activity
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
        readonly VisualElement m_InspectorRoot;
        readonly VisualElement m_NameContainer;
        readonly VisualElement m_BorderLine;
        readonly Label m_AssetName;
        readonly Label m_AssetVersion;
        readonly Image m_AssetDashboardLink;
        readonly AssetInspectorViewModel m_ViewModel;

        TextField m_TextField;
        InlineEditBehavior m_InlineEditBehavior;
        VisualElement m_NameWrapper;

        readonly IInlineEditService m_InlineEditService;
        readonly IAssetDataManager m_AssetDataManager;

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

        EditingMode m_EditingMode;

        public bool IsEditingEnabled { get; private set; }

        public event Action<AssetFieldEdit> FieldEdited;

        public AssetInspectorHeader(VisualElement visualElement, IAssetDataManager assetDataManager, AssetInspectorViewModel viewModel, VisualElement footer, AssetInspectorMetadataTab metadataTab, AssetInspectorVersionsTab versionsTab, AssetInspectorActivityTab activityTab, IInlineEditService inlineEditService, IUnityConnectProxy unityConnectProxy, IUIPreferences uiPreferences)
        {
            m_ViewModel = viewModel;
            m_AssetDataManager = assetDataManager;
            m_InlineEditService = inlineEditService;
            m_UnityConnectProxy = unityConnectProxy;
            m_UIPreferences = uiPreferences;
            m_InspectorRoot = visualElement;

            m_NameContainer = visualElement.Q("details-page-asset-name-container");
            m_BorderLine = visualElement.Q<VisualElement>("asset-name-borderline");
            m_BorderLine.AddToClassList("asset-entry-borderline-style");

            m_AssetName = visualElement.Q<Label>("asset-name");
            m_AssetName.selection.isSelectable = true;
            m_AssetVersion = visualElement.Q<Label>("asset-version");

            m_TextField = visualElement.Q<TextField>("asset-name-edit-field");
            m_TextField.RegisterCallback<KeyUpEvent>(OnKeyUpEvent);
            m_TextField.RegisterCallback<FocusOutEvent>(OnNameFieldFocusOut);
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
                new { Type = TabType.Details,   Label = "Details",          metadataTab.Root, IsFooterVisible = true,  EnabledWhenDisconnected = true },
                new { Type = TabType.Versions,  Label = "Version History",  versionsTab.Root, IsFooterVisible = false, EnabledWhenDisconnected = false },
                new { Type = TabType.Activity,  Label = "Metadata History", activityTab.Root, IsFooterVisible = false, EnabledWhenDisconnected = false },
            };

            foreach (var tab in tabs)
            {
                var button = new Button
                {
                    text = L10n.Tr(tab.Label)
                };
                button.clicked += () =>
                {
                    SetActiveTab(tab.Type);
                };
                button.focusable = false;
                m_TabsContainer.Add(button);

                m_TabContents[tab.Type] = new TabDetails(button, tab.Root, tab.IsFooterVisible, tab.EnabledWhenDisconnected);
            }

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
                UIElementsUtils.Show(m_TabsContainer);
            }

            RefreshLibraryAssetTabs();
        }

        /// <summary>
        /// Library assets are read-only and carry no metadata history, so the tab that reports it
        /// has nothing to say for them.
        /// </summary>
        void RefreshLibraryAssetTabs()
        {
            var isAssetFromLibrary = m_ViewModel.IsAssetFromLibrary;

            UIElementsUtils.SetDisplay(m_TabContents[TabType.Activity].TabButton, !isAssetFromLibrary);

            if (isAssetFromLibrary && ActiveTabType == TabType.Activity)
            {
                SetActiveTab(TabType.Details);
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

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            m_EditingMode = mode;
            IsEditingEnabled = mode != EditingMode.ReadOnly;

            if (mode == EditingMode.Inline)
                ConfigureInlineMode();
            else
                ConfigureStandardMode();
        }

        public void SetEditDisabledReason(string reason)
        {
            m_NameContainer.tooltip = reason;
        }

        void ConfigureInlineMode()
        {
            if (m_InlineEditBehavior == null)
            {
                m_BorderLine.style.display = DisplayStyle.None;

                m_AssetName.RemoveFromHierarchy();
                m_TextField.RemoveFromHierarchy();
                m_TextField.style.display = DisplayStyle.Flex;
                m_TextField.AddToClassList(UssStyle.DetailsPageEntryValue);

                m_NameWrapper = new VisualElement();
                m_NameWrapper.style.flexGrow = 1;
                m_NameWrapper.style.flexShrink = 1;
                m_NameWrapper.style.minWidth = 0;
                m_NameWrapper.style.overflow = Overflow.Visible;
                m_NameWrapper.style.position = Position.Relative;
                m_NameWrapper.style.marginRight = 2;

                var insertIndex = m_NameContainer.IndexOf(m_BorderLine) + 1;
                m_NameContainer.Insert(insertIndex, m_NameWrapper);

                m_InlineEditBehavior = new InlineEditBehavior(
                    container: m_NameWrapper,
                    popupParent: m_InspectorRoot,
                    textField: m_TextField,
                    onSave: SaveNameAsync,
                    onCancel: () => { },
                    editField: EditField.Name,
                    onValueSaved: v => m_AssetName.text = v,
                    inlineEditService: m_InlineEditService,
                    customMetadataType: null);

                m_InlineEditBehavior.Initialize();
            }

            m_InlineEditBehavior.SetEnabled(true);
        }

        void ConfigureStandardMode()
        {
            if (m_InlineEditBehavior != null)
            {
                m_InlineEditBehavior.Cleanup();
                m_InlineEditBehavior = null;

                m_NameWrapper?.RemoveFromHierarchy();
                m_NameWrapper = null;

                m_TextField.RemoveFromClassList(UssStyle.DetailsPageEntryValue);

                m_AssetName.RemoveFromClassList(UssStyle.DetailsPageEntryValue);
                m_AssetName.AddToClassList("asset-name");

                m_BorderLine.style.display = DisplayStyle.Flex;
                m_NameContainer.Insert(1, m_AssetName);
                m_NameContainer.Insert(2, m_TextField);
                m_TextField.value = m_AssetName.text;
            }

            m_TextField.value = m_AssetName.text;
            m_AssetName.style.display = IsEditingEnabled ? DisplayStyle.None : DisplayStyle.Flex;
            m_TextField.style.display = IsEditingEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        async Task SaveNameAsync(string newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue))
                throw new ArgumentException("Asset name cannot be empty");

            var edit = new AssetFieldEdit(m_ViewModel.AssetIdentifier, EditField.Name, newValue);
            var result = await m_InlineEditService.SaveFieldAsync(edit, default);

            if (!result.Success)
                throw new Exception(result.ErrorMessage);
        }

        void OnNameFieldFocusOut(FocusOutEvent _)
        {
            // In inline mode, InlineEditBehavior handles focus-out and calls SaveAsync; we must not
            // update m_AssetName here or ConfirmEdit will think nothing changed and skip saving.
            if (m_EditingMode == EditingMode.Inline)
                return;
            OnEntryEdited(m_TextField.value);
        }

        void OnKeyUpEvent(KeyUpEvent evt)
        {
            // Only handle keys in non-inline mode (inline mode is handled by InlineEditBehavior)
            if (m_EditingMode == EditingMode.Inline)
                return;

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                OnEntryEdited(m_TextField.value);
        }

        void OnEntryEdited(string newValue)
        {
            if (newValue == m_AssetName.text)
            {
                return;
            }

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
            }
            else
            {
                m_BorderLine.style.backgroundColor = Color.clear;
            }
        }
    }
}
