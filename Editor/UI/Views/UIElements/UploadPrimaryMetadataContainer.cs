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
        readonly List<AssetFieldContainer> m_FieldContainers = new();

        public UploadPrimaryMetadataContainer(IPageManager pageManager, IAssetDataManager assetDataManager)
        {
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;

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

            CreateFieldContainers();

            if (m_PageManager.ActivePage == null)
                return;

            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
        }

        void CreateFieldContainers()
        {
            var uploadAssetSelection = m_SelectedAssetsData.Selection.Cast<UploadAssetData>();

            var descriptionContainer = new DescriptionFieldContainer(uploadAssetSelection, GetImportedAssetInfo, ApplyEdits);
            m_FieldContainers.Add(descriptionContainer);
            Add(descriptionContainer.Root);

            var statusContainer = new StatusFieldContainer(uploadAssetSelection, GetImportedAssetInfo, ApplyEdits);
            m_FieldContainers.Add(statusContainer);
            Add(statusContainer.Root);

            var tagsContainer = new TagsFieldContainer(uploadAssetSelection, GetImportedAssetInfo, ApplyEdits);
            m_FieldContainers.Add(tagsContainer);
            Add(tagsContainer.Root);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;
            EnableFields();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DisableFields();
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
        }

        void OnSelectedAssetChanged(IPage page, IEnumerable<AssetIdentifier> identifiers)
        {
            foreach (var fieldContainer in m_FieldContainers)
            {
                fieldContainer.SavePendingChanges();
            }

            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(identifiers);
            UpdateFields();
        }

        void EnableFields()
        {
            UpdateFields();
            foreach (var fieldContainer in m_FieldContainers)
                fieldContainer.Enable();
        }

        void DisableFields()
        {
            foreach (var fieldContainer in m_FieldContainers)
                fieldContainer.Disable();
        }

        void UpdateFields()
        {
            foreach (var fieldContainer in m_FieldContainers)
                fieldContainer.UpdateField(m_SelectedAssetsData.Selection);
        }

        ImportedAssetInfo GetImportedAssetInfo(string assetId)
        {
            return m_AssetDataManager?.GetImportedAssetInfo(assetId);
        }

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

            UpdateFields();
        }
    }
}

