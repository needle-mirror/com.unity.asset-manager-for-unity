using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string DetailsPageFooterContainer = "details-page-footer-container";
    }

    class AssetDetailsFooter : IPageComponent
    {
        readonly ImportButton m_ImportButton;
        readonly Button m_ShowInProjectBrowserButton;
        readonly Button m_RemoveImportButton;
        readonly OperationProgressBar m_OperationProgressBar;

        public VisualElement ButtonsContainer { get; }

        public event Action CancelOperation;
        public event Action<string, IEnumerable<IAssetData>> ImportAsset;
        public event Action HighlightAsset;
        public event Func<bool> RemoveAsset;

        public AssetDetailsFooter(VisualElement visualElement)
        {
            var footer = visualElement.Q("footer");

            var operationsContainer = new VisualElement();
            operationsContainer.AddToClassList(UssStyle.DetailsPageFooterContainer);
            footer.Add(operationsContainer);

            m_OperationProgressBar = new OperationProgressBar(CancelOperationInProgress);
            operationsContainer.Add(m_OperationProgressBar);

            var buttonsContainer = new VisualElement();
            buttonsContainer.AddToClassList(UssStyle.DetailsPageFooterContainer);
            footer.Add(buttonsContainer);

            ButtonsContainer = buttonsContainer;

            m_ImportButton = new ImportButton();
            ButtonsContainer.Add(m_ImportButton);
            m_ShowInProjectBrowserButton = CreateBigButton(ButtonsContainer, Constants.ShowInProjectActionText);
            m_RemoveImportButton = CreateBigButton(ButtonsContainer, Constants.RemoveFromProjectActionText);

            m_ImportButton.RegisterCallback(BeginImport);
            m_ShowInProjectBrowserButton.clicked += ShowInProjectBrowser;
            m_RemoveImportButton.clicked += RemoveFromProject;
        }

        public void OnSelection(IAssetData assetData, bool isLoading) { }

        public void RefreshUI(IAssetData assetData, bool isLoading = false) { }

        public void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress)
        {
            var isEnabled = enabled.IsImportAvailable();

            m_ImportButton.text = AssetDetailsPageExtensions.GetImportButtonLabel(operationInProgress, assetData.PreviewStatus.FirstOrDefault());
            m_ImportButton.tooltip = AssetDetailsPageExtensions.GetImportButtonTooltip(operationInProgress, enabled);
            m_ImportButton.SetEnabled(isEnabled);

            m_ShowInProjectBrowserButton.SetEnabled(enabled.HasFlag(UIEnabledStates.InProject));
            m_ShowInProjectBrowserButton.tooltip = enabled.HasFlag(UIEnabledStates.InProject)
                ? L10n.Tr(Constants.ShowInProjectButtonToolTip)
                : L10n.Tr(Constants.ShowInProjectButtonDisabledToolTip);

            m_RemoveImportButton.SetEnabled(enabled.HasFlag(UIEnabledStates.InProject) && !enabled.HasFlag(UIEnabledStates.IsImporting));
            m_RemoveImportButton.tooltip = enabled.HasFlag(UIEnabledStates.InProject)
                ? L10n.Tr(Constants.RemoveFromProjectButtonToolTip)
                : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);

            m_OperationProgressBar.Refresh(operationInProgress);
        }

        public void UpdatePreviewStatus(IEnumerable<AssetPreview.IStatus> status)
        {
            if (m_ImportButton.enabledSelf)
            {
                m_ImportButton.text = AssetDetailsPageExtensions.GetImportButtonLabel(null, status?.FirstOrDefault());
            }
        }

        void CancelOperationInProgress()
        {
            CancelOperation?.Invoke();
        }

        static Button CreateBigButton(VisualElement container, string text)
        {
            var button = new Button
            {
                text = L10n.Tr(text)
            };
            button.AddToClassList(UssStyle.BigButton);

            container.Add(button);

            return button;
        }

        void BeginImport(string importLocation)
        {
            m_ImportButton.SetEnabled(false);

            var buttonType = m_ImportButton.text == Constants.ImportActionText ? DetailsButtonClickedEvent.ButtonType.Import : DetailsButtonClickedEvent.ButtonType.Reimport;
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(buttonType));

            ImportAsset?.Invoke(importLocation, null);
        }

        void ShowInProjectBrowser()
        {
            HighlightAsset?.Invoke();
        }

        void RemoveFromProject()
        {
            m_RemoveImportButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            if (!RemoveAsset?.Invoke() ?? false)
            {
                m_RemoveImportButton.SetEnabled(true);
                m_ShowInProjectBrowserButton.SetEnabled(true);
            }
        }
    }
}
