using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
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

    class AssetDetailsTab : AssetTab
    {
        const string k_FileSizeName = "file-size";
        const string k_FileCountName = "file-count";

        readonly AssetPreview m_AssetPreview;
        readonly VisualElement m_EntriesContainer;

        BaseAssetData m_AssetData;

        public override AssetDetailsPageTabs.TabType Type => AssetDetailsPageTabs.TabType.Details;
        public override bool IsFooterVisible => true;
        public override bool EnabledWhenDisconnected => true;
        public override VisualElement Root { get; }

        public AssetDetailsTab(VisualElement visualElement)
        {
            var root = visualElement.Q("details-page-content-container");
            Root = root;

            m_EntriesContainer = new VisualElement();
            m_EntriesContainer.AddToClassList(UssStyle.DetailsPageEntriesContainer);
            root.Add(m_EntriesContainer);
            m_EntriesContainer.SendToBack();

            m_AssetPreview = new AssetPreview {name = "details-page-asset-preview"};
            m_AssetPreview.AddToClassList(UssStyle.DetailsPageThumbnailContainer);
            m_AssetPreview.AddToClassList(UssStyle.ImageContainer);
        }

        public override void OnSelection(BaseAssetData assetData)
        {
            m_AssetPreview.ClearPreview();
        }

        public override void RefreshUI(BaseAssetData assetData, bool isLoading = false)
        {
            SetEventHandlers(assetData);

            // Remove asset preview from hierarchy to avoid it being destroyed when clearing the container
            m_AssetPreview.RemoveFromHierarchy();

            m_EntriesContainer.Clear();

            AddText(m_EntriesContainer, null, assetData.Description);

            m_AssetPreview.SetAssetType(assetData.PrimaryExtension);
            UpdatePreviewStatus(AssetDataStatus.GetIStatusFromAssetDataAttributes(assetData.AssetDataAttributeCollection));

            m_EntriesContainer.Add(m_AssetPreview);
            SetPreviewImage(assetData.Thumbnail);

            AddTagChips(m_EntriesContainer, Constants.TagsText, assetData.Tags);
            AddProject(m_EntriesContainer, Constants.ProjectText, assetData.Identifier.ProjectId, "entry-project");
            AddText(m_EntriesContainer, Constants.StatusText, assetData.Status);
            AddText(m_EntriesContainer, Constants.AssetTypeText, assetData.AssetType.DisplayValue());
            AddText(m_EntriesContainer, Constants.TotalFilesText, "-", k_FileCountName);
            AddText(m_EntriesContainer, Constants.FilesSizeText, "-", k_FileSizeName);
            AddText(m_EntriesContainer, Constants.UploadDateText, Utilities.DatetimeToString(assetData.Created));
            AddUser(m_EntriesContainer, Constants.CreatedByText, assetData.CreatedBy, typeof(CreatedByFilter));
            AddText(m_EntriesContainer, Constants.LastModifiedText, Utilities.DatetimeToString(assetData.Updated));
            AddUser(m_EntriesContainer, Constants.LastEditByText, assetData.UpdatedBy, typeof(UpdatedByFilter));

            // Temporary solution to display targeted assets during re-upload. Very handy to understand which assets the system is re-uploading to.
            var identifier = assetData.Identifier;
            if (assetData is UploadAssetData uploadAssetData)
            {
                identifier = uploadAssetData.TargetAssetIdentifier ?? identifier;
            }

            AddAssetIdentifier(m_EntriesContainer, Constants.AssetIdText, identifier);

            DisplayMetadata(assetData);

            if (isLoading)
            {
                AddLoadingText(m_EntriesContainer);
            }
        }

        void SetEventHandlers(BaseAssetData assetData)
        {
            if(m_AssetData != null && m_AssetData.Identifier.Equals(assetData.Identifier))
                return;

            // Unsubscribe from previous asset data events
            if (m_AssetData != null)
                m_AssetData.AssetDataChanged -= OnAssetDataChanged;

            m_AssetData = assetData;
            assetData.AssetDataChanged += OnAssetDataChanged;
        }

        void OnAssetDataChanged(BaseAssetData obj, AssetDataEventType eventType)
        {
            switch (eventType)
            {
                case AssetDataEventType.AssetDataAttributesChanged:
                    UpdatePreviewStatus(AssetDataStatus.GetIStatusFromAssetDataAttributes(obj.AssetDataAttributeCollection));
                    break;
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
                        var textMetadata = (TextMetadata)metadata;
                        AddText(m_EntriesContainer, textMetadata.Name, textMetadata.Value);
                        break;
                    }
                    case MetadataFieldType.Boolean:
                    {
                        var booleanMetadata = (BooleanMetadata)metadata;
                        AddToggle(m_EntriesContainer, booleanMetadata.Name, booleanMetadata.Value);
                        break;
                    }
                    case MetadataFieldType.Number:
                    {
                        var numberMetadata = (NumberMetadata)metadata;
                        AddText(m_EntriesContainer, numberMetadata.Name,
                            numberMetadata.Value.ToString(CultureInfo.CurrentCulture));
                        break;
                    }
                    case MetadataFieldType.Timestamp:
                    {
                        var timestampMetadata = (TimestampMetadata)metadata;
                        AddText(m_EntriesContainer, timestampMetadata.Name,
                            Utilities.DatetimeToString(timestampMetadata.Value.DateTime));
                        break;
                    }
                    case MetadataFieldType.Url:
                    {
                        var urlMetadata = (UrlMetadata)metadata;
                        AddText(m_EntriesContainer, urlMetadata.Name,
                            urlMetadata.Value.Uri == null ? string.Empty : urlMetadata.Value.Uri.ToString());
                        break;
                    }
                    case MetadataFieldType.User:
                    {
                        var userMetadata = (UserMetadata)metadata;
                        AddUser(m_EntriesContainer, metadata.Name, userMetadata.Value, null);
                        break;
                    }
                    case MetadataFieldType.SingleSelection:
                    {
                        var singleSelectionMetadata = (SingleSelectionMetadata)metadata;
                        AddSelectionChips(m_EntriesContainer, metadata.Name, new List<string> {singleSelectionMetadata.Value});
                        break;
                    }
                    case MetadataFieldType.MultiSelection:
                    {
                        var multiSelectionMetadata = (MultiSelectionMetadata)metadata;
                        AddSelectionChips(m_EntriesContainer, metadata.Name, multiSelectionMetadata.Value);
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Unexpected metadata field type was encountered.");
                }
            }
        }

        public override void RefreshButtons(UIEnabledStates enabled, BaseAssetData assetData, BaseOperation operationInProgress)
        {
            m_EntriesContainer.Q("entry-project")?.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));
        }

        public void UpdatePreviewStatus(IEnumerable<AssetPreview.IStatus> status)
        {
            m_AssetPreview.SetStatuses(status);
        }

        public void SetPreviewImage(Texture2D texture)
        {
            m_AssetPreview.SetThumbnail(texture);
        }

        public void SetFileCount(string fileCount)
        {
            m_EntriesContainer.Q<DetailsPageEntry>(k_FileCountName)?.SetText(fileCount);
        }

        public void SetFileSize(string fileSize)
        {
            m_EntriesContainer.Q<DetailsPageEntry>(k_FileSizeName)?.SetText(fileSize);
        }

        public void SetPrimaryExtension(string primaryExtension)
        {
            m_AssetPreview.SetAssetType(primaryExtension);
        }
    }
}
