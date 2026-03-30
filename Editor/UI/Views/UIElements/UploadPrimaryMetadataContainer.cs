using System;
using System.Collections.Generic;
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
        public const string MultiAssetDetailsPageEntryRow = "multi-asset-details-page-entry-row";
        public const string MultiAssetDetailsPageEntryValue = "multi-asset-details-page-entry-value";
        public const string MultiAssetDetailsPageChipField = "multi-asset-details-page-chip-field";
    }

    // Supports multi-asset editing for primary metadata fields
    class UploadPrimaryMetadataContainer : VisualElement
    {
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;

        readonly AssetDataSelection m_SelectedAssetsData = new();
        readonly IFieldChangeHandler m_FieldChangeHandler;

        MultiEditTextEntry m_DescriptionEntry;
        MultiEditStatusEntry m_StatusEntry;
        MultiEditTagsEntry m_TagsEntry;

        public UploadPrimaryMetadataContainer(IPageManager pageManager, IAssetDataManager assetDataManager)
        {
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_FieldChangeHandler = new LocalStagingFieldChangeHandler(ApplyEdits);

            BuildUI();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void BuildUI()
        {
            var separator = new VisualElement();
            separator.AddToClassList(UssStyle.k_HorizontalSeparator);
            Add(separator);

            var title = new Label(L10n.Tr(Constants.PrimaryUploadMetadata));
            title.AddToClassList(UssStyle.k_UploadMetadataTitle);
            Add(title);

            CreateEntries();

            if (m_PageManager.ActivePage == null)
                return;

            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
        }

        void CreateEntries()
        {
            var selection = m_SelectedAssetsData.Selection;

            m_DescriptionEntry = new MultiEditTextEntry(
                Constants.DescriptionText,
                selection,
                inlineEditService: null,
                EditField.Description,
                fieldChangeHandler: m_FieldChangeHandler,
                isEdited: () => IsDescriptionEdited());
            m_DescriptionEntry.ConfigureEditing(EditingMode.Upload);
            Add(m_DescriptionEntry);

            m_StatusEntry = new MultiEditStatusEntry(
                Constants.StatusText,
                selection,
                inlineEditService: null,
                EditField.Status,
                fieldChangeHandler: m_FieldChangeHandler,
                isEdited: () => IsStatusEdited());
            m_StatusEntry.ConfigureEditing(EditingMode.Upload);
            Add(m_StatusEntry);

            m_TagsEntry = new MultiEditTagsEntry(
                Constants.TagsText,
                selection,
                inlineEditService: null,
                fieldChangeHandler: m_FieldChangeHandler,
                isEdited: () => AreTagsEdited());
            m_TagsEntry.ConfigureEditing(EditingMode.Upload);
            Add(m_TagsEntry);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;
            UpdateEntries();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
            m_DescriptionEntry?.Dispose();
            m_StatusEntry?.Dispose();
            m_TagsEntry?.Dispose();
        }

        void OnSelectedAssetChanged(IPage page, IEnumerable<AssetIdentifier> identifiers)
        {
            m_DescriptionEntry?.SavePendingEdits();
            m_StatusEntry?.SavePendingEdits();
            m_TagsEntry?.SavePendingEdits();

            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(identifiers);
            UpdateEntries();
        }

        void UpdateEntries()
        {
            var selection = m_SelectedAssetsData.Selection;
            m_DescriptionEntry?.UpdateSelection(selection);
            m_StatusEntry?.UpdateSelection(selection);
            m_TagsEntry?.UpdateSelection(selection);
        }

        ImportedAssetInfo GetImportedAssetInfo(string assetId)
        {
            return m_AssetDataManager?.GetImportedAssetInfo(assetId);
        }

        bool IsDescriptionEdited() => UploadFieldUtils.IsFieldEdited(
            m_SelectedAssetsData.Selection,
            GetImportedAssetInfo,
            asset => asset.Description,
            info => info?.AssetData?.Description);

        bool IsStatusEdited() => UploadFieldUtils.IsFieldEdited(
            m_SelectedAssetsData.Selection,
            GetImportedAssetInfo,
            asset => asset.Status,
            info => info?.AssetData?.Status);

        bool AreTagsEdited() => UploadFieldUtils.IsFieldEdited(
            m_SelectedAssetsData.Selection,
            GetImportedAssetInfo,
            asset => asset.Tags ?? Enumerable.Empty<string>(),
            info => info?.AssetData?.Tags ?? Enumerable.Empty<string>(),
            areEqual: (x, y) => x.SequenceEqual(y));

        void ApplyEdits(IEnumerable<AssetFieldEdit> edits)
        {
            foreach (var edit in edits)
            {
                var assetData = m_AssetDataManager.GetAssetData(edit.AssetIdentifier) as UploadAssetData;
                if (assetData == null)
                    continue;

                switch (edit.Field)
                {
                    case EditField.Description:
                        assetData.SetDescription(edit.EditValue as string);
                        break;
                    case EditField.Status:
                        assetData.SetStatus(edit.EditValue as string);
                        break;
                    case EditField.Tags:
                        if (edit.EditValue is IEnumerable<string> tags)
                            assetData.SetTags(tags);
                        break;
                }
            }

            var uploadPage = m_PageManager.ActivePage as UploadPage;
            uploadPage?.OnAssetSelectionEdited(edits);

            UpdateEntries();
        }
    }
}
