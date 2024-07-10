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
    interface IPageComponent
    {
        void OnSelection(IAssetData assetData, bool isLoading);
        void RefreshUI(IAssetData assetData, bool isLoading = false);
        void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress);
    }

    class AssetDetailsPage : SelectionInspectorPage
    {
        readonly IEnumerable<IPageComponent> m_PageComponents;

        VisualElement m_NoFilesWarningBox;
        VisualElement m_NoDependenciesBox;
        FilesFoldout m_SourceFilesFoldout;
        FilesFoldout m_UVCSFilesFoldout;
        DependenciesFoldout m_DependenciesFoldout;
        IAssetData m_SelectedAssetData;

        int m_TotalFilesCount;
        int m_IncompleteFilesCount;
        long m_TotalFileSize;

        readonly Action<IEnumerable<AssetPreview.IStatus>> PreviewStatusUpdated;
        readonly Action<Texture2D> PreviewImageUpdated;
        readonly Action<string> SetFileCount;
        readonly Action<string> SetFileSize;
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
            footer.CancelOperation += () => m_AssetImporter.CancelImport(m_PageManager.ActivePage.LastSelectedAssetId, true);
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
                "details-source-files-listview", m_AssetDataManager, m_AssetDatabaseProxy,
                Constants.SourceFilesText);

            m_UVCSFilesFoldout = new FilesFoldout(this, "details-uvcs-files-foldout",
                "details-uvcs-files-listview", m_AssetDataManager, m_AssetDatabaseProxy,
                Constants.UVCSFilesText);

            m_DependenciesFoldout = new DependenciesFoldout(this, "details-dependencies-foldout",
                "details-dependencies-listview", m_PageManager, Constants.DependenciesText);

            m_CloseButton = this.Q<Button>("closeButton");

            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr(Constants.NoFilesText);

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

            m_CloseButton.clicked += () => m_PageManager.ActivePage.ClearSelection();

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            m_SelectedAssetData = m_PageManager.ActivePage?.SelectedAssets.Count > 1 ? null : m_AssetDataManager.GetAssetData(m_PageManager.ActivePage?.LastSelectedAssetId);
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            ApplyFilter += OnFilterModified;
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            ApplyFilter -= OnFilterModified;
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(m_SelectedAssetData?.Identifier))
                return;

            RefreshButtons(m_SelectedAssetData, operation);
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(m_SelectedAssetData?.Identifier)
                || operation.Status == OperationStatus.None)
                return;

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this)
                || !args.Added.Concat(args.Updated).Concat(args.Removed).Any(a => a.Equals(m_SelectedAssetData?.Identifier)))
                return;

            // In case of an import, force a full refresh of the displayed information
            TaskUtils.TrackException(SelectAssetDataAsync(new List<IAssetData> { m_SelectedAssetData }));
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
            m_LinksProxy.OpenAssetManagerDashboard(m_SelectedAssetData?.Identifier);
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        async void ImportAssetAsync(string importDestination, IEnumerable<IAssetData> assetsToImport = null)
        {
            try
            {
                var importType = assetsToImport == null ? ImportOperation.ImportType.UpdateToLatest : ImportOperation.ImportType.Import;

                var previouslySelectedAssetData = m_SelectedAssetData;

                // If assets have been targeted for import, we use the first asset as the selected asset
                m_SelectedAssetData = assetsToImport?.FirstOrDefault() ?? m_SelectedAssetData;

                // If no assets have been targeted for import, we use the selected asset from the details page
                assetsToImport ??= m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x));

                var isImporting = await m_AssetImporter.StartImportAsync(assetsToImport.ToList(), importType, importDestination);

                if (!isImporting)
                {
                    m_SelectedAssetData = previouslySelectedAssetData;
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
            m_AssetImporter.ShowInProject(m_SelectedAssetData?.Identifier);

            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Show));
        }

        bool RemoveFromProject()
        {
            AnalyticsSender.SendEvent(new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            try
            {
                return m_AssetImporter.RemoveImport(m_SelectedAssetData?.Identifier, true);
            }
            catch (Exception)
            {
                RefreshButtons(m_SelectedAssetData,
                    m_AssetImporter.GetImportOperation(m_SelectedAssetData?.Identifier));
                throw;
            }
        }

        void RefreshUI(bool isLoading = false)
        {
            if (m_SelectedAssetData == null)
                return;

            // Asynchronously load the new image and update the UI
            TaskUtils.TrackException(m_SelectedAssetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.Identifier))
                    return;

                PreviewImageUpdated?.Invoke(texture2D);
            }));

            foreach (var component in m_PageComponents)
            {
                component.RefreshUI(m_SelectedAssetData, isLoading);
            }

            var operation = m_AssetOperationManager.GetAssetOperation(m_SelectedAssetData.Identifier);

            RefreshButtons(m_SelectedAssetData, operation);

            m_SourceFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_UVCSFilesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
            m_DependenciesFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();

            m_TotalFilesCount = 0;
            m_IncompleteFilesCount = 0;
            m_TotalFileSize = 0;

            RefreshSourceFilesInformationUI(m_SelectedAssetData);
            RefreshUVCSFilesInformationUI(m_SelectedAssetData);
            RefreshDependenciesInformationUI(m_SelectedAssetData);

            var hasFiles = m_TotalFilesCount > 0;
            if (hasFiles)
            {
                SetFileSize?.Invoke(Utilities.BytesToReadableString(m_TotalFileSize));
                SetFileCount?.Invoke(m_IncompleteFilesCount > 0 ? $"{m_TotalFilesCount} [{m_TotalFilesCount} incomplete]" : m_TotalFilesCount.ToString());
            }
            else
            {
                SetFileSize?.Invoke(Utilities.BytesToReadableString(0));
                SetFileCount?.Invoke("0");
            }

            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, !hasFiles);
        }

        protected override async Task SelectAssetDataAsync(List<IAssetData> assetData)
        {
            if (assetData.Count > 1)
            {
                m_SelectedAssetData = null;
                return;
            }

            m_SelectedAssetData = assetData.FirstOrDefault();

            if (m_SelectedAssetData == null)
                return;

            var requiresLoading = !m_AssetDataManager.IsInProject(m_SelectedAssetData.Identifier);

            foreach (var component in m_PageComponents)
            {
                component.OnSelection(m_SelectedAssetData, requiresLoading);
            }

            RefreshUI(requiresLoading);
            RefreshScrollView();

            var tasks = new List<Task>();
            if (requiresLoading)
            {
                UIElementsUtils.Hide(m_NoFilesWarningBox);
                UIElementsUtils.Hide(m_NoDependenciesBox);

                m_SourceFilesFoldout.StartPopulating();
                m_UVCSFilesFoldout.StartPopulating();
                m_DependenciesFoldout.StartPopulating();

                SetFileSize?.Invoke("-");
                SetFileCount?.Invoke("-");

                tasks.Add(m_SelectedAssetData.SyncWithCloudAsync(identifier =>
                {
                    if (!identifier.Equals(m_SelectedAssetData?.Identifier))
                        return;

                    RefreshUI();
                    RefreshScrollView();
                }));
            }

            PreviewStatusUpdated?.Invoke(null);

            tasks.Add(m_SelectedAssetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(m_SelectedAssetData?.Identifier))
                    return;

                PreviewStatusUpdated?.Invoke(status);
            }));

            await TaskUtils.WaitForTasksWithHandleExceptions(tasks);
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
                || !args.Added.Concat(args.Removed).Concat(args.Updated).Any(a => a.Equals(m_SelectedAssetData?.Identifier)))
                return;

            RefreshButtons(m_SelectedAssetData, m_AssetImporter.GetImportOperation(m_SelectedAssetData?.Identifier));
        }

        void RefreshSourceFilesInformationUI(IAssetData assetData)
        {
            var files = assetData.SourceFiles.Where(f =>
            {
                if (string.IsNullOrEmpty(f?.Path))
                    return false;

                return !AssetDataDependencyHelper.IsASystemFile(Path.GetExtension(f.Path));
            }).ToList();

            var hasFiles = files.Any();

            if (hasFiles)
            {
                var assetFileSize = files.Sum(i => i.FileSize);
                m_TotalFileSize += assetFileSize;
                m_TotalFilesCount += files.Count;
                m_IncompleteFilesCount += files.Count(f => !f.Available);

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
            var status = assetData?.PreviewStatus.FirstOrDefault();
            var enabled = UIEnabledStates.CanImport.GetFlag(assetData is AssetData);
            enabled |= UIEnabledStates.InProject.GetFlag(m_AssetDataManager.IsInProject(assetData?.Identifier));
            enabled |= UIEnabledStates.ServicesReachable.GetFlag(m_UnityConnectProxy.AreCloudServicesReachable);
            enabled |= UIEnabledStates.ValidStatus.GetFlag(status == null || !string.IsNullOrEmpty(status.ActionText));
            enabled |= UIEnabledStates.IsImporting.GetFlag(importOperation?.Status == OperationStatus.InProgress);
            enabled |= UIEnabledStates.HasPermissions.GetFlag(false);

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, assetData, importOperation);
            }

            TaskUtils.TrackException(RefreshButtonsAsync(assetData, importOperation, enabled));
        }

        async Task RefreshButtonsAsync(IAssetData assetData, BaseOperation importOperation, UIEnabledStates enabled)
        {
            var hasPermissions = await m_PermissionsManager.CheckPermissionAsync(assetData?.Identifier.OrganizationId, assetData?.Identifier.ProjectId, Constants.ImportPermission);
            enabled |= UIEnabledStates.HasPermissions.GetFlag(hasPermissions);

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, assetData, importOperation);
            }
        }
    }
}
