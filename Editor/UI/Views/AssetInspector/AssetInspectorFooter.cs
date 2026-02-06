using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string DetailsPageFooterContainer = "details-page-footer-container";
        public const string DetailsPageFooterHelpBox = "details-page-footer-helpbox";
    }

    class AssetInspectorFooter : IPageComponent
    {
        readonly ImportButton m_ImportButton;
        readonly Button m_ShowInProjectBrowserButton;
        readonly RemoveButton m_RemoveButton;
        readonly OperationProgressBar m_OperationProgressBar;
        readonly VisualElement m_FooterVisualElement;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly Button m_ShowInDashboardButton;
        readonly HelpBox m_AssetLibraryHelpBox;
        readonly AssetInspectorViewModel m_ViewModel;

        public VisualElement ButtonsContainer { get; }

        AssetPreview.IStatus m_ImportStatus;

        public AssetInspectorFooter(VisualElement visualElement, IDialogManager dialogManager, AssetInspectorViewModel viewModel)
        {
            m_ViewModel = viewModel;
            BindViewModelEvents();

            m_FooterVisualElement = visualElement.Q("footer");

            m_PageManager = ServicesContainer.instance.Resolve<IPageManager>();
            m_AssetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            var operationsContainer = new VisualElement();
            operationsContainer.AddToClassList(UssStyle.DetailsPageFooterContainer);
            m_FooterVisualElement.Add(operationsContainer);

            m_OperationProgressBar = new OperationProgressBar(CancelOperationInProgress);
            operationsContainer.Add(m_OperationProgressBar);

            var buttonsContainer = new VisualElement();
            buttonsContainer.AddToClassList(UssStyle.DetailsPageFooterContainer);
            m_FooterVisualElement.Add(buttonsContainer);

            ButtonsContainer = buttonsContainer;

            m_ImportButton = new ImportButton(dialogManager)
            {
                focusable = false
            };
            ButtonsContainer.Add(m_ImportButton);
            m_ShowInProjectBrowserButton = CreateBigButton(ButtonsContainer, Constants.ShowInProjectActionText);
            m_RemoveButton = new RemoveButton(false)
            {
                text = L10n.Tr(Constants.RemoveFromProjectActionText),
                focusable = false
            };
            ButtonsContainer.Add(m_RemoveButton);

            m_AssetLibraryHelpBox = new HelpBox(L10n.Tr(Constants.AssetLibraryFooterHelpBoxText), HelpBoxMessageType.Info);
            m_AssetLibraryHelpBox.AddToClassList(UssStyle.DetailsPageFooterHelpBox);
            ButtonsContainer.Add(m_AssetLibraryHelpBox);

            m_ShowInDashboardButton = CreateBigButton(ButtonsContainer, Constants.OpenInBrowserText);
            ButtonsContainer.Add(m_ShowInDashboardButton);

            m_ImportButton.RegisterCallback(BeginImport);



            m_ShowInProjectBrowserButton.clicked += () => m_ViewModel.ShowInProjectBrowser();
            m_ShowInDashboardButton.clicked += () => m_ViewModel.LinkToDashboard();



            m_RemoveButton.RemoveWithExclusiveDependencies += RemoveFromProject;
            m_RemoveButton.RemoveOnlySelected += RemoveOnlySelectedFromProject;
            m_RemoveButton.StopTracking += OnStopTracking;
            m_RemoveButton.StopTrackingOnlySelected += OnStopTrackingOnlySelected;
        }

        void BindViewModelEvents()
        {
            m_ViewModel.PreviewStatusUpdated += UpdatePreviewStatus;
        }

        public void OnSelection() { }

        public void RefreshUI(bool isLoading = false)
        {
            m_ImportStatus = null;
            UIElementsUtils.SetDisplay(m_FooterVisualElement, ((BasePage)m_PageManager.ActivePage).DisplayFooter);
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress)
        {
            var isEnabled = enabled.IsImportAvailable() && m_ViewModel.HasFiles;

            m_ImportStatus = m_ViewModel.AssetAttributes?.GetStatusOfImport();

            m_ImportButton.text = AssetInspectorUIElementHelper.GetImportButtonLabel(operationInProgress, m_ImportStatus);
            m_ImportButton.tooltip = AssetInspectorUIElementHelper.GetImportButtonTooltip(operationInProgress, enabled, hasFiles: m_ViewModel.HasFiles);
            m_ImportButton.SetEnabled(isEnabled);

            m_ShowInProjectBrowserButton.SetEnabled(enabled.HasFlag(UIEnabledStates.InProject) && m_ViewModel.HasFiles);
            m_ShowInProjectBrowserButton.tooltip = enabled.HasFlag(UIEnabledStates.InProject) && m_ViewModel.HasFiles
                ? L10n.Tr(Constants.ShowInProjectButtonToolTip)
                : L10n.Tr(Constants.ShowInProjectButtonDisabledToolTip);

            var isRemoveEnabled = enabled.HasFlag(UIEnabledStates.InProject) && !enabled.HasFlag(UIEnabledStates.IsImporting);
            m_RemoveButton.text = isRemoveEnabled ?
                $"{L10n.Tr(Constants.RemoveFromProjectActionText)} ({m_AssetDataManager.FindExclusiveDependencies(new List<AssetIdentifier>{m_ViewModel.AssetIdentifier}).Count})" :
                L10n.Tr(Constants.RemoveFromProjectActionText);
            m_RemoveButton.SetEnabled(isRemoveEnabled);
            m_RemoveButton.tooltip = enabled.HasFlag(UIEnabledStates.InProject)
                ? L10n.Tr(Constants.RemoveFromProjectButtonToolTip)
                : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);

            m_OperationProgressBar.Refresh(operationInProgress);

            m_RemoveButton.style.display = m_ViewModel.IsAssetFromLibrary ? DisplayStyle.None : DisplayStyle.Flex;
            m_ShowInProjectBrowserButton.style.display = m_ViewModel.IsAssetFromLibrary ? DisplayStyle.None : DisplayStyle.Flex;
            m_ImportButton.style.display = m_ViewModel.IsAssetFromLibrary ? DisplayStyle.None : DisplayStyle.Flex;
            m_ShowInDashboardButton.style.display = m_ViewModel.IsAssetFromLibrary ? DisplayStyle.Flex : DisplayStyle.None;
            m_AssetLibraryHelpBox.style.display = m_ViewModel.IsAssetFromLibrary ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void UpdatePreviewStatus(IEnumerable<AssetPreview.IStatus> status)
        {
            if (m_ImportButton.enabledSelf)
            {
                m_ImportButton.text = AssetInspectorUIElementHelper.GetImportButtonLabel(null, status?.FirstOrDefault());
            }
        }

        void CancelOperationInProgress()
        {
            m_ViewModel.CancelOrClearImport(m_PageManager.ActivePage.LastSelectedAssetId);
        }

        static Button CreateBigButton(VisualElement container, string text)
        {
            var button = new Button
            {
                text = L10n.Tr(text)
            };
            button.AddToClassList(UssStyle.BigButton);

            button.focusable = false;
            container.Add(button);

            return button;
        }

        void BeginImport(string importLocation)
        {
            m_ImportButton.SetEnabled(false);

            ImportTrigger trigger;
            DetailsButtonClickedEvent.ButtonType buttonType;
            if (m_ImportStatus == null || string.IsNullOrEmpty(m_ImportStatus.ActionText) || m_ImportStatus.ActionText == Constants.ImportActionText)
            {
                trigger = ImportTrigger.Import;
                buttonType = DetailsButtonClickedEvent.ButtonType.Import;
            }
            else
            {
                trigger = m_ImportStatus.ActionText == Constants.ReimportActionText ? ImportTrigger.Reimport : ImportTrigger.UpdateToLatest;
                buttonType = DetailsButtonClickedEvent.ButtonType.Reimport;
            }

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(buttonType));

            m_ViewModel.ImportAssetAsync(trigger, importLocation);
        }

        void RemoveFromProject()
        {
            m_RemoveButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            if (!m_ViewModel.RemoveFromProject())
            {
                m_RemoveButton.SetEnabled(true);
                m_ShowInProjectBrowserButton.SetEnabled(true);
            }
            else
            {
                m_RemoveButton.text = L10n.Tr(Constants.RemoveFromProjectActionText);
                m_RemoveButton.tooltip = L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
            }
        }

        void RemoveOnlySelectedFromProject()
        {
            m_RemoveButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            if (!m_ViewModel.RemoveOnlyAssetFromProject())
            {
                m_RemoveButton.SetEnabled(true);
                m_ShowInProjectBrowserButton.SetEnabled(true);
            }
            else
            {
                m_RemoveButton.text = L10n.Tr(Constants.RemoveFromProjectActionText);
                m_RemoveButton.tooltip = L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
            }
        }

        void OnStopTracking()
        {
            m_RemoveButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            if (!m_ViewModel.StopTracking())
            {
                m_RemoveButton.SetEnabled(true);
                m_ShowInProjectBrowserButton.SetEnabled(true);
            }
            else
            {
                m_RemoveButton.text = L10n.Tr(Constants.RemoveFromProjectActionText);
                m_RemoveButton.tooltip = L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
            }
        }

        void OnStopTrackingOnlySelected()
        {
            m_RemoveButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            if (!m_ViewModel.StopTrackingOnlyAsset())
            {
                m_RemoveButton.SetEnabled(true);
                m_ShowInProjectBrowserButton.SetEnabled(true);
            }
            else
            {
                m_RemoveButton.text = L10n.Tr(Constants.RemoveFromProjectActionText);
                m_RemoveButton.tooltip = L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
            }
        }
    }
}
