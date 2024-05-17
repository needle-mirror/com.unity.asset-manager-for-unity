using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.Editor
{
    class AssetDetailsPage : SelectionInspectorPage
    {
        static readonly string k_TotalFiles = L10n.Tr("Total Files");
        static readonly string k_FilesSize = L10n.Tr("Files Size");
        static readonly string k_SourceFiles = L10n.Tr("Source Files");
        static readonly string k_UVCSFiles = L10n.Tr("UVCS Files");
        static readonly string k_Dependencies = L10n.Tr("Dependencies");
        static readonly string k_ImportLocationTitle = L10n.Tr("Choose import location");
        static readonly string k_Loading = L10n.Tr("Loading...");

        static readonly string k_DraftVersionUSSClassName = "asset-version--draft";

        AssetPreview m_AssetPreview;
        Label m_AssetName;
        Label m_AssetVersion;
        Label m_AssetId;
        Label m_Version;
        Label m_Description;
        Label m_UpdatedDate;
        Label m_CreatedDate;
        Label m_FilesSizeLabel;
        Label m_FilesSizeText;
        Label m_AssetType;
        Label m_Status;
        Label m_TotalFilesLabel;
        Label m_TotalFilesText;
        VisualElement m_TagsContainer;
        VisualElement m_TagSection;
        VisualElement m_ButtonsContainer;
        Button m_ImportButton;
        Button m_ImportToButton;
        Button m_ShowInProjectBrowserButton;
        Button m_RemoveImportButton;
        OperationProgressBar m_OperationProgressBar;
        VisualElement m_NoFilesWarningBox;
        VisualElement m_NoDependenciesBox;
        VisualElement m_ProjectContainer;
        Image m_AssetDashboardLink;
        FilesFoldout m_SourceFilesFoldout;
        FilesFoldout m_UVCSFilesFoldout;
        DependenciesFoldout m_DependenciesFoldout;
        IAssetData m_SelectedAssetData;
        UserInfo m_CreatedBy;
        UserInfo m_LastModifiedBy;
        VisualElement m_PopupContainer;
        Label m_LoadingLabel;

        int m_TotalFilesCount;
        long m_TotalFileSize;

        protected VisualElement m_CreatedByContainer;
        protected VisualElement m_LastModifiedByContainer;

        public AssetDetailsPage(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IUnityConnectProxy unityConnectProxy, IProjectIconDownloader projectIconDownloader, IPermissionsManager permissionsManager)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, unityConnectProxy, projectIconDownloader, permissionsManager)
        {
            BuildUxmlDocument();
        }

        protected sealed override void BuildUxmlDocument()
        {
            var treeAsset = UIElementsUtils.LoadUXML("DetailsPageContainer");
            treeAsset.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>("details-page-scrollview");
            m_AssetName = this.Q<Label>("asset-name");
            m_AssetVersion = this.Q<Label>("asset-version");
            m_AssetId = this.Q<Label>("id");
            m_Version = this.Q<Label>("version");
            m_Description = this.Q<Label>("description");
            var thumbnailContainer = this.Q<VisualElement>("details-page-thumbnail-container");
            m_AssetType = this.Q<Label>("assetType");
            m_Status = this.Q<Label>("status");
            m_TotalFilesText = this.Q<Label>("total-files");
            m_TotalFilesLabel = this.Q<Label>("total-files-label");
            m_TotalFilesLabel.text = k_TotalFiles;
            m_FilesSizeText = this.Q<Label>("files-size");
            m_FilesSizeLabel = this.Q<Label>("files-size-label");
            m_FilesSizeLabel.text = k_FilesSize;
            m_LoadingLabel = this.Q<Label>("details-page-loading");
            m_LoadingLabel.text = L10n.Tr(k_Loading);
            UIElementsUtils.Hide(m_LoadingLabel);

            m_SourceFilesFoldout = new FilesFoldout(this, "details-source-files-foldout",
                "details-source-files-listview", m_AssetDataManager, m_AssetDatabaseProxy,
                k_SourceFiles);

            m_UVCSFilesFoldout = new FilesFoldout(this, "details-uvcs-files-foldout",
                "details-uvcs-files-listview", m_AssetDataManager, m_AssetDatabaseProxy,
                k_UVCSFiles);

            m_DependenciesFoldout = new DependenciesFoldout(this, "details-dependencies-foldout",
                "details-dependencies-listview", m_PageManager, k_Dependencies);

            m_CreatedDate = this.Q<Label>("created-date");
            m_UpdatedDate = this.Q<Label>("updated-date");
            m_CloseButton = this.Q<Button>("closeButton");

            m_ImportButton = this.Q<Button>("importButton");
            m_ImportButton.text = L10n.Tr(Constants.ImportActionText);

            m_PopupContainer = CreatePopupContainer();

            m_ImportToButton = this.Q<Button>("importToButton");
            var caret = new VisualElement();

            caret.AddToClassList("import-to-button-caret");
            m_ImportToButton.Add(caret);

            m_ShowInProjectBrowserButton = this.Q<Button>("showInProjectBrowserButton");
            m_ShowInProjectBrowserButton.text = L10n.Tr(Constants.ShowInProjectActionText);

            m_RemoveImportButton = this.Q<Button>("removeImportButton");
            m_RemoveImportButton.text = L10n.Tr(Constants.RemoveFromProjectActionText);

            m_TagSection = this.Q<VisualElement>("details-page-tag-section");
            m_TagsContainer = this.Q<VisualElement>("details-page-tags-container");

            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr("No files were found in this asset.");

            m_NoDependenciesBox = this.Q<Label>("no-dependencies-label");
            m_NoDependenciesBox.Q<Label>().text = L10n.Tr("This asset has no dependencies");

            m_ProjectContainer = this.Q<VisualElement>("details-page-project-chip-container");
            m_CreatedByContainer = this.Q<VisualElement>("details-page-create-by-chip-container");
            m_LastModifiedByContainer = this.Q<VisualElement>("details-page-last-edit-by-chip-container");

            m_AssetDashboardLink = this.Q<Image>("asset-dashboard-link");
            m_AssetDashboardLink.tooltip = L10n.Tr("Open asset in the dashboard");
            m_AssetDashboardLink.RegisterCallback<ClickEvent>(e =>
            {
                m_LinksProxy.OpenAssetManagerDashboard(m_SelectedAssetData?.Identifier);
            });

            m_AssetPreview = new AssetPreview { name = "details-page-asset-preview" };
            m_AssetPreview.AddToClassList("image-container");
            m_AssetPreview.AddToClassList("details-page-asset-preview-thumbnail");
            thumbnailContainer.Add(m_AssetPreview);

            m_ButtonsContainer = this.Q<VisualElement>("footer-container");
            m_OperationProgressBar = new OperationProgressBar(() =>
            {
                m_AssetImporter.CancelImport(m_PageManager.ActivePage.LastSelectedAssetId, true);
            });
            m_ButtonsContainer.Add(m_OperationProgressBar);

            m_ScrollView.viewDataKey = "details-page-scrollview";

            m_SourceFilesFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.DetailsSourceFilesFoldoutValue = m_SourceFilesFoldout.Expanded;
                RefreshScrollView();
            });

            m_SourceFilesFoldout.Expanded = m_StateManager.DetailsSourceFilesFoldoutValue;

            m_UVCSFilesFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.DetailsUVCSFilesFoldoutValue = m_UVCSFilesFoldout.Expanded;
                RefreshScrollView();
            });

            m_UVCSFilesFoldout.Expanded = m_StateManager.DetailsUVCSFilesFoldoutValue;

            m_DependenciesFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.DependenciesFoldoutValue = m_DependenciesFoldout.Expanded;
                RefreshScrollView();
            });

            m_DependenciesFoldout.Expanded = m_StateManager.DependenciesFoldoutValue;

            // Setup the import button logic here. Since we have the assetData information both in this object's properties
            // and in AssetManagerWindow.m_CurrentAssetData, we should not need any special method with arguments
            m_ImportButton.clicked += () => _ = ImportAssetAsync(null);
            m_ImportToButton.clicked += ShowImportOptions;
            m_ShowInProjectBrowserButton.clicked += ShowInProjectBrowser;
            m_RemoveImportButton.clicked += RemoveFromProject;
            m_CloseButton.clicked += () => m_PageManager.ActivePage.ClearSelection();

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            m_SelectedAssetData = m_AssetDataManager.GetAssetData(m_PageManager.ActivePage?.LastSelectedAssetId);
            RefreshUI();
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            m_CreatedByContainer.RegisterCallback<ClickEvent>(OnCreatedByClicked);
            m_LastModifiedByContainer.RegisterCallback<ClickEvent>(OnLastModifiedByClicked);
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            m_CreatedByContainer.UnregisterCallback<ClickEvent>(OnCreatedByClicked);
            m_LastModifiedByContainer.UnregisterCallback<ClickEvent>(OnLastModifiedByClicked);
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if (operation is not ImportOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this) || !operation.Identifier.Equals(m_SelectedAssetData?.Identifier))
                return;

            RefreshButtons(m_SelectedAssetData, operation);
            m_OperationProgressBar.Refresh(operation);
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if (operation is not ImportOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this) || !operation.Identifier.Equals(m_SelectedAssetData?.Identifier) || operation.Status == OperationStatus.None)
                return;

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this) || !args.Added.Concat(args.Updated).Concat(args.Removed).Any(a => a.Equals(m_SelectedAssetData?.Identifier)))
                return;

            // In case of an import, force a full refresh of the displayed information
            _ = SelectAssetDataAsync(new List<IAssetData> { m_SelectedAssetData });
        }

        void OnCreatedByClicked(ClickEvent evt)
        {
            _ = m_PageManager.ActivePage.PageFilters.ApplyFilter(typeof(CreatedByFilter), m_CreatedBy?.Name);
        }

        void OnLastModifiedByClicked(ClickEvent evt)
        {
            _ = m_PageManager.ActivePage.PageFilters.ApplyFilter(typeof(UpdatedByFilter), m_LastModifiedBy?.Name);
        }

        VisualElement CreatePopupContainer()
        {
            var popupContainer = new VisualElement();
            popupContainer.focusable = true;
            popupContainer.AddToClassList("import-popup");
            UIElementsUtils.Hide(popupContainer);
            this.Q("importButtonContainer").Add(popupContainer);

            popupContainer.RegisterCallback<FocusOutEvent>(e =>
            {
                UIElementsUtils.Hide(popupContainer);
            });

            return popupContainer;
        }

        void ShowImportOptions()
        {
            m_PopupContainer.Clear();

            var importToText = new TextElement();
            importToText.style.paddingLeft = 24; // Add padding to match the checkmark icon
            importToText.AddToClassList("import-popup-import-to");
            importToText.text = L10n.Tr("Import to");
            importToText.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                UIElementsUtils.Hide(m_PopupContainer);

                var importLocation = Utilities.OpenFolderPanelInProject(k_ImportLocationTitle,
                    Constants.AssetsFolderName);

                // The user clicked cancel
                if (string.IsNullOrEmpty(importLocation))
                    return;

                _ = ImportAssetAsync(importLocation);
            });

            m_PopupContainer.Add(importToText);

            UIElementsUtils.Show(m_PopupContainer);
            m_PopupContainer.Focus();
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        async Task ImportAssetAsync(string optionalImportDestination)
        {
            var buttonType = m_ImportButton.text == Constants.ImportActionText ? DetailsButtonClickedEvent.ButtonType.Import : DetailsButtonClickedEvent.ButtonType.Reimport;
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(buttonType));

            m_ImportButton.SetEnabled(false);
            m_ImportToButton.SetEnabled(false);
            try
            {
                var isImporting = await m_AssetImporter.StartImportAsync(
                    m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList(),
                    optionalImportDestination);

                if (!isImporting)
                {
                    RefreshButtons(m_SelectedAssetData,
                        m_AssetOperationManager.GetAssetOperation(m_SelectedAssetData?.Identifier));
                }
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData,
                    m_AssetOperationManager.GetAssetOperation(m_SelectedAssetData?.Identifier));
                throw;
            }
        }

        void ShowInProjectBrowser()
        {
            var assetData = m_AssetDataManager.GetAssetData(m_PageManager.ActivePage.LastSelectedAssetId);
            m_AssetImporter.ShowInProject(assetData.Identifier);

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Show));
        }

        void RemoveFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            m_RemoveImportButton.SetEnabled(false);
            m_ShowInProjectBrowserButton.SetEnabled(false);

            try
            {
                if (!m_AssetImporter.RemoveImport(m_SelectedAssetData?.Identifier, true))
                {
                    m_RemoveImportButton.SetEnabled(true);
                    m_ShowInProjectBrowserButton.SetEnabled(true);
                }
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData,
                    m_AssetImporter.GetImportOperation(m_SelectedAssetData?.Identifier));
                throw;
            }
        }

        void RefreshBaseInformationUI(IAssetData assetData)
        {
            m_AssetName.text = assetData.Name;

            m_AssetVersion.text = assetData.VersionNumber > 0
                ? L10n.Tr(Constants.VersionText) + assetData.VersionNumber
                : L10n.Tr(Constants.PendingText);

            if (assetData.Status == Constants.AssetDraftStatus)
            {
                m_AssetVersion.AddToClassList(k_DraftVersionUSSClassName);
            }
            else
            {
                m_AssetVersion.RemoveFromClassList(k_DraftVersionUSSClassName);
            }

            m_AssetId.text = assetData.Identifier.AssetId;
            UIElementsUtils.SetDisplay(m_AssetId.parent, !string.IsNullOrEmpty(m_AssetId.text));

            m_AssetType.text = assetData.AssetType.DisplayValue();
            UIElementsUtils.SetDisplay(m_AssetType.parent, !string.IsNullOrEmpty(m_AssetType.text));

            m_Status.text = assetData.Status;
            UIElementsUtils.SetDisplay(m_Status.parent, !string.IsNullOrEmpty(m_Status.text));

            m_Version.text = assetData.Identifier.Version;
            UIElementsUtils.SetDisplay(m_Version.parent, !string.IsNullOrEmpty(m_Version.text));

            m_CreatedDate.text = assetData.Created?.ToLocalTime().ToString("G");
            UIElementsUtils.SetDisplay(m_CreatedDate.parent, !string.IsNullOrEmpty(m_CreatedDate.text));
            UIElementsUtils.SetDisplay(m_AssetDashboardLink,
                !(string.IsNullOrEmpty(assetData.Identifier.OrganizationId) ||
                  string.IsNullOrEmpty(assetData.Identifier.ProjectId) ||
                  string.IsNullOrEmpty(assetData.Identifier.AssetId)));

            _ = m_ProjectOrganizationProvider.SelectedOrganization.GetUserInfosAsync(userInfos =>
            {
                m_CreatedBy = userInfos.Find(ui => ui.UserId == assetData.CreatedBy);
                RefreshUserChip(m_CreatedByContainer, m_CreatedBy);
            });

            m_UpdatedDate.text = assetData.Updated?.ToLocalTime().ToString("G");
            UIElementsUtils.SetDisplay(m_UpdatedDate.parent, !string.IsNullOrEmpty(m_UpdatedDate.text));

            if (assetData.UpdatedBy == "System" || assetData.UpdatedBy == "Service Account")
            {
                m_LastModifiedBy = new UserInfo { Name = L10n.Tr("Service Account") };
                RefreshUserChip(m_LastModifiedByContainer, m_LastModifiedBy);
            }
            else
            {
                _ = m_ProjectOrganizationProvider.SelectedOrganization.GetUserInfosAsync(userInfos =>
                {
                    m_LastModifiedBy = userInfos.Find(ui => ui.UserId == assetData.UpdatedBy);
                    RefreshUserChip(m_LastModifiedByContainer, m_LastModifiedBy);
                });
            }

            m_Description.text = assetData.Description;
            UIElementsUtils.SetDisplay(m_Description, !string.IsNullOrEmpty(m_Description.text));

            m_AssetPreview.SetAssetType(assetData.PrimaryExtension);
            m_AssetPreview.SetStatuses(assetData.PreviewStatus);

            RefreshProjectChip(assetData.Identifier.ProjectId);
            RefreshTags(assetData.Tags);

            var operation = m_AssetOperationManager.GetAssetOperation(assetData.Identifier);
            RefreshButtons(assetData, operation);

            m_OperationProgressBar.Refresh(operation);

            m_SourceFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_UVCSFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
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
                if (!identifier.Equals(m_SelectedAssetData?.Identifier))
                    return;

                m_AssetPreview.SetThumbnail(texture2D);
            });

            m_TotalFilesCount = 0;
            m_TotalFileSize = 0;

            UIElementsUtils.Hide(m_LoadingLabel);
            RefreshSourceFilesInformationUI(m_SelectedAssetData);
            RefreshUVCSFilesInformationUI(m_SelectedAssetData);
            RefreshDependenciesInformationUI(m_SelectedAssetData);

            var hasFiles = m_TotalFilesCount > 0;
            if (hasFiles)
            {
                m_FilesSizeText.text = Utilities.BytesToReadableString(m_TotalFileSize);
                m_TotalFilesText.text = m_TotalFilesCount.ToString();
            }
            else
            {
                m_FilesSizeText.text = Utilities.BytesToReadableString(0);
                m_TotalFilesText.text = "0";
            }

            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
        }

        protected override async Task SelectAssetDataAsync(List<IAssetData> assetData)
        {
            if (assetData.Count > 1)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            UIElementsUtils.Show(this);

            m_SelectedAssetData = assetData.FirstOrDefault();

            if (m_SelectedAssetData == null)
                return;

            RefreshUI();
            RefreshScrollView();

            var isInProject = m_AssetDataManager.IsInProject(m_SelectedAssetData.Identifier);
            var tasks = new List<Task>();

            if (!isInProject)
            {
                UIElementsUtils.Hide(m_NoFilesWarningBox);
                UIElementsUtils.Hide(m_NoDependenciesBox);
                UIElementsUtils.Show(m_LoadingLabel);

                m_SourceFilesFoldout.StartPopulating();
                m_UVCSFilesFoldout.StartPopulating();
                m_DependenciesFoldout.StartPopulating();

                m_TotalFilesText.text = "-";
                m_FilesSizeText.text = "-";

                tasks.Add(m_SelectedAssetData.SyncWithCloudAsync(identifier =>
                {
                    if (!identifier.AssetId.Equals(m_SelectedAssetData?.Identifier.AssetId))
                        return;

                    RefreshUI();
                    RefreshScrollView();
                }));
            }

            m_AssetPreview.SetStatuses(null);

            tasks.Add(m_SelectedAssetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.Identifier))
                    return;

                m_AssetPreview.SetStatuses(status);
            }));

            await Utilities.WaitForTasksAndHandleExceptions(tasks);
        }

        static void RefreshUserChip(VisualElement container, UserInfo userInfo)
        {
            container.Clear();

            if (userInfo == null || string.IsNullOrEmpty(userInfo.Name))
            {
                UIElementsUtils.Hide(container);
                return;
            }

            UIElementsUtils.Show(container);

            var userChip = new UserChip(userInfo);
            container.Add(userChip);
        }

        void RefreshProjectChip(string projectId)
        {
            m_ProjectContainer.Clear();

            var projectInfo = m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos.Find(p => p.Id == projectId);

            if (projectInfo == null)
            {
                UIElementsUtils.Hide(m_ProjectContainer.parent);
                return;
            }

            UIElementsUtils.Show(m_ProjectContainer.parent);

            var projectChip = new ProjectChip(projectInfo);
            projectChip.ProjectChipClickAction += pi => { m_ProjectOrganizationProvider.SelectProject(pi); };

            m_ProjectIconDownloader.DownloadIcon(projectInfo.Id, (id, icon) =>
            {
                if (id == projectInfo.Id)
                {
                    projectChip.SetIcon(icon);
                }
            });

            m_ProjectContainer.Add(projectChip);
        }

        void RefreshTags(IEnumerable<string> tags)
        {
            m_TagsContainer.Clear();

            foreach (var tag in tags)
            {
                var tagChip = new TagChip(tag);
                tagChip.TagChipClickAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    m_PageManager.ActivePage.PageFilters.AddSearchFilter(words);
                };
                m_TagsContainer.Add(tagChip);
            }

            UIElementsUtils.SetDisplay(m_TagSection, m_TagsContainer.childCount > 0);
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this) || !args.Added.Concat(args.Removed).Concat(args.Updated).Any(a => a.Equals(m_SelectedAssetData?.Identifier)))
                return;

            RefreshButtons(m_SelectedAssetData, m_AssetImporter.GetImportOperation(m_SelectedAssetData?.Identifier));
        }

        void RefreshSourceFilesInformationUI(IAssetData assetData)
        {
            var files = assetData.SourceFiles.ToList();

            var hasFiles = files.Any();

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.FileSize);
                m_TotalFileSize += assetFileSize;
                m_TotalFilesCount += files.Count;

                m_SourceFilesFoldout.Populate(assetData, files);
            }
            else
            {
                m_SourceFilesFoldout.Clear();
            }

            m_SourceFilesFoldout.StopPopulating();
        }

        void RefreshUVCSFilesInformationUI(IAssetData assetData)
        {
            var files = assetData.UVCSFiles?.ToList();

            var hasFiles = files != null && files.Any();

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.FileSize);
                m_TotalFileSize += assetFileSize;
                m_TotalFilesCount += files.Count;

                m_UVCSFilesFoldout.Populate(assetData, files);
            }
            else
            {
                m_UVCSFilesFoldout.Clear();
            }

            m_UVCSFilesFoldout.StopPopulating();
        }

        void RefreshDependenciesInformationUI(IAssetData assetData)
        {
            var dependencyAssets = assetData.Dependencies.ToList();
            m_DependenciesFoldout.Populate(assetData, dependencyAssets);
            UIElementsUtils.SetDisplay(m_NoDependenciesBox, !dependencyAssets.Any());
            m_DependenciesFoldout.StopPopulating();
        }

        void RefreshButtons(IAssetData assetData, BaseOperation importOperation)
        {
            UIElementsUtils.SetDisplay(m_ButtonsContainer, assetData is AssetData);

            var isInProject = m_AssetDataManager.IsInProject(assetData.Identifier);
            var isImporting = importOperation?.Status == OperationStatus.InProgress;
            var isImportingPermission = m_PermissionsManager.CheckPermission(Constants.ImportPermission);
            var isEnabled = !isImporting && isImportingPermission && m_UnityConnectProxy.AreCloudServicesReachable;

            m_ImportButton.SetEnabled(isEnabled);
            m_ImportToButton.SetEnabled(isEnabled);
            m_ImportButton.text = GetImportButtonLabel(isImporting, assetData.PreviewStatus.FirstOrDefault(), importOperation);
            m_ImportButton.tooltip = m_ImportToButton.tooltip = GetImportButtonTooltip(isEnabled, isImportingPermission);

            m_ProjectContainer.SetEnabled(m_UnityConnectProxy.AreCloudServicesReachable);
            m_AssetDashboardLink.SetEnabled(m_UnityConnectProxy.AreCloudServicesReachable);
            m_ImportToButton.SetEnabled(isEnabled);
            m_ShowInProjectBrowserButton.SetEnabled(isInProject);
            m_RemoveImportButton.SetEnabled(isInProject && !isImporting);
            m_RemoveImportButton.tooltip = isInProject ? string.Empty : L10n.Tr(Constants.RemoveFromProjectButtonDisabledToolTip);
        }

        static string GetImportButtonTooltip(bool isEnabled, bool isImportingPermission)
        {
            if (isEnabled)
            {
                return string.Empty;
            }

            return L10n.Tr(isImportingPermission ? Constants.ImportButtonDisabledToolTip : Constants.ImportNoPermissionMessage);
        }

        static string GetImportButtonLabel(bool isImporting, AssetPreview.IStatus status, BaseOperation importOperation)
        {
            if (isImporting)
            {
                return $"{L10n.Tr(Constants.ImportingText)} ({importOperation.Progress * 100:0.#}%)";
            }

            return status != null ? L10n.Tr(status.ActionText) : L10n.Tr(Constants.ImportActionText);
        }
    }
}
