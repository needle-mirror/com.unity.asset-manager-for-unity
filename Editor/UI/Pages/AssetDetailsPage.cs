using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Unity.AssetManager.UI.Editor
{
    interface IPageComponent
    {
        void OnSelection(BaseAssetData assetData);
        void RefreshUI(BaseAssetData assetData, bool isLoading = false);
        void RefreshButtons(UIEnabledStates enabled, BaseAssetData assetData, BaseOperation operationInProgress);
    }

    class AssetDetailsPage : SelectionInspectorPage
    {
        readonly IEnumerable<IPageComponent> m_PageComponents;

        VisualElement m_NoFilesWarningBox;
        VisualElement m_SameFileNamesWarningBox;
        VisualElement m_NoDependenciesBox;
        FilesFoldout m_SourceFilesFoldout;
        FilesFoldout m_UVCSFilesFoldout;
        DependenciesFoldout m_DependenciesFoldout;

        BaseAssetData m_SelectedAssetData;

        BaseAssetData SelectedAssetData
        {
            get => m_SelectedAssetData;
            set
            {
                if (m_SelectedAssetData == value)
                    return;

                if (m_SelectedAssetData != null)
                {
                    m_SelectedAssetData.AssetDataChanged -= OnAssetDataEvent;
                }

                m_SelectedAssetData = value;

                if (m_SelectedAssetData != null)
                {
                    m_SelectedAssetData.AssetDataChanged += OnAssetDataEvent;
                }
            }
        }

        void OnAssetDataEvent(BaseAssetData assetData, AssetDataEventType eventType)
        {
            if (assetData != SelectedAssetData)
                return;

            switch (eventType)
            {
                case AssetDataEventType.PrimaryFileChanged:
                    RefreshSourceFilesInformationUI(SelectedAssetData);
                    break;
                // Need to add more cases similar to GridItem
            }
        }

        BaseAssetData m_PreviouslySelectedAssetData;

        readonly Action<IEnumerable<AssetPreview.IStatus>> PreviewStatusUpdated;
        readonly Action<Texture2D> PreviewImageUpdated;
        readonly Action<string> SetFileCount;
        readonly Action<string> SetFileSize;
        readonly Action<string> SetPrimaryExtension;
        Action<Type, string> ApplyFilter;

        public AssetDetailsPage(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IUnityConnectProxy unityConnectProxy, IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, unityConnectProxy, projectIconDownloader,
                permissionsManager)
        {
            BuildUxmlDocument();

            var header = new AssetDetailsHeader(this);
            header.OpenDashboard += LinkToDashboard;

            var footer = new AssetDetailsFooter(this);
            footer.CancelOperation += CancelOrClearImport;
            footer.ImportAsset += ImportAssetAsync;
            footer.HighlightAsset += ShowInProjectBrowser;
            footer.RemoveAsset += RemoveFromProject;
            PreviewStatusUpdated += footer.UpdatePreviewStatus;

            var detailsTab = new AssetDetailsTab(m_ScrollView.contentContainer);
            detailsTab.CreateProjectChip += CreateProjectChip;
            detailsTab.CreateUserChip += CreateUserChip;
            detailsTab.ApplyFilter += OnFilterModified;
            PreviewStatusUpdated += detailsTab.UpdatePreviewStatus;
            PreviewImageUpdated += detailsTab.SetPreviewImage;
            SetFileCount += detailsTab.SetFileCount;
            SetFileSize += detailsTab.SetFileSize;
            SetPrimaryExtension += detailsTab.SetPrimaryExtension;

            var versionsTab = new AssetVersionsTab(m_ScrollView.contentContainer);
            versionsTab.CreateUserChip += CreateUserChip;
            versionsTab.ApplyFilter += OnFilterModified;
            versionsTab.ApplyFilter += OnFilterModified;
            versionsTab.ImportAsset += ImportAssetAsync;

            var tabBar = new AssetDetailsPageTabs(this, footer.ButtonsContainer,
                new AssetTab[]
                {
                    detailsTab,
                    versionsTab
                });

            m_PageComponents = new IPageComponent[]
            {
                header,
                footer,
                tabBar,
                detailsTab,
                versionsTab,
            };

            RefreshUI();
        }

        public override bool IsVisible(int selectedAssetCount)
        {
            return selectedAssetCount == 1;
        }

        protected sealed override void BuildUxmlDocument()
        {
            var treeAsset = UIElementsUtils.LoadUXML("DetailsPageContainer");
            treeAsset.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>("details-page-scrollview");

            m_SourceFilesFoldout = new FilesFoldout(this, "details-source-files-foldout",
                "details-source-files-listview", m_AssetDatabaseProxy,
                Constants.SourceFilesText);

            m_UVCSFilesFoldout = new FilesFoldout(this, "details-uvcs-files-foldout",
                "details-uvcs-files-listview", m_AssetDatabaseProxy,
                Constants.UVCSFilesText);

            m_DependenciesFoldout = new DependenciesFoldout(this, "details-dependencies-foldout",
                "details-dependencies-listview", m_PageManager, Constants.DependenciesText);

            m_CloseButton = this.Q<Button>("closeButton");

            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr(Constants.NoFilesText);

            m_SameFileNamesWarningBox = this.Q<VisualElement>("same-files-warning-box");
            m_SameFileNamesWarningBox.Q<Label>().text = L10n.Tr(Constants.SameFileNamesText);

            m_NoDependenciesBox = this.Q<Label>("no-dependencies-label");
            m_NoDependenciesBox.Q<Label>().text = L10n.Tr(Constants.NoDependenciesText);

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

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            SelectedAssetData = m_PageManager.ActivePage?.SelectedAssets.Count > 1 ? null : m_AssetDataManager.GetAssetData(m_PageManager.ActivePage?.LastSelectedAssetId);
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            m_CloseButton.clicked += OnCloseButton;

            ApplyFilter += OnFilterModified;
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            m_CloseButton.clicked -= OnCloseButton;

            ApplyFilter -= OnFilterModified;
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(SelectedAssetData?.Identifier))
                return;

            RefreshButtons(SelectedAssetData, operation);
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation)
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(SelectedAssetData?.Identifier)
                || operation.Status == OperationStatus.None)
                return;

            if (m_PreviouslySelectedAssetData != null &&
                operation.Status is OperationStatus.Cancelled or OperationStatus.Error)
            {
                SelectedAssetData = m_PreviouslySelectedAssetData;
            }

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this)
                || !args.Added.Concat(args.Updated).Concat(args.Removed).Any(a => a.Equals(SelectedAssetData?.Identifier)))
                return;

            // In case of an import, force a full refresh of the displayed information
            TaskUtils.TrackException(SelectAssetDataAsync(new List<BaseAssetData> { SelectedAssetData }));
        }

        void OnFilterModified(Type filterType, string filterValue)
        {
            TaskUtils.TrackException(m_PageManager.ActivePage.PageFilters.ApplyFilter(filterType, filterValue));
        }

        void OnFilterModified(IEnumerable<string> filterValue)
        {
            m_PageManager.ActivePage.PageFilters.AddSearchFilter(filterValue);
        }

        void LinkToDashboard()
        {
            m_LinksProxy.OpenAssetManagerDashboard(SelectedAssetData?.Identifier);
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        void CancelOrClearImport()
        {
            var operation = m_AssetOperationManager.GetAssetOperation(m_PageManager.ActivePage.LastSelectedAssetId);
            if (operation == null)
                return;

            if (operation.Status == OperationStatus.InProgress)
            {
                m_AssetImporter.CancelImport(m_PageManager.ActivePage.LastSelectedAssetId, true);
            }
            else
            {
                m_AssetOperationManager.ClearFinishedOperations();
            }
        }

        async void ImportAssetAsync(string importDestination, IEnumerable<BaseAssetData> assetsToImport = null)
        {
            m_PreviouslySelectedAssetData = SelectedAssetData;
            try
            {
                var importType = assetsToImport == null ? ImportOperation.ImportType.UpdateToLatest : ImportOperation.ImportType.Import;

                // If assets have been targeted for import, we use the first asset as the selected asset
                SelectedAssetData = assetsToImport?.FirstOrDefault() ?? SelectedAssetData;

                // If no assets have been targeted for import, we use the selected asset from the details page
                assetsToImport ??= m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x));

                var importedAssets = await m_AssetImporter.StartImportAsync(assetsToImport.ToList(), importType, importDestination);
                SelectedAssetData = importedAssets?.FirstOrDefault() ?? m_PreviouslySelectedAssetData;

                if (importedAssets == null)
                {
                    RefreshUI();
                }
            }
            catch (Exception)
            {
                SelectedAssetData = m_PreviouslySelectedAssetData;
                RefreshUI();
                throw;
            }
        }

        void ShowInProjectBrowser()
        {
            m_AssetImporter.ShowInProject(SelectedAssetData?.Identifier);

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Show));
        }

        bool RemoveFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            try
            {
                return m_AssetImporter.RemoveImport(SelectedAssetData?.Identifier, true);
            }
            catch (Exception)
            {
                RefreshButtons(SelectedAssetData,
                    m_AssetImporter.GetImportOperation(SelectedAssetData?.Identifier));
                throw;
            }
        }

        void RefreshUI(bool isLoading = false)
        {
            if (SelectedAssetData == null)
                return;

            // Asynchronously load the new image and update the UI
            TaskUtils.TrackException(SelectedAssetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(SelectedAssetData?.Identifier))
                    return;

                PreviewImageUpdated?.Invoke(texture2D);
            }));

            foreach (var component in m_PageComponents)
            {
                component.RefreshUI(SelectedAssetData, isLoading);
            }

            var operation = m_AssetOperationManager.GetAssetOperation(SelectedAssetData.Identifier);

            RefreshButtons(SelectedAssetData, operation);

            m_SourceFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_UVCSFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_DependenciesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();

            RefreshSourceFilesInformationUI(SelectedAssetData);
            RefreshDependenciesInformationUI(SelectedAssetData);
        }

        protected override async Task SelectAssetDataAsync(IReadOnlyCollection<BaseAssetData> assetData)
        {
            if (assetData == null || assetData.Count > 1)
            {
                SelectedAssetData = null;
                return;
            }

            SelectedAssetData = assetData.FirstOrDefault();

            if (SelectedAssetData == null)
                return;

            var requiresLoading = SelectedAssetData is not UploadAssetData && !m_AssetDataManager.IsInProject(SelectedAssetData.Identifier);

            foreach (var component in m_PageComponents)
            {
                component.OnSelection(SelectedAssetData);
            }

            var tasks = new List<Task>();
            if (requiresLoading)
            {
                UIElementsUtils.Hide(m_NoFilesWarningBox);
                UIElementsUtils.Hide(m_SameFileNamesWarningBox);
                UIElementsUtils.Hide(m_NoDependenciesBox);

                m_SourceFilesFoldout.StartPopulating();
                m_UVCSFilesFoldout.StartPopulating();
                m_DependenciesFoldout.StartPopulating();

                SetFileSize?.Invoke("-");
                SetFileCount?.Invoke("-");

                tasks.Add(SyncWithCloudAsync(SelectedAssetData));
            }

            RefreshUI(requiresLoading);
            RefreshScrollView();

            PreviewStatusUpdated?.Invoke(null);

            tasks.Add(SelectedAssetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(SelectedAssetData?.Identifier))
                    return;

                PreviewStatusUpdated?.Invoke(AssetDataStatus.GetIStatusFromAssetDataStatusType(status));
            }));

            await TaskUtils.WaitForTasksWithHandleExceptions(tasks);
        }

        async Task SyncWithCloudAsync(BaseAssetData assetData)
        {
            var tasks = new List<Task>
            {
                assetData.RefreshPropertiesAsync(),
                assetData.ResolvePrimaryExtensionAsync(),
                assetData.RefreshDependenciesAsync(),
            };

            await Task.WhenAll(tasks);

            if (!assetData.Identifier.Equals(SelectedAssetData?.Identifier))
                return;

            RefreshUI();
            RefreshScrollView();
        }

        async Task<UserChip> CreateUserChip(string userId, Type searchFilterType)
        {
            UserChip userChip = null;

            if (userId is "System" or "Service Account")
            {
                var userInfo = new UserInfo { Name = L10n.Tr(Constants.ServiceAccountText) };
                userChip = RefreshUserChip(userInfo, null);
            }
            else
            {
                await m_ProjectOrganizationProvider.SelectedOrganization.GetUserInfosAsync(userInfos =>
                {
                    var userInfo = userInfos.Find(ui => ui.UserId == userId);
                    userChip = RefreshUserChip(userInfo, searchFilterType);
                });
            }

            return userChip;
        }

        UserChip RefreshUserChip(UserInfo userInfo, Type searchFilterType)
        {
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Name))
            {
                return null;
            }

            var userChip = new UserChip(userInfo);

            if (searchFilterType != null)
            {
                userChip.RegisterCallback<ClickEvent>(_ => ApplyFilter?.Invoke(searchFilterType, userInfo.Name));
            }

            return userChip;
        }

        ProjectChip CreateProjectChip(string projectId)
        {
            var projectInfo = m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos.Find(p => p.Id == projectId);

            if (projectInfo == null)
            {
                return null;
            }

            var projectChip = new ProjectChip(projectInfo);
            projectChip.ProjectChipClickAction += pi => { m_ProjectOrganizationProvider.SelectProject(pi); };

            m_ProjectIconDownloader.DownloadIcon(projectInfo.Id, (id, icon) =>
            {
                if (id == projectInfo.Id)
                {
                    projectChip.SetIcon(icon);
                }
            });

            return projectChip;
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this)
                || !args.Added.Concat(args.Removed).Concat(args.Updated).Any(a => a.Equals(SelectedAssetData?.Identifier)))
                return;

            RefreshButtons(SelectedAssetData, m_AssetImporter.GetImportOperation(SelectedAssetData?.Identifier));
        }

        void RefreshSourceFilesInformationUI(BaseAssetData assetData)
        {
            SetPrimaryExtension?.Invoke(assetData.PrimaryExtension);

            var files = assetData.SourceFiles.Where(f =>
            {
                if (string.IsNullOrEmpty(f?.Path))
                    return false;

                return !AssetDataDependencyHelper.IsASystemFile(Path.GetExtension(f.Path));
            }).ToList();


            long totalFileSize = 0;
            var totalFilesCount = 0;
            var incompleteFilesCount = 0;

            var hasFiles = files.Any();

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.FileSize);
                totalFileSize += assetFileSize;
                totalFilesCount += files.Count;
                incompleteFilesCount += files.Count(f => !f.Available);

                m_SourceFilesFoldout.Populate(assetData, files);
            }
            else
            {
                m_SourceFilesFoldout.Clear();
            }

            m_SourceFilesFoldout.StopPopulating();

            RefreshUVCSFilesInformationUI(assetData, out var uvcsTotalFileSize, out var uvcsTotalFilesCount);

            totalFileSize += uvcsTotalFileSize;
            totalFilesCount += uvcsTotalFilesCount;

            if (hasFiles)
            {
                SetFileSize?.Invoke(Utilities.BytesToReadableString(totalFileSize));
                SetFileCount?.Invoke(incompleteFilesCount > 0 ? $"{totalFilesCount} [{incompleteFilesCount} incomplete]" : totalFilesCount.ToString());
            }
            else
            {
                SetFileSize?.Invoke(Utilities.BytesToReadableString(0));
                SetFileCount?.Invoke("0");
            }

            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
            UIElementsUtils.SetDisplay(m_SameFileNamesWarningBox, HasCaseInsensitiveMatch(assetData.SourceFiles.Select(f => f.Path)));
        }

        void RefreshUVCSFilesInformationUI(BaseAssetData assetData, out long totalFileSize, out int totalFilesCount)
        {
            var files = assetData.UVCSFiles?.ToList();

            var hasFiles = files != null && files.Any();

            totalFileSize = 0;
            totalFilesCount = 0;

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.FileSize);
                totalFileSize += assetFileSize;
                totalFilesCount += files.Count;

                m_UVCSFilesFoldout.Populate(assetData, files);
            }
            else
            {
                m_UVCSFilesFoldout.Clear();
            }

            m_UVCSFilesFoldout.StopPopulating();
        }

        void RefreshDependenciesInformationUI(BaseAssetData assetData)
        {
            var dependencies = assetData.Dependencies.ToList();
            m_DependenciesFoldout.Populate(assetData, dependencies);
            UIElementsUtils.SetDisplay(m_NoDependenciesBox, !dependencies.Any());
            m_DependenciesFoldout.StopPopulating();
        }

        void RefreshButtons(BaseAssetData assetData, BaseOperation importOperation)
        {
            var status = AssetDataStatus.GetIStatusFromAssetDataStatusType(assetData?.PreviewStatus?.FirstOrDefault());
            var enabled = UIEnabledStates.CanImport.GetFlag(assetData is AssetData);
            enabled |= UIEnabledStates.InProject.GetFlag(m_AssetDataManager.IsInProject(assetData?.Identifier));
            enabled |= UIEnabledStates.ServicesReachable.GetFlag(m_UnityConnectProxy.AreCloudServicesReachable);
            enabled |= UIEnabledStates.ValidStatus.GetFlag(status == null || !string.IsNullOrEmpty(status.ActionText));
            enabled |= UIEnabledStates.IsImporting.GetFlag(importOperation?.Status == OperationStatus.InProgress);
            enabled |= UIEnabledStates.HasPermissions.GetFlag(false);

            var files = assetData?.SourceFiles?.ToList();
            if (files != null
                && files.Any() // has files
                && !HasCaseInsensitiveMatch(files.Select(f => f.Path)) // files have unique names
                && files.All(file => file.Available)) // files are all available
            {
                enabled |= UIEnabledStates.CanImport;
            }
            else
            {
                enabled &= ~UIEnabledStates.CanImport;
            }

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, assetData, importOperation);
            }

            TaskUtils.TrackException(RefreshButtonsAsync(assetData, importOperation, enabled));
        }

        async Task RefreshButtonsAsync(BaseAssetData assetData, BaseOperation importOperation, UIEnabledStates enabled)
        {
            var hasPermissions = await m_PermissionsManager.CheckPermissionAsync(assetData?.Identifier.OrganizationId, assetData?.Identifier.ProjectId, Constants.ImportPermission);
            enabled |= UIEnabledStates.HasPermissions.GetFlag(hasPermissions);

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, assetData, importOperation);
            }
        }

        static bool HasCaseInsensitiveMatch(IEnumerable<string> files)
        {
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return files.Any(file => !seenFiles.Add(file));
        }
    }
}
