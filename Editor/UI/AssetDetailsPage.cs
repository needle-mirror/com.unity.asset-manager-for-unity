using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.Editor
{
    internal class AssetDetailsPage : VisualElement
    {
        readonly string k_LoadingText = L10n.Tr("Loading...");
        readonly AssetPreview m_AssetPreview;
        readonly Label m_AssetName;
        readonly Label m_AssetId;
        readonly Label m_Version;
        readonly Label m_Description;
        readonly Label m_LastEdited;
        readonly Label m_UploadDate;
        readonly Label m_Filesize;
        readonly Label m_AssetType;
        readonly Label m_Status;
        readonly Label m_TotalFiles;
        readonly VisualElement m_TagsContainer;
        readonly VisualElement m_TagSection;
        readonly VisualElement m_ButtonsContainer;
        readonly Button m_ImportButton;
        readonly Button m_ShowInProjectBrowserButton;
        readonly Button m_RemoveImportButton;
        readonly Label m_FilesLoadingLabel;
        readonly ListView m_FilesListView;
        readonly Foldout m_FilesFoldout;
        readonly OperationProgressBar m_OperationProgressBar;
        readonly VisualElement m_NoFilesWarningBox;
        readonly VisualElement m_ProjectContainer;

        List<string> m_FilesList = new ();

        readonly IAssetImporter m_AssetImporter;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IEditorGUIUtilityProxy m_EditorGUIUtilityProxy;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IProjectIconDownloader m_ProjectIconDownloader;

        IAssetData m_SelectedAssetData;

        public AssetDetailsPage(IAssetImporter assetImporter,
            IStateManager stateManager,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IEditorGUIUtilityProxy editorGUIUtilityProxy,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IProjectIconDownloader projectIconDownloader)
        {
            m_AssetImporter = assetImporter;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_EditorGUIUtilityProxy = editorGUIUtilityProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_ProjectIconDownloader = projectIconDownloader;

            var treeAsset = UIElementsUtils.LoadUXML("DetailsPageContainer");
            treeAsset.CloneTree(this);

            var scrollView = this.Q<VisualElement>("details-page-scrollview");
            m_AssetName = this.Q<Label>("asset-name");
            m_AssetId = this.Q<Label>("id");
            m_Version = this.Q<Label>("version");
            m_Description = this.Q<Label>("description");
            var thumbnailContainer = this.Q<VisualElement>("details-page-thumbnail-container");
            m_Filesize = this.Q<Label>("filesize");
            m_AssetType = this.Q<Label>("assetType");
            m_Status = this.Q<Label>("status");
            m_TotalFiles = this.Q<Label>("total-files");
            m_FilesLoadingLabel = this.Q<Label>("details-files-loading-label");
            m_FilesListView = this.Q<ListView>("details-files-listview");
            m_FilesFoldout = this.Q<Foldout>("details-files-foldout");
            m_LastEdited = this.Q<Label>("last-edited");
            m_UploadDate = this.Q<Label>("upload-date");
            var closeButton = this.Q<Button>("closeButton");

            m_ImportButton = this.Q<Button>("importButton");
            m_ImportButton.text = Constants.ImportActionText;

            m_ShowInProjectBrowserButton = this.Q<Button>("showInProjectBrowserButton");
            m_ShowInProjectBrowserButton.text = Constants.ShowInProjectActionText;

            m_RemoveImportButton = this.Q<Button>("removeImportButton");
            m_RemoveImportButton.text = Constants.RemoveFromProjectActionText;

            m_TagSection = this.Q<VisualElement>("details-page-tag-section");
            m_TagsContainer = this.Q<VisualElement>("details-page-tags-container");
            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr("No files were found in this asset.");
            m_ProjectContainer = this.Q<VisualElement>("details-page-project-pill-container");

            var assetDashboardLink = this.Q<Image>("asset-dashboard-link");
            assetDashboardLink.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ExternalLinkName]);
            assetDashboardLink.tooltip = L10n.Tr("Open asset in the dashboard");
            assetDashboardLink.RegisterCallback<ClickEvent>(e =>
            {
                linksProxy.OpenAssetManagerDashboard(m_SelectedAssetData?.identifier);
            });

            m_AssetPreview = new AssetPreview { name = "details-page-asset-preview" };
            m_AssetPreview.AddToClassList("image-container");
            m_AssetPreview.AddToClassList("details-page-asset-preview-thumbnail");
            thumbnailContainer.Add(m_AssetPreview);

            m_ButtonsContainer = this.Q<VisualElement>("footer-container");
            m_OperationProgressBar = new OperationProgressBar(m_PageManager, m_AssetImporter, true);
            m_ButtonsContainer.Add(m_OperationProgressBar);

            m_FilesLoadingLabel.text = k_LoadingText;
            UIElementsUtils.Hide(m_FilesLoadingLabel);
            scrollView.viewDataKey = "details-page-scrollview";
            m_FilesFoldout.viewDataKey = "details-files-foldout";
            m_FilesListView.viewDataKey = "details-files-listview";

            m_FilesFoldout.RegisterValueChangedCallback(e =>
            {
                stateManager.detailsFileFoldoutValue = m_FilesFoldout.value;
                RefreshFoldoutStyleBasedOnExpansionStatus();

                // Bug in UI Toolkit where the scrollview does not update its size when the foldout is expanded/collapsed.
                schedule.Execute(e => { this.Q<ScrollView>("details-page-scrollview").verticalScrollerVisibility = ScrollerVisibility.Auto; }).StartingIn(25);
            });

            m_FilesFoldout.value = stateManager.detailsFileFoldoutValue;
            RefreshFoldoutStyleBasedOnExpansionStatus();

            // Setup the import button logic here. Since we have the assetData information both in this object's properties
            // and in AssetManagerWindow.m_CurrentAssetData, we should not need any special method with arguments
            m_ImportButton.clicked += ImportAssetAsync;
            m_ShowInProjectBrowserButton.clicked += ShowInProjectBrowser;
            m_RemoveImportButton.clicked += RemoveFromProject;
            closeButton.clicked += () => m_PageManager.activePage.selectedAssetId = null;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            // We need to manually refresh once to make sure the UI is updated when the window is opened.

            m_SelectedAssetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage?.selectedAssetId);
            RefreshUI();
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_AssetImporter.onImportProgress += OnImportProgress;
            m_AssetImporter.onImportFinalized += OnImportFinalized;
            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged += OnAssetDataChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_AssetImporter.onImportProgress -= OnImportProgress;
            m_AssetImporter.onImportFinalized -= OnImportFinalized;
            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged -= OnAssetDataChanged;
        }

        public async Task SelectedAsset(AssetIdentifier assetIdentifier)
        {
            var assetData = m_AssetDataManager.GetAssetData(assetIdentifier);
            await SelectAssetDataAsync(assetData);
        }

        private void OnImportProgress(ImportOperation importOperation)
        {
            if (!importOperation.assetId.Equals(m_SelectedAssetData?.identifier))
                return;

            RefreshButtons(m_SelectedAssetData, importOperation);
            m_OperationProgressBar.Refresh(importOperation);

            if (importOperation.Status != OperationStatus.InProgress)
            {
                _ = SelectAssetDataAsync(importOperation.assetData);
            }
        }

        private void OnImportFinalized(ImportOperation importOperation)
        {
            if (!importOperation.assetId.Equals(m_SelectedAssetData?.identifier))
                return;

            RefreshUI();
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.updated).Concat(args.removed).Any(a => a.Equals(m_SelectedAssetData?.identifier)))
                return;

            RefreshUI();
        }

        private void ImportAssetAsync()
        {
            var buttonType = m_ImportButton.text == Constants.ImportingText ?
                DetailsButtonClickedEvent.ButtonType.Import :
                DetailsButtonClickedEvent.ButtonType.Reimport;
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(buttonType));

            m_ImportButton.SetEnabled(false);
            try
            {
                m_AssetImporter.StartImportAsync(m_SelectedAssetData, ImportAction.ImportButton);
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData, m_AssetImporter.GetImportOperation(m_SelectedAssetData?.identifier));
                throw;
            }
        }

        void ShowInProjectBrowser()
        {
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.activePage.selectedAssetId);
            m_AssetImporter.ShowInProject(assetData.identifier);

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Show));
        }

        void RemoveFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            m_RemoveImportButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            try
            {
                m_AssetImporter.RemoveImport(m_SelectedAssetData?.identifier, true);
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData, m_AssetImporter.GetImportOperation(m_SelectedAssetData?.identifier));
                throw;
            }
        }

        void RefreshBaseInformationUI(IAssetData assetData)
        {
            m_AssetName.text = assetData.name;
            m_AssetId.text = assetData.identifier.assetId;
            m_AssetType.text = assetData.assetType.DisplayValue();
            m_Status.text = assetData.status;
            m_Version.text = assetData.identifier.version;

            m_UploadDate.text = assetData.updated?.ToString("G", CultureInfo.CurrentCulture);
            m_LastEdited.text = assetData.created?.ToString("G", CultureInfo.CurrentCulture);
            m_Description.text = assetData.description;
            UIElementsUtils.SetDisplay(m_Description, !string.IsNullOrEmpty(m_Description.text));

            m_AssetPreview.SetAssetType(assetData.primaryExtension, true);
            m_AssetPreview.SetStatus(assetData.previewStatus);

            RefreshProjectPill(assetData.identifier.projectId);
            RefreshTags(assetData.tags);

            var importOperation = m_AssetImporter.GetImportOperation(assetData.identifier);
            RefreshButtons(assetData, importOperation);

            m_OperationProgressBar.Refresh(importOperation);

            RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void RefreshUI()
        {
            if (m_SelectedAssetData == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            UIElementsUtils.Show(this);

            RefreshBaseInformationUI(m_SelectedAssetData);

            _ = m_SelectedAssetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.identifier))
                    return;

                m_AssetPreview.SetThumbnail(texture2D);
            });

            RefreshFilesInformationUI(m_SelectedAssetData.sourceFiles);
        }

        async Task SelectAssetDataAsync(IAssetData assetData)
        {
            m_SelectedAssetData = assetData;

            if (m_SelectedAssetData == null)
                return;

            RefreshUI();

            var isInProject = m_AssetDataManager.IsInProject(m_SelectedAssetData.identifier); // TODO remove the notion of InProject
            var tasks = new List<Task>();

            if (!isInProject)
            {
                m_FilesListView.itemsSource = null;
                UIElementsUtils.Hide(m_NoFilesWarningBox);
                UIElementsUtils.Show(m_FilesLoadingLabel);
                UIElementsUtils.Show(m_FilesFoldout);

                m_TotalFiles.text = "-";
                m_Filesize.text = "-";

                tasks.Add(assetData.SyncWithCloudAsync(identifier =>
                {
                    if (!identifier.Equals(m_SelectedAssetData?.identifier))
                        return;

                    RefreshBaseInformationUI(assetData);

                    RefreshFilesInformationUI(assetData.sourceFiles);

                    UIElementsUtils.Hide(m_FilesLoadingLabel);
                }));
            }

            m_AssetPreview.SetStatus(null);

            tasks.Add(assetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.identifier))
                    return;

                m_AssetPreview.SetStatus(status);
            }));

            await Task.WhenAll(tasks);

            UIElementsUtils.Hide(m_FilesLoadingLabel);
        }

        void RefreshProjectPill(string projectId)
        {
            m_ProjectContainer.Clear();

            var projectInfo = m_ProjectOrganizationProvider.SelectedOrganization?.projectInfos.Find(p => p.id == projectId);

            if (projectInfo == null)
                return;

            var projectPill = new ProjectPill(projectInfo);
            projectPill.ProjectPillClickAction += pi => { m_ProjectOrganizationProvider.SelectProject(pi); };

            m_ProjectIconDownloader.DownloadIcon(projectInfo.id, (id, icon) =>
            {
                if (id == projectInfo.id)
                {
                    projectPill.SetIcon(icon);
                }
            });

            m_ProjectContainer.Add(projectPill);
        }

        void RefreshTags(IEnumerable<string> tags)
        {
            m_TagsContainer.Clear();

            foreach (var tag in tags)
            {
                var tagPill = new TagPill(tag);
                tagPill.TagPillClickAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    m_PageManager.activePage.pageFilters.AddSearchFilter(words);
                };
                m_TagsContainer.Add(tagPill);
            }

            UIElementsUtils.SetDisplay(m_TagSection, m_TagsContainer.childCount > 0);
        }

        void RefreshFoldoutStyleBasedOnExpansionStatus()
        {
            if (m_FilesFoldout.value)
            {
                m_FilesFoldout.AddToClassList("details-files-foldout-expanded");
            }
            else
            {
                m_FilesFoldout.RemoveFromClassList("details-files-foldout-expanded");
            }
        }

        void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.removed).Concat(args.updated).Any(a => a.Equals(m_SelectedAssetData?.identifier)))
                return;

            RefreshButtons(m_SelectedAssetData, m_AssetImporter.GetImportOperation(m_SelectedAssetData?.identifier));
        }

        void RefreshFilesInformationUI(IEnumerable<IAssetDataFile> assetDataFiles)
        {
            var files = assetDataFiles.ToList();

            var hasFiles = files.Any();

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.fileSize);
                m_Filesize.text = Utilities.BytesToReadableString(assetFileSize);
                m_TotalFiles.text = files.Count.ToString();

                m_FilesList = files.OrderBy(f => f.path).Select(f => f.path).ToList();

                m_FilesListView.makeItem = () => new DetailsPageFileItem(m_AssetDataManager, m_PageManager, m_AssetImporter, m_EditorGUIUtilityProxy, m_AssetDatabaseProxy);
                m_FilesListView.bindItem = (element, i) => { ((DetailsPageFileItem)element).Refresh(m_FilesList[i], m_FilesList); };
                m_FilesListView.itemsSource = m_FilesList;
                m_FilesListView.fixedItemHeight = 30;
            }
            else
            {
                m_Filesize.text = Utilities.BytesToReadableString(0);
                m_TotalFiles.text = "0";

                m_FilesListView.itemsSource = null;
            }

            UIElementsUtils.SetDisplay(m_FilesFoldout, hasFiles);
            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
            UIElementsUtils.SetDisplay(m_FilesListView, hasFiles);
        }

        void RefreshButtons(IAssetData assetData, BaseOperation importOperation)
        {
            UIElementsUtils.SetDisplay(m_ButtonsContainer, assetData is AssetData); // TODO Expose per button override per IAssetData

            var isInProject = m_AssetDataManager.IsInProject(assetData.identifier);
            var isImporting = importOperation?.Status == OperationStatus.InProgress;
            var isEnabled = !isImporting;

            m_ImportButton.SetEnabled(isEnabled);
            m_ImportButton.tooltip = !isEnabled ? L10n.Tr(Constants.ImportButtonDisabledToolTip) : string.Empty;
            if (isImporting)
            {
                m_ImportButton.text = $"{Constants.ImportingText} ({importOperation.Progress * 100:0.#}%)";
            }
            else
            {
                m_ImportButton.text = isInProject ? Constants.ReimportActionText : Constants.ImportActionText;
            }

            m_ShowInProjectBrowserButton.SetEnabled(isInProject);
            m_RemoveImportButton.SetEnabled(isInProject);
            m_RemoveImportButton.tooltip = isInProject ? string.Empty : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
        }
    }
}