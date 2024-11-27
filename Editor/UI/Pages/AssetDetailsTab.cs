using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
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
            // Remove asset preview from hierarchy to avoid it being destroyed when clearing the container
            m_AssetPreview.RemoveFromHierarchy();

            m_EntriesContainer.Clear();

            AddText(m_EntriesContainer, null, assetData.Description);

            m_AssetPreview.SetAssetType(assetData.PrimaryExtension);
            UpdatePreviewStatus(AssetDataStatus.GetIStatusFromAssetDataStatusType(assetData.PreviewStatus));

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
            AddAssetIdentifier(m_EntriesContainer, Constants.AssetIdText, assetData.Identifier);

            DisplayMetadata(assetData);

            if (isLoading)
            {
                AddLoadingText(m_EntriesContainer);
            }
        }

        void DisplayMetadata(BaseAssetData assetData)
        {
            foreach (var metadata in assetData.Metadata)
            {
                if (metadata is BooleanMetadata booleanMetadata)
                {
                    AddToggle(m_EntriesContainer, metadata.Name, booleanMetadata.GetValue() as bool? ?? bool.Parse(booleanMetadata.GetValue()?.ToString() ?? false.ToString()));
                }
                else if (metadata is UserMetadata userMetadata)
                {
                    AddUser(m_EntriesContainer, metadata.Name,
                        userMetadata.Value, null);
                }
                else if (metadata is SingleSelectionMetadata singleSelectionMetadata)
                {
                    AddSelectionChips(m_EntriesContainer, metadata.Name,
                        new List<string> {singleSelectionMetadata.Value});
                }
                else if (metadata is MultiSelectionMetadata multiSelectionMetadata)
                {
                    AddSelectionChips(m_EntriesContainer, metadata.Name,
                        multiSelectionMetadata.Value);
                }
                else
                {
                    AddText(m_EntriesContainer, metadata.Name, metadata.ToString());
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
