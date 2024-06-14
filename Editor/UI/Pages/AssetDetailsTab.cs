using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
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

        public override void OnSelection(IAssetData assetData, bool isLoading)
        {
            m_AssetPreview.ClearPreview();
        }

        public override void RefreshUI(IAssetData assetData, bool isLoading = false)
        {
            // Remove asset preview from hierarchy to avoid it being destroyed when clearing the container
            m_AssetPreview.RemoveFromHierarchy();

            m_EntriesContainer.Clear();

            AddText(m_EntriesContainer, null, assetData.Description);

            m_AssetPreview.SetAssetType(assetData.PrimaryExtension);
            UpdatePreviewStatus(assetData.PreviewStatus);
            m_EntriesContainer.Add(m_AssetPreview);

            AddTags(m_EntriesContainer, Constants.TagsText, assetData.Tags);
            AddProject(m_EntriesContainer, Constants.ProjectText, assetData.Identifier.ProjectId, "entry-project");
            AddText(m_EntriesContainer, Constants.StatusText, assetData.Status);
            AddText(m_EntriesContainer, Constants.AssetTypeText, assetData.AssetType.DisplayValue());
            AddText(m_EntriesContainer, Constants.TotalFilesText, "-", k_FileCountName);
            AddText(m_EntriesContainer, Constants.FilesSizeText, "-", k_FileSizeName);
            AddText(m_EntriesContainer, Constants.CreatedDateText, assetData.Created?.ToLocalTime().ToString("G"));
            AddUser(m_EntriesContainer, Constants.CreatedByText, assetData.CreatedBy, typeof(CreatedByFilter));
            AddText(m_EntriesContainer, Constants.ModifiedDateText, assetData.Updated?.ToLocalTime().ToString("G"));
            AddUser(m_EntriesContainer, Constants.ModifiedByText, assetData.UpdatedBy, typeof(UpdatedByFilter));
            AddText(m_EntriesContainer, Constants.AssetIdText, assetData.Identifier.AssetId);

            if (isLoading)
            {
                AddLoadingText(m_EntriesContainer);
            }
        }

        public override void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress)
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
    }
}
