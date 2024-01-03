using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.Editor
{
    internal class AssetDetailsPage : VisualElement
    {
        private string k_LoadingText = L10n.Tr("Loading...");
        private ScrollView m_ScrollView;
        private AssetPreview m_Thumbnail;
        private VisualElement m_ThumbnailContainer;
        private Label m_AssetName;
        private Label m_Version;
        private Label m_Description;
        private Label m_LastEdited;
        private Label m_UploadDate;
        private Label m_Filesize;
        private Label m_AssetType;
        private Label m_Status;
        private Label m_TotalFiles;
        private VisualElement m_TagsContainer;
        private Button m_ImportButton;
        private Button m_RemoveImportButton;
        private Button m_PreviewButton;
        private Button m_CloseButton;
        private VisualElement m_Footer;
        private Label m_FilesLoadingLabel;
        private ListView m_FilesListView;
        private Foldout m_FilesFoldout;
        private VisualElement m_OwnedAssetIcon;
        private ImportProgressBar m_ImportProgressBar;

        private List<IAssetDataFile> m_Files;
        private List<string> m_FilesList;

        private const string k_DarkFooterClass = "footer-dark";
        private const string k_LightFooterClass = "footer-light";

        private readonly IAssetImporter m_AssetImporter;
        private readonly IStateManager m_StateManager;
        private readonly IPageManager m_PageManager;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IEditorGUIUtilityProxy m_EditorGUIUtilityProxy;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        public AssetDetailsPage(IAssetImporter assetImporter,
            IStateManager stateManager,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IThumbnailDownloader thumbnailDownloader,
            IIconFactory iconFactory,
            IEditorGUIUtilityProxy editorGUIUtilityProxy,
            IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetImporter = assetImporter;
            m_StateManager = stateManager;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_EditorGUIUtilityProxy = editorGUIUtilityProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;

            var treeAsset = UIElementsUtils.LoadUXML("DetailsPageContainer");
            treeAsset.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>("details-page-scrollview");
            m_AssetName = this.Q<Label>("name");
            m_Version = this.Q<Label>("version");
            m_Description = this.Q<Label>("description");
            m_ThumbnailContainer = this.Q<VisualElement>("details-page-thumbnail-container");
            m_Filesize = this.Q<Label>("filesize");
            m_AssetType = this.Q<Label>("assetType");
            m_Status = this.Q<Label>("status");
            m_TotalFiles = this.Q<Label>("total-files");
            m_FilesLoadingLabel = this.Q<Label>("details-files-loading-label");
            m_FilesListView = this.Q<ListView>("details-files-listview");
            m_FilesFoldout = this.Q<Foldout>("details-files-foldout");
            m_LastEdited = this.Q<Label>("last-edited");
            m_UploadDate = this.Q<Label>("upload-date");
            m_CloseButton = this.Q<Button>("closeButton");
            m_ImportButton = this.Q<Button>("importButton");
            m_RemoveImportButton = this.Q<Button>("removeImportButton");
            m_TagsContainer = this.Q<VisualElement>("details-page-tags-container");
            m_Footer = this.Q<VisualElement>("footer");
            m_OwnedAssetIcon = this.Q<VisualElement>("thumbnail-owned-icon");
            m_OwnedAssetIcon.BringToFront();

            m_Thumbnail = new AssetPreview(iconFactory) { name = "details-page-asset-preview" };
            m_Thumbnail.AddToClassList("image-container");
            m_ThumbnailContainer.Add(m_Thumbnail);
            
            var footerContainer = this.Q<VisualElement>("footer-container");
            m_ImportProgressBar = new ImportProgressBar(m_PageManager, m_AssetImporter, true);
            footerContainer.Add(m_ImportProgressBar);

            m_FilesLoadingLabel.text = k_LoadingText;
            m_ScrollView.viewDataKey = "details-page-scrollview";
            m_FilesFoldout.viewDataKey = "details-files-foldout";
            m_FilesListView.viewDataKey = "details-files-listview";

            m_FilesFoldout.RegisterValueChangedCallback(e =>
            {
                m_StateManager.detailsFileFoldoutValue = m_FilesFoldout.value;
                RefreshFoldoutStyleBasedOnExpansionStatus();

                // Bug in UI Toolkit where the scrollview does not update its size when the foldout is expanded/collapsed.
                schedule.Execute(e => { this.Q<ScrollView>("details-page-scrollview").verticalScrollerVisibility = ScrollerVisibility.Auto; }).StartingIn(25);
            });

            m_FilesFoldout.value = m_StateManager.detailsFileFoldoutValue;
            RefreshFoldoutStyleBasedOnExpansionStatus();

            // Setup the import button logic here. Since we have the assetData information both in this object's properties
            // and in AssetManagerWindow.m_CurrentAssetData, we should not need any special method with arguments
            m_ImportButton.clicked += ImportAssetAsync;
            m_RemoveImportButton.clicked += RemoveFromProject;
            m_CloseButton.clicked += () => m_PageManager.activePage.selectedAssetId = null;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);


            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            Refresh(m_PageManager.activePage);
        }

        internal void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += Refresh;
            m_PageManager.onSelectedAssetChanged += onSelectedAssetChanged;
            m_AssetImporter.onImportProgress += OnImportProgress;
            m_AssetImporter.onImportFinalized += OnImportFinalized;
            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged += OnAssetDataChanged;
        }

        internal void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= Refresh;
            m_PageManager.onSelectedAssetChanged -= onSelectedAssetChanged;
            m_AssetImporter.onImportProgress -= OnImportProgress;
            m_AssetImporter.onImportFinalized -= OnImportFinalized;
            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged -= OnAssetDataChanged;
        }

        private void onSelectedAssetChanged(IPage page, AssetIdentifier _)
        {
            Refresh(page);
        }

        private void OnImportProgress(ImportOperation importOperation)
        {
            if (!importOperation.assetId.Equals(m_PageManager.activePage.selectedAssetId))
                return;
            RefreshImportVisibility(importOperation, m_AssetDataManager.IsInProject(m_PageManager.activePage.selectedAssetId));
            m_ImportProgressBar.Refresh(importOperation);
        }

        private void OnImportFinalized(ImportOperation importOperation)
        {
            if (!importOperation.assetId.Equals(m_PageManager.activePage.selectedAssetId))
                return;
            Refresh(m_PageManager.activePage);
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.updated).Concat(args.removed).Any(a => a.Equals(m_PageManager.activePage.selectedAssetId)))
                return;
            Refresh(m_PageManager.activePage);
        }

        private void ImportAssetAsync()
        {
            m_ImportButton.SetEnabled(false);
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            try
            {
                m_AssetImporter.StartImportAsync(assetData, ImportAction.ImportButton);
            }
            catch (Exception e)
            {
                RefreshImportVisibility(m_AssetImporter.GetImportOperation(assetData.id), m_AssetDataManager.IsInProject(assetData.id));
                throw e;
            }
        }

        void RemoveFromProject()
        {
            m_RemoveImportButton.SetEnabled(false);
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            try
            {
                m_AssetImporter.RemoveImport(assetData, true);
            }
            catch (Exception e)
            {
                RefreshImportVisibility(m_AssetImporter.GetImportOperation(assetData.id), m_AssetDataManager.IsInProject(assetData.id));
                throw e;
            }
        }

        private void RefreshImportVisibility(ImportOperation importOperation, bool isInProject)
        {
            if (m_PageManager.activePage.selectedAssetId == null) 
                return;
            
            var isImporting = importOperation?.status == OperationStatus.InProgress;
            m_ImportButton.SetEnabled(!isImporting && m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId).files.Any());
            m_ImportButton.tooltip = !m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId).files.Any() ? L10n.Tr(Constants.ImportButtonDisabledToolTip) : string.Empty;
            if (isImporting)
                m_ImportButton.text = $"{Constants.ImportingText} ({importOperation.progress * 100:0.#}%)";
            else
                m_ImportButton.text = isInProject ? Constants.ReImportText : Constants.ImportText;

            var removeImportEnabled = m_AssetDataManager.IsInProject(m_PageManager.activePage.selectedAssetId);
            m_RemoveImportButton.SetEnabled(removeImportEnabled);
            m_RemoveImportButton.tooltip = removeImportEnabled ? string.Empty : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
            
            m_OwnedAssetIcon.style.display = isInProject ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void Refresh(IPage page)
        {
            var assetData = m_AssetDataManager.GetAssetData(page?.selectedAssetId);
            if (assetData == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            UIElementsUtils.Show(this);
            RefreshUI(assetData);
            RefreshFilesInformation(assetData);
        }

        void RefreshUI(IAssetData assetData)
        {
            var lastEdited = assetData.updated.ToString("d", CultureInfo.CurrentCulture);
            var uploadDate = assetData.created.ToString("d", CultureInfo.CurrentCulture);

            m_AssetName.text = assetData.name;
            m_Description.text = assetData.description;
            m_AssetType.text = assetData.assetType.DisplayValue();
            m_Status.text = assetData.status;
            m_LastEdited.text = lastEdited;
            m_UploadDate.text = uploadDate;
            m_Version.text = assetData.id.version;
            m_TagsContainer.Clear();

            foreach (var tag in assetData.tags)
            {
                var tagPill = new TagPill(tag);
                tagPill.TagPillClickAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    m_PageManager.activePage.AddSearchFilter(words, true);
                };
                m_TagsContainer.Add(tagPill);
            }

            //TODO: Light and dark skin application should be done once on the window level and the rest of the UIElements shouldn't care about it anymore
            m_Footer.AddToClassList(EditorGUIUtility.isProSkin ? k_DarkFooterClass : k_LightFooterClass);
            m_Footer.RemoveFromClassList(EditorGUIUtility.isProSkin ? k_LightFooterClass : k_DarkFooterClass);

            RefreshImportVisibility(m_AssetImporter.GetImportOperation(assetData.id), m_AssetDataManager.IsInProject(assetData.id));
            m_ImportProgressBar.Refresh(m_AssetImporter.GetImportOperation(assetData.id));
            m_Thumbnail.SetAssetType(assetData.assetType, true);
            m_ThumbnailDownloader.DownloadThumbnail(assetData, (identifier, texture2D) =>
            {
                if (identifier.Equals(assetData.id))
                    m_Thumbnail.SetThumbnail(texture2D);
            });

            RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void RefreshFoldoutStyleBasedOnExpansionStatus()
        {
            if (m_FilesFoldout.value)
            {
                m_FilesFoldout.RemoveFromClassList("details-files-foldout-collapsed");
                m_FilesFoldout.AddToClassList("details-files-foldout-expanded");
            }
            else
            {
                m_FilesFoldout.RemoveFromClassList("details-files-foldout-expanded");
                m_FilesFoldout.AddToClassList("details-files-foldout-collapsed");
            }
        }

        private void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.removed).Concat(args.updated).Any(a => a.Equals(m_PageManager.activePage.selectedAssetId)))
                return;
            
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            RefreshFilesInformation(assetData);
            RefreshImportVisibility(m_AssetImporter.GetImportOperation(assetData.id), m_AssetDataManager.IsInProject(assetData.id));
        }

        private void RefreshFilesInformation(IAssetData assetData)
        {
            if (assetData == null)
                return;

            var assetFileSize = assetData.files.Sum(i => i.fileSize);
            m_Filesize.text = Utilities.BytesToReadableString(assetFileSize);
            m_TotalFiles.text = assetData.files.Count.ToString();
            
            UIElementsUtils.Hide(m_FilesLoadingLabel);
            UIElementsUtils.Show(m_FilesListView);
            m_FilesList = new List<string>();
            foreach (var file in assetData.files)
                m_FilesList.Add(file.path);
            m_FilesListView.makeItem = () => new DetailsPageFileItem(m_AssetDataManager, m_PageManager, m_AssetImporter, m_EditorGUIUtilityProxy, m_AssetDatabaseProxy);
            m_FilesListView.bindItem = (element, i) => { ((DetailsPageFileItem)element).Refresh(m_FilesList[i]); };
            m_FilesListView.itemsSource = m_FilesList;
        }
    }
}