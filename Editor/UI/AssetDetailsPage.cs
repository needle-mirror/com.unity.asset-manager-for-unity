using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.Editor
{
    class AssetDetailsPage : VisualElement
    {
        readonly AssetPreview m_AssetPreview;
        readonly Label m_AssetName;
        readonly Label m_AssetId;
        readonly Label m_Version;
        readonly Label m_Description;
        readonly Label m_UpdatedDate;
        readonly Label m_CreatedDate;
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

        readonly OperationProgressBar m_OperationProgressBar;
        readonly VisualElement m_NoFilesWarningBox;
        readonly VisualElement m_NoDependenciesBox;
        readonly VisualElement m_ProjectContainer;
        readonly Image m_AssetDashboardLink;

        readonly FilesFoldout m_FilesFoldout;
        readonly DependenciesFoldout m_DependenciesFoldout;

        readonly IAssetImporter m_AssetImporter;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IProjectIconDownloader m_ProjectIconDownloader;

        IAssetData m_SelectedAssetData;


        public AssetDetailsPage(IAssetImporter assetImporter,
            IAssetOperationManager assetOperationManager,
            IStateManager stateManager,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IProjectIconDownloader projectIconDownloader)
        {
            m_AssetImporter = assetImporter;
            m_AssetOperationManager = assetOperationManager;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
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
            m_AssetType = this.Q<Label>("assetType");
            m_Status = this.Q<Label>("status");
            m_TotalFiles = this.Q<Label>("total-files");
            m_Filesize = this.Q<Label>("filesize");

            m_FilesFoldout = new FilesFoldout(this, "details-files-foldout", "details-files-listview", "details-files-loading-label",
                assetDataManager, pageManager, assetImporter, assetDatabaseProxy);

            m_DependenciesFoldout = new DependenciesFoldout(this, "details-dependencies-foldout", "details-dependencies-listview", "details-dependencies-loading-label",
                pageManager);

            m_CreatedDate = this.Q<Label>("created-date");
            m_UpdatedDate = this.Q<Label>("updated-date");
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

            m_NoDependenciesBox = this.Q<VisualElement>("no-dependencies-box");
            m_NoDependenciesBox.Q<Label>().text = L10n.Tr("This asset has no dependencies");

            m_ProjectContainer = this.Q<VisualElement>("details-page-project-pill-container");

            m_AssetDashboardLink = this.Q<Image>("asset-dashboard-link");
            m_AssetDashboardLink.image = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ExternalLinkName]);
            m_AssetDashboardLink.tooltip = L10n.Tr("Open asset in the dashboard");
            m_AssetDashboardLink.RegisterCallback<ClickEvent>(e =>
            {
                linksProxy.OpenAssetManagerDashboard(m_SelectedAssetData?.identifier);
            });

            m_AssetPreview = new AssetPreview { name = "details-page-asset-preview" };
            m_AssetPreview.AddToClassList("image-container");
            m_AssetPreview.AddToClassList("details-page-asset-preview-thumbnail");
            thumbnailContainer.Add(m_AssetPreview);

            m_ButtonsContainer = this.Q<VisualElement>("footer-container");
            m_OperationProgressBar = new OperationProgressBar(() =>
            {
                assetImporter.CancelImport(pageManager.activePage.selectedAssetId, true);
            });
            m_ButtonsContainer.Add(m_OperationProgressBar);

            scrollView.viewDataKey = "details-page-scrollview";

            m_FilesFoldout.RegisterValueChangedCallback(_ =>
            {
                stateManager.detailsFileFoldoutValue = m_FilesFoldout.Expanded;
                RefreshScrollView();
            });

            m_FilesFoldout.Expanded = stateManager.detailsFileFoldoutValue;

            m_DependenciesFoldout.RegisterValueChangedCallback(_ =>
            {
                stateManager.detailsFileFoldoutValue = m_DependenciesFoldout.Expanded; // TODO Use own state
                RefreshScrollView();
            });

            m_DependenciesFoldout.Expanded = stateManager.detailsFileFoldoutValue;

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

        void RefreshScrollView()
        {
            // Bug in UI Toolkit where the scrollview does not update its size when the foldout is expanded/collapsed.
            schedule.Execute(_ => { this.Q<ScrollView>("details-page-scrollview").verticalScrollerVisibility = ScrollerVisibility.Auto; }).StartingIn(25);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_AssetOperationManager.OperationProgressChanged += OnOperationProgress;
            m_AssetOperationManager.OperationFinished += OnOperationFinished;
            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged += OnAssetDataChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_AssetOperationManager.OperationProgressChanged -= OnOperationProgress;
            m_AssetOperationManager.OperationFinished -= OnOperationFinished;
            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_AssetDataManager.onAssetDataChanged -= OnAssetDataChanged;
        }

        public async Task SelectedAsset(AssetIdentifier assetIdentifier)
        {
            var assetData = await m_AssetDataManager.GetOrSearchAssetData(assetIdentifier, default);
            await SelectAssetDataAsync(assetData);
        }

        private void OnOperationProgress(AssetDataOperation operation)
        {
            if (!operation.AssetId.Equals(m_SelectedAssetData?.identifier))
                return;

            RefreshButtons(m_SelectedAssetData, operation);
            m_OperationProgressBar.Refresh(operation);
        }

        private void OnOperationFinished(AssetDataOperation operation)
        {
            if (!operation.AssetId.Equals(m_SelectedAssetData?.identifier))
                return;

            RefreshUI();
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!args.added.Concat(args.updated).Concat(args.removed).Any(a => a.Equals(m_SelectedAssetData?.identifier)))
                return;

            // In case of an import, force a full refresh of the displayed information
            _ = SelectAssetDataAsync(m_SelectedAssetData);
        }

        private void ImportAssetAsync()
        {
            var buttonType = m_ImportButton.text == Constants.ImportActionText ?
                DetailsButtonClickedEvent.ButtonType.Import :
                DetailsButtonClickedEvent.ButtonType.Reimport;
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(buttonType));

            m_ImportButton.SetEnabled(false);
            try
            {
                m_AssetImporter.StartImportAsync(m_SelectedAssetData);
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData, m_AssetOperationManager.GetAssetOperation(m_SelectedAssetData?.identifier));
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
                if (!m_AssetImporter.RemoveImport(m_SelectedAssetData?.identifier, true))
                {
                    m_RemoveImportButton.SetEnabled(true);
                    m_ShowInProjectBrowserButton.SetEnabled(true);
                }
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
            UIElementsUtils.SetDisplay(m_AssetId.parent, !string.IsNullOrEmpty(m_AssetId.text));

            m_AssetType.text = assetData.assetType.DisplayValue();
            UIElementsUtils.SetDisplay(m_AssetType.parent, !string.IsNullOrEmpty(m_AssetType.text));

            m_Status.text = assetData.status;
            UIElementsUtils.SetDisplay(m_Status.parent, !string.IsNullOrEmpty(m_Status.text));

            m_Version.text = assetData.identifier.version;
            UIElementsUtils.SetDisplay(m_Version.parent, !string.IsNullOrEmpty(m_Version.text));

            m_CreatedDate.text = assetData.created?.ToLocalTime().ToString("G");
            UIElementsUtils.SetDisplay(m_CreatedDate.parent, !string.IsNullOrEmpty(m_CreatedDate.text));
            UIElementsUtils.SetDisplay(m_AssetDashboardLink,
                !(string.IsNullOrEmpty(assetData.identifier.organizationId) || string.IsNullOrEmpty(assetData.identifier.projectId) || string.IsNullOrEmpty(assetData.identifier.assetId)));


            m_UpdatedDate.text = assetData.updated?.ToLocalTime().ToString("G");
            UIElementsUtils.SetDisplay(m_UpdatedDate.parent, !string.IsNullOrEmpty(m_UpdatedDate.text));

            m_Description.text = assetData.description;
            UIElementsUtils.SetDisplay(m_Description, !string.IsNullOrEmpty(m_Description.text));

            m_AssetPreview.SetAssetType(assetData.primaryExtension);
            m_AssetPreview.SetStatuses(assetData.previewStatus);

            RefreshProjectPill(assetData.identifier.projectId);
            RefreshTags(assetData.tags);

            var operation = m_AssetOperationManager.GetAssetOperation(assetData.identifier);
            RefreshButtons(assetData, operation);

            m_OperationProgressBar.Refresh(operation);

            m_FilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_DependenciesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void RefreshUI()
        {
            if (m_SelectedAssetData == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            m_AssetPreview.ClearPreview();

            UIElementsUtils.Show(this);

            RefreshBaseInformationUI(m_SelectedAssetData);

            _ = m_SelectedAssetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.identifier))
                    return;

                m_AssetPreview.SetThumbnail(texture2D);
            });

            RefreshFilesInformationUI(m_SelectedAssetData.sourceFiles);
            RefreshDependenciesInformationUI(m_SelectedAssetData.dependencies);
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
                UIElementsUtils.Hide(m_NoFilesWarningBox);
                UIElementsUtils.Hide(m_NoDependenciesBox);

                m_FilesFoldout.StartPopulating();
                m_DependenciesFoldout.StartPopulating();

                m_TotalFiles.text = "-";
                m_Filesize.text = "-";

                tasks.Add(assetData.SyncWithCloudAsync(identifier =>
                {
                    if (!identifier.Equals(m_SelectedAssetData?.identifier))
                        return;

                    RefreshUI();
                }));
            }

            m_AssetPreview.SetStatuses(null);

            tasks.Add(assetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.identifier))
                    return;

                m_AssetPreview.SetStatuses(status);
            }));

            // TODO Display global loading state

            await Utilities.WaitForTasksAndHandleExceptions(tasks);

            // TODO Hide global loading state
        }

        void RefreshProjectPill(string projectId)
        {
            m_ProjectContainer.Clear();

            var projectInfo = m_ProjectOrganizationProvider.SelectedOrganization?.projectInfos.Find(p => p.id == projectId);

            if (projectInfo == null)
            {
                UIElementsUtils.Hide(m_ProjectContainer.parent);
                return;
            }

            UIElementsUtils.Show(m_ProjectContainer.parent);

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

                m_FilesFoldout.Populate(files);
            }
            else
            {
                m_Filesize.text = Utilities.BytesToReadableString(0);
                m_TotalFiles.text = "0";

                m_FilesFoldout.Clear();
            }

            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
            m_FilesFoldout.StopPopulating();
        }

        void RefreshDependenciesInformationUI(IEnumerable<DependencyAsset> dependencies)
        {
            var dependencyAssets = dependencies.ToList();
            m_DependenciesFoldout.Populate(dependencyAssets);
            UIElementsUtils.SetDisplay(m_NoDependenciesBox, !dependencyAssets.Any());
            m_DependenciesFoldout.StopPopulating();
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