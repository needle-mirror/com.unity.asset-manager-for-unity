using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class DependencyFoldoutItem : VisualElement
    {
        const string k_DetailsPageFileItemUssStyle = "details-page-dependency-item";
        const string k_DetailsPageFileItemInfoUssStyle = "details-page-dependency-item-info";
        const string k_DetailsPageFileItemStatusUssStyle = "details-page-dependency-item-status";
        const string k_DetailsPageFileIconItemUssStyle = "details-page-dependency-item-icon";
        const string k_DetailsPageFileLabelItemUssStyle = "details-page-dependency-item-label";
        const string k_DetailsPageDependencySelectionItemDisabledUssStyle = "details-page-dependency-selection-disabled";
        const string k_DetailsPageDependencyButtonUssStyle = "details-page-dependency-selection-button";
        const string k_DetailsPageDependencyButtonCaretUssStyle = "details-page-dependency-selection-button-caret";

        readonly Button m_Button;
        readonly VisualElement m_Icon;
        readonly Label m_FileName;
        readonly VisualElement m_ImportedStatusIcon;
        readonly Label m_VersionNumber;
        readonly Button m_DependencyVersionButton;
        readonly TextElement m_DependencyVersionButtonText;

        readonly IPopupManager m_PopupManager;
        readonly DependencyFoldoutItemViewModel m_ViewModel;

        public DependencyFoldoutItem(DependencyFoldoutItemViewModel viewModel, IPopupManager popupManager)
        {
            m_PopupManager = popupManager;
            m_ViewModel = viewModel;

            m_Button = new Button(() =>
            {
                m_ViewModel.NavigateToDependency();
            });

            Add(m_Button);

            m_Button.focusable = false;
            m_Button.AddToClassList(k_DetailsPageFileItemUssStyle);

            var infoElement = new VisualElement();
            infoElement.AddToClassList(k_DetailsPageFileItemInfoUssStyle);

            m_FileName = new Label("");
            m_Icon = new VisualElement();

            m_Icon.AddToClassList(k_DetailsPageFileIconItemUssStyle);
            m_FileName.AddToClassList(k_DetailsPageFileLabelItemUssStyle);

            infoElement.Add(m_Icon);
            infoElement.Add(m_FileName);

            var statusElement = new VisualElement();
            statusElement.AddToClassList(k_DetailsPageFileItemStatusUssStyle);

            m_ImportedStatusIcon = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };

            m_VersionNumber = new Label();
            m_VersionNumber.AddToClassList("asset-version");
            m_VersionNumber.text = L10n.Tr(Constants.LoadingText);

            if (m_ViewModel.IsDependencySelectionEnabled())
            {
                m_VersionNumber.visible = false;

                m_DependencyVersionButton = new Button();
                m_DependencyVersionButton.AddToClassList(k_DetailsPageDependencyButtonUssStyle);
                statusElement.Add(m_DependencyVersionButton);

                m_DependencyVersionButtonText = new TextElement();
                m_DependencyVersionButtonText.text = L10n.Tr(Constants.LoadingText);
                m_DependencyVersionButtonText.AddToClassList("unity-text-element");
                m_DependencyVersionButton.Add(m_DependencyVersionButtonText);

                var caret = new VisualElement();
                caret.AddToClassList(k_DetailsPageDependencyButtonCaretUssStyle);
                m_DependencyVersionButton.Add(caret);
            }
            else
            {
                statusElement.Add(m_VersionNumber);
            }

            statusElement.Add(m_ImportedStatusIcon);

            m_Button.Add(infoElement);
            m_Button.Add(statusElement);
            m_Button.SetEnabled(false);

            m_ViewModel.AssetDataChanged += OnAssetDataChanged;
        }


        public async Task Bind(AssetIdentifier dependencyIdentifier)
        {
            await m_ViewModel.Bind(dependencyIdentifier);
        }

        void OnAssetDataChanged()
        {
            if (m_ViewModel.IsDependencySelectionEnabled())
            {
                UpdateVersionSelectionDisplayText();
                BuildVersionSelection();

                m_DependencyVersionButton.RegisterCallback<ClickEvent>(evt =>
                {
                    BuildVersionSelection();
                    m_PopupManager.Show(m_DependencyVersionButton, PopupContainer.PopupAlignment.BottomRight);
                });
            }

            RefreshUI();
        }

        void RefreshUI()
        {
            m_Button.SetEnabled(!string.IsNullOrEmpty(m_ViewModel.AssetId));

            m_FileName.text = m_ViewModel.AssetName ?? $"{m_ViewModel.AssetId} (unavailable)";

            SetStatuses(m_ViewModel.AssetAttributes.GetOverallStatus());

            m_Icon.style.backgroundImage = AssetDataTypeHelper.GetIconForExtension(m_ViewModel.AssetPrimaryExtension);
            m_Icon.tooltip = m_ViewModel.AssetPrimaryExtension;

            SetVersionNumber(m_ViewModel.AssetData);

            if (m_ViewModel.IsDependencySelectionEnabled() && m_ViewModel.NeedToRefreshVersions())
                UpdateVersionSelectionDisplayText();
        }

        void BuildVersionSelection()
        {
            m_PopupManager.Clear();
            var versionSelection = new ScrollView();

            var versionLabelTitle = new Label(L10n.Tr(Constants.VersionLabelSelectionTitle));
            versionLabelTitle.AddToClassList(UssStyle.k_FilterSectionLabel);
            versionSelection.Add(versionLabelTitle);

            foreach (var versionLabel in m_ViewModel.VersionLabels)
            {
                if (versionLabel == "Pending") // Skip the pending label (not allowed to be selected by user)
                    continue;

                var versionLabelSelection = new TextElement();
                versionLabelSelection.AddToClassList(UssStyle.k_FilterItemSelection);
                versionLabelSelection.text = versionLabel;
                versionLabelSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();

                    m_ViewModel.SetAssetVersionAndLabel(AssetManagerCoreConstants.NewVersionId, versionLabel);
                    m_DependencyVersionButtonText.text = versionLabel;
                    m_PopupManager.Hide();
                });
                versionSelection.Add(versionLabelSelection);
            }

            var line = new VisualElement();
            line.AddToClassList(UssStyle.k_FilterSeparatorLine);
            versionSelection.Add(line);

            var fixedVersionTitle = new Label(L10n.Tr(Constants.FixedVersionSelectionTitle));
            fixedVersionTitle.AddToClassList(UssStyle.k_FilterSectionLabel);
            versionSelection.Add(fixedVersionTitle);

            foreach (var versionNumber in m_ViewModel.VersionIds.Keys)
            {
                var versionLabelSelection = new TextElement();
                versionLabelSelection.AddToClassList(UssStyle.k_FilterItemSelection);
                versionLabelSelection.text = versionNumber;
                versionLabelSelection.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();

                    var version = versionNumber == Constants.NewVersionText ? AssetManagerCoreConstants.NewVersionId : m_ViewModel.VersionIds[versionNumber];
                    m_ViewModel.SetAssetVersionAndLabel(version, string.Empty);
                    m_DependencyVersionButtonText.text = versionNumber;
                    m_PopupManager.Hide();
                });
                versionSelection.Add(versionLabelSelection);
            }

            m_PopupManager.Container.Add(versionSelection);
        }

        void UpdateVersionSelectionDisplayText()
        {
            if(!string.IsNullOrEmpty(m_ViewModel.AssetVersionLabel))
            {
                m_DependencyVersionButtonText.text = m_ViewModel.AssetVersionLabel;
            }
            else
            {
                var versionDisplayText = m_ViewModel.VersionIds.FirstOrDefault(versionDisplayIds =>
                    versionDisplayIds.Value == m_ViewModel.AssetVersion).Key;

                if (string.IsNullOrEmpty(versionDisplayText)) // that means it's trying to select "new version" but it's not present
                {
                    versionDisplayText = m_ViewModel.VersionIds.FirstOrDefault().Key ?? string.Empty;
                }

                m_DependencyVersionButtonText.text = versionDisplayText;
            }
        }

        // Common code between this and the AssetPreview class
        void SetStatuses(IEnumerable<AssetPreview.IStatus> statuses)
        {
            if (statuses == null)
            {
                UIElementsUtils.SetDisplay(m_ImportedStatusIcon, false);
                return;
            }

            var validStatuses = statuses.Where(s => s != null).ToList();
            validStatuses.Remove(AssetDataStatus.Linked); // Hack : we need to extract the linked from the status and have only one status instead of a list
            var hasStatuses = validStatuses.Any();
            UIElementsUtils.SetDisplay(m_ImportedStatusIcon, hasStatuses);

            m_ImportedStatusIcon.Clear();

            if (!hasStatuses)
                return;

            foreach (var status in validStatuses)
            {
                var statusElement = status.CreateVisualTree();
                statusElement.tooltip = L10n.Tr(status.Description);
                statusElement.style.position = Position.Relative;
                m_ImportedStatusIcon.Add(statusElement);
            }
        }

        // Common code between this and the AssetDetailsHeader class
        void SetVersionNumber(BaseAssetData assetData)
        {
            UIElementsUtils.SetDisplay(m_VersionNumber, assetData != null);
            if (assetData == null)
                return;

            UIElementsUtils.SetSequenceNumberText(m_VersionNumber, assetData, m_ViewModel.AssetVersionLabel);
        }
    }
}
