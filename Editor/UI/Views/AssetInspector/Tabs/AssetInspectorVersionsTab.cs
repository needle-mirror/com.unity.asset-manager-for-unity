using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string DetailsPageContentContainer = "details-page-content-container";
        public const string AssetVersionDetailsFoldout = "asset-version-details-foldout";
        public const string AssetVersionLabelContainer = "asset-version-label-container";
        public const string AssetVersionLabel = "asset-version-label";
        public const string AssetVersionLabel_Filled = "asset-version-label--filled";
        public const string AssetVersionLabel_Imported = "asset-version-label--imported";
        public const string UnityFoldoutInput = "unity-foldout__input";
    }

    class AssetInspectorVersionsTab : IPageComponent
    {
        const string k_FoldoutLabelsContainer = "foldout-labels-container";
        const string k_ImportedTagContainer = "imported-tag";
        const string k_PreferencesProjectId = "selected-project-id";

        static readonly string k_NoChangelogProvided = $"<i>{L10n.Tr(Constants.NoChangeLogText)}</i>";

        readonly IDialogManager m_DialogManager;
        readonly IUIPreferences m_UIPreferences;
        readonly List<string> m_ImportedVersions = new List<string>();
        readonly AssetInspectorViewModel m_ViewModel;

        string m_CurrentProjectId;
        bool m_IsLoading;
        CancellationTokenSource m_LoadingTaskCancellationTokenSource;
        UIEnabledStates m_LastEnabledStates;
        BaseOperation m_LastOperationInProgress;

        public VisualElement Root { get; }

        public AssetInspectorVersionsTab(VisualElement visualElement, IDialogManager dialogManager, AssetInspectorViewModel viewModel)
        {
            var root = new VisualElement();
            root.AddToClassList(UssStyle.DetailsPageContentContainer);
            visualElement.Add(root);

            m_DialogManager = dialogManager;
            m_UIPreferences = ServicesContainer.instance.Resolve<IUIPreferences>();
            m_ViewModel = viewModel;

            m_CurrentProjectId = m_UIPreferences.GetString(k_PreferencesProjectId, string.Empty);
            m_ViewModel.VersionsRefreshed += RefreshAll;
            Root = root;
        }

        public void OnSelection()
        {
            if (m_CurrentProjectId != m_ViewModel.ProjectId)
            {
                m_UIPreferences.RemoveAll("foldout:");
                m_CurrentProjectId = m_ViewModel.ProjectId;
                m_UIPreferences.SetString(k_PreferencesProjectId, m_CurrentProjectId);
            }

            // Have the loaded version expanded by default if the foldout is not already set
            if (!m_UIPreferences.Contains($"foldout:{m_ViewModel.AssetId}"))
            {
                m_UIPreferences.SetBool($"foldout:{m_ViewModel.AssetId}", true);
                m_UIPreferences.SetBool(GetFoldoutKey(m_ViewModel.AssetIdentifier), true);
            }

            m_IsLoading = true;
            _ = m_ViewModel.RefreshAssetVersionsAsync();
            RefreshUI();
        }

        async void RefreshAll()
        {
            m_IsLoading = false;
            RefreshUI();

            if (m_ViewModel.SelectedAssetData == null)
                return;

            var enabled = m_ViewModel.GetUIEnabledStates();
            var hasPermissions = m_ViewModel.IsAssetFromLibrary || await m_ViewModel.CheckPermissionAsync();
            enabled |= UIEnabledStates.HasPermissions.GetFlag(hasPermissions);
            RefreshButtons(enabled, m_ViewModel.GetAssetOperation());
        }

        public void RefreshUI(bool isLoading = false)
        {
            Root.Clear();

            TryDisplayLoadingMessage();

            if(m_IsLoading || m_ViewModel.AssetVersions == null)
                return;

            foreach (var data in m_ViewModel.AssetVersions)
            {
                var foldout = CreateFoldout(data);

                if (data.Labels != null)
                {
                    foreach (var label in data.Labels)
                    {
                        AssetInspectorUIElementHelper.AddText(foldout.parent.Q(k_FoldoutLabelsContainer), null, label.Name, new[] {UssStyle.AssetVersionLabel, UssStyle.AssetVersionLabel_Filled});
                    }
                }

                if (data.SequenceNumber > 0)
                {
                    AssetInspectorUIElementHelper.AddHeader(foldout, Constants.ChangeLogText);
                    AssetInspectorUIElementHelper.AddText(foldout, null, string.IsNullOrEmpty(data.Changelog) ? k_NoChangelogProvided : data.Changelog);
                }

                if (data.ParentSequenceNumber > 0)
                {
                    AssetInspectorUIElementHelper.AddText(foldout, Constants.CreatedFromText, L10n.Tr(Constants.VersionText) + " " + data.ParentSequenceNumber);
                }

                AssetInspectorUIElementHelper.AddUser(foldout, Constants.CreatedByText, data.CreatedBy, typeof(CreatedByFilter));
                AssetInspectorUIElementHelper.AddText(foldout, Constants.DateText, data.Updated?.ToLocalTime().ToString("G"));
                AssetInspectorUIElementHelper.AddText(foldout, Constants.StatusText, data.Status);

                var importButton = new ImportButton(m_DialogManager);
                foldout.Add(importButton);
                importButton.RegisterCallback(importLocation => BeginImport(importLocation, data));
                _ = ResolveVersionDatasetsAndUpdateButtonAsync(data, importButton);
            }
        }

        async Task ResolveVersionDatasetsAndUpdateButtonAsync(BaseAssetData versionData, ImportButton button)
        {
            await versionData.ResolveDatasetsAsync();

            if (!versionData.HasImportableFiles())
            {
                button.SetEnabled(false);
                button.tooltip = L10n.Tr(Constants.ImportNoFilesTooltip);
                return;
            }

            // Version has files; apply the last known enabled state so the button
            // reflects permissions / services / operation status correctly.
            var enabled = m_LastEnabledStates;
            var assetOperation = m_LastOperationInProgress as AssetDataOperation;
            var versionIsImported = m_ViewModel.AssetIdentifier != null && enabled.HasFlag(UIEnabledStates.InProject) && m_ViewModel.AssetIdentifier.Equals(versionData.Identifier);
            var versionOperation = assetOperation?.Identifier.Version == versionData.Identifier.Version ? m_LastOperationInProgress : null;
            button.SetEnabled(enabled.IsImportAvailable());
            button.tooltip = AssetInspectorUIElementHelper.GetImportButtonTooltip(versionOperation, enabled, versionIsImported, hasFiles: true);
            button.text = AssetInspectorUIElementHelper.GetImportButtonLabel(versionOperation, versionIsImported ? AssetDataStatus.Imported : null);
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress)
        {
            if (operationInProgress is { Status: OperationStatus.Paused })
            {
                return;
            }

            m_LastEnabledStates = enabled;
            m_LastOperationInProgress = operationInProgress;
            m_ImportedVersions.Clear();

            var assetOperation = operationInProgress as AssetDataOperation;
            var baseImportAvailable = enabled.IsImportAvailable();

            if (m_ViewModel.AssetVersions == null)
                return;

            foreach (var versionData in m_ViewModel.AssetVersions)
            {
                var identifier = versionData.Identifier;
                var foldoutContainer = Root.Q(identifier.Version);

                if (foldoutContainer == null)
                    continue;

                var versionOperation = assetOperation?.Identifier.Version == identifier.Version ? operationInProgress : null;
                var versionIsImported = enabled.HasFlag(UIEnabledStates.InProject) && m_ViewModel.AssetIdentifier.Equals(identifier);
                var versionHasFiles = versionData.HasImportableFiles();

                if (versionIsImported)
                {
                    m_ImportedVersions.Add(identifier.Version);
                }

                RefreshImportedChip(foldoutContainer, versionIsImported && !enabled.HasFlag(UIEnabledStates.IsImporting));

                var button = foldoutContainer.Q<ImportButton>();
                button.text = AssetInspectorUIElementHelper.GetImportButtonLabel(versionOperation, versionIsImported ? AssetDataStatus.Imported : null);
                button.tooltip = AssetInspectorUIElementHelper.GetImportButtonTooltip(versionOperation, enabled, versionIsImported, hasFiles: versionHasFiles);
                button.SetEnabled(baseImportAvailable && versionHasFiles);
            }
        }

        void ClearLoadingCancellationTokenSource()
        {
            if (m_LoadingTaskCancellationTokenSource != null)
            {
                m_LoadingTaskCancellationTokenSource.Cancel();
                m_LoadingTaskCancellationTokenSource.Dispose();
            }

            m_LoadingTaskCancellationTokenSource = null;
        }

        void TryDisplayLoadingMessage()
        {
            if (m_IsLoading)
            {
                AssetInspectorUIElementHelper.AddLoadingText(Root);
            }
            else if (m_ViewModel.AssetVersions != null && !m_ViewModel.AssetVersions.Any())
            {
                AssetInspectorUIElementHelper.AddText(Root, null, $"<i>{L10n.Tr(Constants.StatusErrorText)}</i>");
            }
        }

        Foldout CreateFoldout(BaseAssetData assetVersion)
        {
            var foldoutContainer = new VisualElement
            {
                name = assetVersion.Identifier.Version
            };
            Root.Add(foldoutContainer);

            var title = L10n.Tr(Constants.VersionText) + " " + assetVersion.SequenceNumber;
            if (assetVersion.SequenceNumber <= 0)
            {
                title = L10n.Tr(Constants.PendingVersionText);

                if (assetVersion.ParentSequenceNumber > 0)
                {
                    title += $" ({L10n.Tr(Constants.FromVersionText)} {assetVersion.ParentSequenceNumber})";
                }
            }

            var key = GetFoldoutKey(assetVersion.Identifier);

            var foldout = new Foldout
            {
                text = title,
                value = m_UIPreferences.GetBool(key, false)
            };
            foldout.AddToClassList(UssStyle.AssetVersionDetailsFoldout);
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    m_UIPreferences.SetBool(key, true);
                }
                else
                {
                    m_UIPreferences.Remove(key);
                }
            });

            foldoutContainer.Add(foldout);

            var labelsContainer = new VisualElement
            {
                name = k_FoldoutLabelsContainer
            };
            labelsContainer.AddToClassList(UssStyle.AssetVersionLabelContainer);
            var foldoutLabel = foldout.Q(null, UssStyle.UnityFoldoutInput);
            foldoutLabel.Add(labelsContainer);

            return foldout;
        }

        void BeginImport(string importLocation, BaseAssetData assetData)
        {
            var trigger = m_ImportedVersions.Contains(assetData.Identifier.Version) ? ImportTrigger.ReimportVersion : ImportTrigger.ImportVersion;
            m_ViewModel.ImportAssetAsync(trigger, importLocation, new List<BaseAssetData> {assetData});
        }

        static void RefreshImportedChip(VisualElement foldoutContainer, bool isChipEnabled)
        {
            var importedTag = foldoutContainer.Q(k_ImportedTagContainer);

            if (isChipEnabled)
            {
                if (importedTag == null)
                {
                    AssetInspectorUIElementHelper.AddText(foldoutContainer.Q(k_FoldoutLabelsContainer), null, Constants.ImportedTagText,
                        new[] {UssStyle.AssetVersionLabel, UssStyle.AssetVersionLabel_Imported}, k_ImportedTagContainer);
                }
            }
            else
            {
                importedTag?.RemoveFromHierarchy();
            }
        }

        static string GetFoldoutKey(AssetIdentifier identifier)
        {
            return $"foldout:{identifier.AssetId}_{identifier.Version}";
        }
    }
}
