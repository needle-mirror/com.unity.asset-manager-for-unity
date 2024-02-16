using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.Editor
{
    internal class AssetDetailsPage : VisualElement
    {
        private string k_LoadingText = L10n.Tr("Loading...");
        private ScrollView m_ScrollView;
        private AssetPreview m_AssetPreview;
        private VisualElement m_ThumbnailContainer;
        private Label m_AssetName;
        private Label m_AssetId;
        private Image m_AssetDashboardLink;
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
        private Button m_ShowInProjectBrowserButton;
        private Button m_RemoveImportButton;
        private Button m_PreviewButton;
        private Button m_CloseButton;
        private VisualElement m_Footer;
        private Label m_FilesLoadingLabel;
        private ListView m_FilesListView;
        private Foldout m_FilesFoldout;
        private ImportProgressBar m_ImportProgressBar;
        private VisualElement m_NoFilesWarningBox;
        private VisualElement m_ProjectContainer;

        private List<string> m_FilesList;

        private readonly IAssetImporter m_AssetImporter;
        private readonly IStateManager m_StateManager;
        private readonly IPageManager m_PageManager;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IEditorGUIUtilityProxy m_EditorGUIUtilityProxy;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        private readonly ILinksProxy m_LinksProxy;
        private readonly IProjectIconDownloader m_ProjectIconDownloader;

        public AssetDetailsPage(IAssetImporter assetImporter,
            IStateManager stateManager,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IThumbnailDownloader thumbnailDownloader,
            IEditorGUIUtilityProxy editorGUIUtilityProxy,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IProjectIconDownloader projectIconDownloader)
        {
            m_AssetImporter = assetImporter;
            m_StateManager = stateManager;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_EditorGUIUtilityProxy = editorGUIUtilityProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_LinksProxy = linksProxy;
            m_ProjectIconDownloader = projectIconDownloader;

            var treeAsset = UIElementsUtils.LoadUXML("DetailsPageContainer");
            treeAsset.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>("details-page-scrollview");
            m_AssetName = this.Q<Label>("asset-name");
            m_AssetId = this.Q<Label>("id");
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
            m_ShowInProjectBrowserButton = this.Q<Button>("showInProjectBrowserButton");
            m_RemoveImportButton = this.Q<Button>("removeImportButton");
            m_TagsContainer = this.Q<VisualElement>("details-page-tags-container");
            m_Footer = this.Q<VisualElement>("footer");
            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr("No files were found in this asset.");
            m_ProjectContainer = this.Q<VisualElement>("details-page-project-pill-container");

            m_AssetDashboardLink = this.Q<Image>("asset-dashboard-link");
            m_AssetDashboardLink.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ExternalLinkName]);
            m_AssetDashboardLink.tooltip = L10n.Tr("Open asset in the dashboard");
            m_AssetDashboardLink.RegisterCallback<ClickEvent>(e =>
            {
                var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
                m_LinksProxy.OpenAssetManagerDashboard(assetData.identifier.projectId, assetData.identifier.assetId);
            });

            m_AssetPreview = new AssetPreview { name = "details-page-asset-preview" };
            m_AssetPreview.AddToClassList("image-container");
            m_AssetPreview.AddToClassList("details-page-asset-preview-thumbnail");
            m_ThumbnailContainer.Add(m_AssetPreview);

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
            m_ShowInProjectBrowserButton.clicked += m_ShowInProjectBrowser;
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
            if (m_PageManager.activePage != page)
                return;
            
            Refresh(page);
        }

        private void OnImportProgress(ImportOperation importOperation)
        {
            if (!importOperation.assetId.Equals(m_PageManager.activePage.selectedAssetId))
                return;
            
            RefreshImportVisibility(importOperation.assetData, importOperation);
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
            catch (Exception)
            {
                RefreshImportVisibility(assetData, m_AssetImporter.GetImportOperation(assetData.identifier));
                throw;
            }
        }

        void m_ShowInProjectBrowser()
        {
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            m_AssetImporter.ShowInProject(assetData);
        }

        void RemoveFromProject()
        {
            m_RemoveImportButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            try
            {
                m_AssetImporter.RemoveImport(assetData, true);
            }
            catch (Exception)
            {
                RefreshImportVisibility(assetData, m_AssetImporter.GetImportOperation(assetData.identifier));
                throw;
            }
        }

        private async void RefreshImportVisibility(IAssetData assetData, ImportOperation importOperation)
        {
            ClearUI(assetData);
            
            var isInProject = m_AssetDataManager.IsInProject(assetData.identifier);
            
            _ = m_AssetDataManager.GetImportedStatus(assetData.identifier, async (identifier, importedStatus) =>
            {
                if (!identifier.Equals(assetData.identifier))
                    return;
                
                RefreshFilesInformation(assetData, importedStatus);
            
                m_AssetPreview.SetImportStatusIcon(importedStatus);
            });

            if (m_PageManager.activePage.selectedAssetId == null) 
                return;
            
            var isImporting = importOperation?.status == OperationStatus.InProgress;
            var isEnabled = !isImporting;

            m_ImportButton.SetEnabled(isEnabled);
            m_ImportButton.tooltip = !isEnabled ? L10n.Tr(Constants.ImportButtonDisabledToolTip) : string.Empty;
            if (isImporting)
            {
                m_ImportButton.text = $"{Constants.ImportingText} ({importOperation.progress * 100:0.#}%)";
            }
            else
            {
                m_ImportButton.text = isInProject ? Constants.ReimportText : Constants.ImportText;
                if (isInProject)
                {
                    m_ImportButton.tooltip = L10n.Tr("Restore the asset to it's original state");
                }
            }
            
            m_ShowInProjectBrowserButton.SetEnabled(isInProject);
            m_RemoveImportButton.SetEnabled(isInProject);
            m_RemoveImportButton.tooltip = isInProject ? string.Empty : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
        }

        void Refresh(IPage page)
        {
            var assetData = m_AssetDataManager.GetAssetData(page?.selectedAssetId);
            if (assetData == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            UIElementsUtils.Show(this);
            RefreshUI(assetData);
        }

        void ClearUI(IAssetData assetData)
        {
            m_AssetName.text = assetData.name;
            m_AssetId.text = assetData.identifier.assetId;
            m_AssetType.text = assetData.assetType.DisplayValue();
            m_Status.text = assetData.status;
            m_Version.text = assetData.identifier.version;

            m_FilesListView.itemsSource = null;
            UIElementsUtils.Hide(m_NoFilesWarningBox);
            UIElementsUtils.Show(m_FilesFoldout);
            UIElementsUtils.Show(m_FilesLoadingLabel);

            m_Filesize.text = m_TotalFiles.text = "...";
            m_LastEdited.text = m_UploadDate.text = "...";
            m_Description.text = "...";
            
            m_AssetPreview.SetImportStatusIcon(ImportedStatus.None);
        }

        async void RefreshUI(IAssetData assetData)
        {
            ClearUI(assetData);
            
            m_TagsContainer.Clear();
            m_ProjectContainer.Clear();

            var projectInfo = m_ProjectOrganizationProvider.organization?.projectInfos.FirstOrDefault(p => p.id == assetData.identifier.projectId);
            if (projectInfo != null)
            {
                var projectPill = new ProjectPill(projectInfo);
                projectPill.ProjectPillClickAction += pi =>
                {
                    m_ProjectOrganizationProvider.selectedProject = pi;
                };
                m_ProjectIconDownloader.DownloadIcon(projectInfo.id, (projectId, icon) =>
                {
                    if (projectId == projectInfo.id)
                    {
                        projectPill.SetIcon(icon);
                    }
                });
                m_ProjectContainer.Add(projectPill);

                var projectDashboardButton = new Image();
                projectDashboardButton.AddToClassList("details-page-project-dashboard-button");
                projectDashboardButton.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ExternalLinkName]);
                projectDashboardButton.tooltip = L10n.Tr("Open project in the dashboard");
                projectDashboardButton.RegisterCallback<ClickEvent>(e => m_LinksProxy.OpenAssetManagerDashboard(projectInfo.id));
                m_ProjectContainer.Add(projectDashboardButton);
            }

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

            var importOperation = m_AssetImporter.GetImportOperation(assetData.identifier);
            RefreshImportVisibility(assetData, importOperation);
            m_ImportProgressBar.Refresh(importOperation);
            
            _ = assetData.GetPrimaryExtension((identifier, extension) =>
            {
                if (!identifier.Equals(assetData.identifier))
                    return;
                
                m_AssetPreview.SetAssetType(extension, true);
            });
            
            m_ThumbnailDownloader.DownloadThumbnail(assetData, (identifier, texture2D) =>
            {
                if (identifier.Equals(assetData.identifier))
                {
                    m_AssetPreview.SetThumbnail(texture2D);
                }
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

        private async void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.removed).Concat(args.updated).Any(a => a.Equals(m_PageManager.activePage.selectedAssetId)))
                return;
            
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            
            RefreshImportVisibility(assetData, m_AssetImporter.GetImportOperation(assetData.identifier));
        }

        async void RefreshFilesInformation(IAssetData assetData, ImportedStatus importedStatus)
        {
            if (assetData == null)
                return;
            
            if (importedStatus == ImportedStatus.None)
            {
                await assetData.SyncWithCloudAsync();
            }

            m_FilesList = new List<string>();
            var files = assetData.cachedFiles.ToList();
            foreach (var file in files.OrderBy(f => f.path))
            {
                m_FilesList.Add(file.path);
            }

            m_UploadDate.text = assetData.updated.ToString("G", CultureInfo.CurrentCulture);
            m_LastEdited.text = assetData.created.ToString("G", CultureInfo.CurrentCulture);
            m_Description.text = assetData.description;


            files = assetData.cachedFiles.ToList();
            
            var assetFileSize = files.Sum(i => i.fileSize);
            m_Filesize.text = Utilities.BytesToReadableString(assetFileSize);
            m_TotalFiles.text = files.Count.ToString();
            
            var hasFiles = files.Any();
            UIElementsUtils.SetDisplay(m_FilesFoldout, hasFiles);
            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
            
            UIElementsUtils.Hide(m_FilesLoadingLabel);
            UIElementsUtils.Show(m_FilesListView);
            
            m_FilesList = new List<string>();
            foreach (var file in files.OrderBy(f => f.path))
            {
                m_FilesList.Add(file.path);
            }

            m_FilesListView.makeItem = () => new DetailsPageFileItem(m_AssetDataManager, m_PageManager, m_AssetImporter, m_EditorGUIUtilityProxy, m_AssetDatabaseProxy);
            m_FilesListView.bindItem = (element, i) => { ((DetailsPageFileItem)element).Refresh(m_FilesList[i], m_FilesList); };
            m_FilesListView.itemsSource = m_FilesList;
            m_FilesListView.fixedItemHeight = 30;
        }
    }
}