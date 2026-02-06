using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string DetailsPageEntriesContainer = "details-page-entries-container";
        public const string DetailsPageThumbnailContainer = "details-page-thumbnail-container";
        public const string ImageContainer = "image-container";
    }

    class AssetInspectorMetadataTab : IPageComponent, IEditableComponent
    {
        const string k_FileSizeName = "file-size";
        const string k_FileCountName = "file-count";

        readonly AssetPreview m_AssetPreview;
        readonly VisualElement m_OutdatedWarningBox;
        readonly VisualElement m_OfflineMessageBox;
        readonly VisualElement m_TrackingOverlapWarningBox;
        readonly Label m_TrackingOverlapWarningLabel;
        readonly VisualElement m_EntriesContainer;
        readonly IPageManager m_PageManager;
        readonly IStateManager m_StateManager;
        readonly IPopupManager m_PopupManager;
        readonly ISettingsManager m_SettingsManager;
        readonly IProjectOrganizationProvider m_projectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        private readonly AssetInspectorViewModel m_ViewModel;

        public VisualElement Root { get; }

        readonly Func<bool> m_IsFilterActive;

        AssetDependenciesComponent m_DependenciesComponent;

        List<IEditableEntry> m_EditableEntries = new();

        public bool IsEditingEnabled { get; private set; }
        public event Action<AssetFieldEdit> FieldEdited;

        public AssetInspectorMetadataTab(VisualElement visualElement, Func<bool> isFilterActive,
            IPageManager pageManager, IStateManager stateManager, IPopupManager popupManager,
            ISettingsManager settingsManager, IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnectProxy, AssetInspectorViewModel viewModel)
        {
            var root = visualElement.Q("details-page-content-container");
            Root = root;
            m_PageManager = pageManager;
            m_StateManager = stateManager;
            m_PopupManager = popupManager;
            m_SettingsManager = settingsManager;
            m_projectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;

            m_EntriesContainer = new VisualElement();
            m_EntriesContainer.AddToClassList(UssStyle.DetailsPageEntriesContainer);
            root.Add(m_EntriesContainer);
            m_EntriesContainer.SendToBack();

            m_OutdatedWarningBox = root.Q("outdated-warning-box");
            m_OutdatedWarningBox.Q<Label>().text = L10n.Tr(Constants.FilteredAssetOutdatedWarning);
            m_OutdatedWarningBox.SendToBack();

            m_OfflineMessageBox = new VisualElement();
            m_OfflineMessageBox.AddToClassList("offline-warning-box");
            var offlineIcon = new Image();
            m_OfflineMessageBox.Add(offlineIcon);
            var offlineLabel = new Label(L10n.Tr(Constants.OfflineMessageText));
            offlineLabel.AddToClassList("text-bold");
            m_OfflineMessageBox.Add(offlineLabel);
            UIElementsUtils.Hide(m_OfflineMessageBox);

            m_TrackingOverlapWarningBox = new VisualElement();
            m_TrackingOverlapWarningBox.AddToClassList("tracking-overlap-warning-box");
            var trackingOverlapIcon = new Image();
            m_TrackingOverlapWarningBox.Add(trackingOverlapIcon);
            m_TrackingOverlapWarningLabel = new Label();
            m_TrackingOverlapWarningBox.Add(m_TrackingOverlapWarningLabel);
            UIElementsUtils.Hide(m_TrackingOverlapWarningBox);

            m_AssetPreview = new AssetPreview {name = "details-page-asset-preview"};
            m_AssetPreview.AddToClassList(UssStyle.DetailsPageThumbnailContainer);
            m_AssetPreview.AddToClassList(UssStyle.ImageContainer);

            m_IsFilterActive = isFilterActive;

            m_ViewModel = viewModel;
            BindViewModelEvents();

            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
        }

        void OnCloudServicesReachabilityChanged(bool isReachable)
        {
            RefreshOfflineMessage();
        }

        void BindViewModelEvents()
        {
            m_ViewModel.StatusInformationUpdated += () => RefreshUI();
            m_ViewModel.AssetDependenciesUpdated += () => UpdateDependencyComponent(m_ViewModel.SelectedAssetData);
            m_ViewModel.PreviewImageUpdated += SetPreviewImage;
            m_ViewModel.PreviewStatusUpdated += UpdatePreviewStatus;
            m_ViewModel.AssetDataAttributesUpdated += UpdateStatusWarning;
            m_ViewModel.FilesChanged += SetPrimaryExtension;

            m_ViewModel.PropertiesUpdated += () =>
            {
                RefreshUI();
                OnFileChanged();
            };

            m_ViewModel.LinkedProjectsUpdated += () =>
            {
                RefreshUI();
                OnFileChanged();
            };
        }

        public void OnSelection()
        {
            m_AssetPreview.ClearPreview();
        }

        public void RefreshUI(bool isLoading = false)
        {
            // Remove asset preview from hierarchy to avoid it being destroyed when clearing the container
            m_AssetPreview.RemoveFromHierarchy();

            // Save any pending changes before clearing the entries
            // This ensures edits are committed with the correct asset context
            // Use ToList() to create a copy since SavePendingChanges may trigger events that modify the collection
            foreach (var entry in m_EditableEntries.ToList())
            {
                entry.SavePendingEdits();
            }

            m_EntriesContainer.Clear();
            m_EditableEntries.Clear();

            m_AssetPreview.SetAssetType(m_ViewModel.AssetPrimaryExtension);
            m_EntriesContainer.Add(m_AssetPreview);
            SetPreviewImage(m_ViewModel.AssetThumbnail);

            UpdatePreviewStatus(m_ViewModel.GetOverallStatus());
            UpdateStatusWarning(m_ViewModel.AssetAttributes);

            // Offline message: same placement as no-files-warning-box (under thumbnail, above data fields)
            m_EntriesContainer.Add(m_OfflineMessageBox);
            RefreshOfflineMessage();

            m_EntriesContainer.Add(m_TrackingOverlapWarningBox);
            RefreshTrackingOverlapWarning();

            AssetInspectorUIElementHelper.AddSpace(m_EntriesContainer);

            if (!string.IsNullOrWhiteSpace(m_ViewModel.AssetDescription) || IsEditingEnabled)
            {
                var assetId = (m_ViewModel.SelectedAssetData as UploadAssetData)?.ExistingAssetIdentifier?.AssetId ?? m_ViewModel.AssetId;
                // Capture the identifier at creation time to ensure edits are applied to the correct asset
                var capturedIdentifier = m_ViewModel.AssetIdentifier;
                var descriptionEntry = AssetInspectorUIElementHelper.AddEditableText(m_EntriesContainer, assetId, Constants.DescriptionText, m_ViewModel.AssetDescription);
                descriptionEntry.EntryEdited += value => OnEntryEdited(capturedIdentifier, EditField.Description, value);
                descriptionEntry.IsEntryEdited += IsDescriptionEdited;
                descriptionEntry.EnableEditing(IsEditingEnabled);
                m_EditableEntries.Add(descriptionEntry);
            }

            if (!string.IsNullOrWhiteSpace(m_ViewModel.AssetStatus))
            {
                var assetId = (m_ViewModel.SelectedAssetData as UploadAssetData)?.ExistingAssetIdentifier?.AssetId ?? m_ViewModel.AssetId;
                // Capture the identifier at creation time to ensure edits are applied to the correct asset
                var capturedIdentifier = m_ViewModel.AssetIdentifier;
                var statusEntry = AssetInspectorUIElementHelper.AddEditableStatusDropdown(m_EntriesContainer, assetId, Constants.StatusText, m_ViewModel.AssetStatus, m_ViewModel.AssetReachableStatus);
                statusEntry.EntryEdited += value => OnEntryEdited(capturedIdentifier, EditField.Status, value);
                statusEntry.IsEntryEdited += IsStatusEdited;
                statusEntry.EnableEditing(IsEditingEnabled);
                m_EditableEntries.Add(statusEntry);
            }

            var projectIds = GetProjectIdsDisplayList(m_ViewModel.ProjectId, m_ViewModel.LinkedProjects);
            var projectEntryTitle = projectIds.Length > 1 ? Constants.ProjectsText : Constants.ProjectText;
            AssetInspectorUIElementHelper.AddProjectChips(m_EntriesContainer, projectEntryTitle, projectIds, "entry-project");
            AssetInspectorUIElementHelper.AddCollectionChips(m_EntriesContainer, Constants.CollectionsText, m_ViewModel.LinkedCollections);

            if (m_ViewModel.AssetTags.Any() || IsEditingEnabled)
            {
                var assetId = (m_ViewModel.SelectedAssetData as UploadAssetData)?.ExistingAssetIdentifier?.AssetId ?? m_ViewModel.AssetId;
                // Capture the identifier at creation time to ensure edits are applied to the correct asset
                var capturedIdentifier = m_ViewModel.AssetIdentifier;
                var tagsEntry = AssetInspectorUIElementHelper.AddEditableTagList(m_EntriesContainer, assetId, Constants.TagsText, m_ViewModel.AssetTags);
                tagsEntry.EntryEdited += value => OnEntryEdited(capturedIdentifier, EditField.Tags, value);
                tagsEntry.IsEntryEdited += AreTagsEdited;
                tagsEntry.EnableEditing(IsEditingEnabled);
                m_EditableEntries.Add(tagsEntry);
            }

            m_DependenciesComponent = new AssetDependenciesComponent(m_EntriesContainer, m_PageManager, m_PopupManager, m_SettingsManager, m_projectOrganizationProvider, m_StateManager);
            m_DependenciesComponent.RefreshUI(m_ViewModel.SelectedAssetData, isLoading);
            DisplayMetadata(m_ViewModel.SelectedAssetData);

            AssetInspectorUIElementHelper.AddText(m_EntriesContainer, Constants.FilesSizeText, "-", isSelectable:false, k_FileSizeName);
            AssetInspectorUIElementHelper.AddText(m_EntriesContainer, Constants.TotalFilesText, "-", isSelectable:false, k_FileCountName);

            // Temporary solution to display targeted assets during re-upload. Very handy to understand which assets the system is re-uploading to.
            var identifier = m_ViewModel.AssetIdentifier;
            if (m_ViewModel.SelectedAssetData is UploadAssetData uploadAssetData)
            {
                identifier = uploadAssetData.TargetAssetIdentifier ?? identifier;
            }
            AssetInspectorUIElementHelper.AddAssetIdentifier(m_EntriesContainer, Constants.AssetIdText, identifier);

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            AssetInspectorUIElementHelper.AddText(m_EntriesContainer, Constants.AssetTypeText, assetsProvider.GetValueAsString(m_ViewModel.AssetType), isSelectable: true);

            AssetInspectorUIElementHelper.AddText(m_EntriesContainer, Constants.LastModifiedText, Utilities.DatetimeToString(m_ViewModel.AssetUpdatedOn));
            AssetInspectorUIElementHelper.AddUser(m_EntriesContainer, Constants.LastEditByText, m_ViewModel.AssetUpdatedBy, typeof(UpdatedByFilter));
            AssetInspectorUIElementHelper.AddText(m_EntriesContainer, Constants.UploadDateText, Utilities.DatetimeToString(m_ViewModel.AssetCreatedOn));
            AssetInspectorUIElementHelper.AddUser(m_EntriesContainer, Constants.CreatedByText, m_ViewModel.AssetCreatedBy, typeof(CreatedByFilter));

            SetFileCount(m_ViewModel.GetFilesCount());
            SetFileSize(m_ViewModel.GetFileSize());

            if (isLoading)
            {
                AssetInspectorUIElementHelper.AddLoadingText(m_EntriesContainer);
            }
        }

        void DisplayMetadata(BaseAssetData assetData)
        {
            // Dot not display metadata for local assets (a.k.a UploadAssetData)
            if (assetData.Identifier.IsLocal())
                return;

            foreach (var metadata in assetData.Metadata)
            {
                switch (metadata.Type)
                {
                    case MetadataFieldType.Text:
                    {
                        var textMetadata = (Core.Editor.TextMetadata)metadata;
                        AssetInspectorUIElementHelper.AddText(m_EntriesContainer, textMetadata.Name, textMetadata.Value, isSelectable: true);
                        break;
                    }
                    case MetadataFieldType.Boolean:
                    {
                        var booleanMetadata = (Core.Editor.BooleanMetadata)metadata;
                        AssetInspectorUIElementHelper.AddToggle(m_EntriesContainer, booleanMetadata.Name, booleanMetadata.Value);
                        break;
                    }
                    case MetadataFieldType.Number:
                    {
                        var numberMetadata = (Core.Editor.NumberMetadata)metadata;
                        AssetInspectorUIElementHelper.AddText(m_EntriesContainer, numberMetadata.Name,
                            numberMetadata.Value.ToString(CultureInfo.CurrentCulture), isSelectable: true);
                        break;
                    }
                    case MetadataFieldType.Timestamp:
                    {
                        var timestampMetadata = (Core.Editor.TimestampMetadata)metadata;
                        AssetInspectorUIElementHelper.AddText(m_EntriesContainer, timestampMetadata.Name,
                            Utilities.DatetimeToString(timestampMetadata.Value.DateTime), isSelectable: true);
                        break;
                    }
                    case MetadataFieldType.Url:
                    {
                        var urlMetadata = (Core.Editor.UrlMetadata)metadata;
                        AssetInspectorUIElementHelper.AddText(m_EntriesContainer, urlMetadata.Name,
                            urlMetadata.Value.Uri == null ? string.Empty : urlMetadata.Value.Uri.ToString(), isSelectable: true);
                        break;
                    }
                    case MetadataFieldType.User:
                    {
                        var userMetadata = (Core.Editor.UserMetadata)metadata;
                        AssetInspectorUIElementHelper.AddUser(m_EntriesContainer, metadata.Name, userMetadata.Value, null);
                        break;
                    }
                    case MetadataFieldType.SingleSelection:
                    {
                        var singleSelectionMetadata = (Core.Editor.SingleSelectionMetadata)metadata;
                        AssetInspectorUIElementHelper.AddSelectionChips(m_EntriesContainer, metadata.Name, new List<string> {singleSelectionMetadata.Value}, isSelectable: true);
                        break;
                    }
                    case MetadataFieldType.MultiSelection:
                    {
                        var multiSelectionMetadata = (Core.Editor.MultiSelectionMetadata)metadata;
                        AssetInspectorUIElementHelper.AddSelectionChips(m_EntriesContainer, metadata.Name, multiSelectionMetadata.Value, isSelectable: true);
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Unexpected metadata field type was encountered.");
                }
            }
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress)
        {
            m_EntriesContainer.Q("entry-project")?.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));
        }

        public void UpdatePreviewStatus(IEnumerable<AssetPreview.IStatus> status)
        {
            m_AssetPreview.SetStatuses(status);
        }

        public void UpdateDependencyComponent(BaseAssetData assetData)
        {
            m_DependenciesComponent.RefreshUI(assetData, false);
        }

        public void UpdateStatusWarning(AssetDataAttributeCollection assetDataAttributeCollection)
        {
            // If the selected asset is outdated and selected via filtered results, we want to warn
            // the user that the asset may not be displaying all up-to-date information.

            var status = assetDataAttributeCollection?.GetAttribute<ImportAttribute>()?.Status;

            var filterActive = m_IsFilterActive?.Invoke() ?? false;
            var displayOutdatedWarning = filterActive && status == ImportAttribute.ImportStatus.OutOfDate;
            UIElementsUtils.SetDisplay(m_OutdatedWarningBox, displayOutdatedWarning);
        }

        void SetPreviewImage(Texture2D texture)
        {
            m_AssetPreview.SetThumbnail(texture);
        }

        void SetFileCount(string fileCount)
        {
            m_EntriesContainer.Q<DetailsPageEntry>(k_FileCountName)?.SetText(fileCount);
        }

        void SetFileSize(string fileSize)
        {
            m_EntriesContainer.Q<DetailsPageEntry>(k_FileSizeName)?.SetText(fileSize);
        }

        void SetPrimaryExtension()
        {
            m_AssetPreview.SetAssetType(m_ViewModel.AssetPrimaryExtension);
        }

        static string[] GetProjectIdsDisplayList(string currentProjectId, IEnumerable<ProjectIdentifier> linkedProjects)
        {
            // Ensure the current project is first in the list
            var uniqueProjectIds = new HashSet<string>() { currentProjectId };
            foreach (var linkedProject in linkedProjects)
                uniqueProjectIds.Add(linkedProject.ProjectId);

            return uniqueProjectIds.ToArray();
        }

        bool IsDescriptionEdited(string assetId, object description)
        {
            var importedAssetData = m_ViewModel.GetImportedAssetInfo();
            return !string.Equals(importedAssetData?.AssetData?.Description, description as string, StringComparison.Ordinal);
        }

        bool AreTagsEdited(string assetId, object tags)
        {
            var importedAssetData = m_ViewModel.GetImportedAssetInfo();
            var tagsCollection = tags as IEnumerable<string> ?? Enumerable.Empty<string>();
            var importedTags = importedAssetData?.AssetData?.Tags ?? Enumerable.Empty<string>();

            return !importedTags.SequenceEqual(tagsCollection);
        }

        bool IsStatusEdited(string assetId, object value)
        {
            var importedAssetData = m_ViewModel.GetImportedAssetInfo();
            return !string.Equals(importedAssetData?.AssetData?.Status, value as string, StringComparison.Ordinal);
        }

        void OnEntryEdited(AssetIdentifier assetIdentifier, EditField fieldType, object editValue)
        {
            var edit = new AssetFieldEdit(assetIdentifier, fieldType, editValue);
            FieldEdited?.Invoke(edit);
        }

        public void EnableEditing(bool enable)
        {
            foreach (var editableEntry in m_EditableEntries)
            {
                editableEntry.EnableEditing(enable);
            }
            IsEditingEnabled = enable;
        }

        void OnFileChanged()
        {
            SetPrimaryExtension();
            SetFileCount(m_ViewModel.GetFilesCount());
            SetFileSize(m_ViewModel.GetFileSize());
        }

        void RefreshOfflineMessage()
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Show(m_OfflineMessageBox);
            }
            else
            {
                UIElementsUtils.Hide(m_OfflineMessageBox);
            }
        }

        void RefreshTrackingOverlapWarning()
        {
            var overlaps = m_ViewModel.GetTrackingOverlaps();
            if (overlaps == null || overlaps.Count == 0)
            {
                UIElementsUtils.Hide(m_TrackingOverlapWarningBox);
                return;
            }

            const int k_MaxDisplayedConflicts = 3;

            var distinctAssets = overlaps
                .Select(o => (Name: o.ConflictingAssetName, Id: o.ConflictingAssetId))
                .Where(a => !string.IsNullOrEmpty(a.Id))
                .Distinct()
                .ToList();

            var lines = distinctAssets
                .Take(k_MaxDisplayedConflicts)
                .Select(a =>
                {
                    var shortId = a.Id.Length > 8 ? a.Id[..8] : a.Id;
                    var displayName = string.IsNullOrEmpty(a.Name) ? shortId : a.Name;
                    return $" \u2022 {displayName} ({shortId})";
                });

            var message = L10n.Tr(Constants.AssetInspectorTrackingOverlapWarning)
                + "\n" + string.Join("\n", lines);

            var remaining = distinctAssets.Count - k_MaxDisplayedConflicts;
            if (remaining > 0)
                message += $"\n... and {remaining} more.";

            m_TrackingOverlapWarningLabel.text = message;
            UIElementsUtils.Show(m_TrackingOverlapWarningBox);
        }
    }
}
