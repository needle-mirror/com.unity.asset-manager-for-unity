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
using ImportSettings = Unity.AssetManager.Editor.ImportSettings;

namespace Unity.AssetManager.UI.Editor
{
    interface IPageComponent
    {
        void OnSelection();
        void RefreshUI( bool isLoading = false);
        void RefreshButtons(UIEnabledStates enabled, BaseOperation operationInProgress);
    }

    interface IEditableComponent
    {
        bool IsEditingEnabled { get; }
        void EnableEditing(bool enable);
    }

    class AssetInspector : SelectionInspectorPage
    {
        readonly IEnumerable<IPageComponent> m_PageComponents;
        readonly IEnumerable<IEditableComponent> m_EditableComponents;
        readonly AssetInspectorViewModel m_ViewModel = new();

        VisualElement m_NoFilesWarningBox;
        VisualElement m_SameFileNamesWarningBox;
        VisualElement m_NoDependenciesBox;
        FileFoldoutComponent m_FilesFoldoutComponent;

        public AssetInspector(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IUnityConnectProxy unityConnectProxy, IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager, IDialogManager dialogManager, IPopupManager popupManager, ISettingsManager settingsManager)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, unityConnectProxy, projectIconDownloader,
                permissionsManager, dialogManager)
        {
            BuildUxmlDocument();

            m_ViewModel = new AssetInspectorViewModel(assetImporter, linksProxy, assetDataManager,
                assetOperationManager, projectOrganizationProvider, permissionsManager, unityConnectProxy);
            BindViewModelEvents();

            var versionsTab = new AssetInspectorVersionsTab(m_ScrollView.contentContainer, m_DialogManager, m_ViewModel);
            var detailsTab = new AssetInspectorMetadataTab(m_ScrollView.contentContainer, IsAnyFilterActive, m_PageManager, m_StateManager, popupManager, settingsManager, projectOrganizationProvider, unityConnectProxy, m_ViewModel);
            var footer = new AssetInspectorFooter(this, m_DialogManager, m_ViewModel);
            var header = new AssetInspectorHeader(this, assetDataManager, m_ViewModel, footer.ButtonsContainer, detailsTab, versionsTab);

            header.FieldEdited += OnFieldEdited;
            detailsTab.FieldEdited += OnFieldEdited;

            m_PageComponents = new IPageComponent[]
            {
                header,
                footer,
                detailsTab,
                versionsTab,
            };

            m_EditableComponents = new IEditableComponent[]
            {
                header,
                detailsTab
            };

            RefreshUI();
        }

        void BindViewModelEvents()
        {
            m_ViewModel.OperationStateChanged += RefreshButtons;
            m_ViewModel.AssetDataChanged += () => RefreshUI();
            m_ViewModel.AssetDataAttributesUpdated += (attributes) => RefreshButtons();
            m_ViewModel.FilesChanged += RefreshSourceFilesInformationUI;
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

            // Upload metadata container
            m_UploadCustomMetadataContainer = m_ScrollView.Q<VisualElement>("upload-metadata-container");
            m_UploadCustomMetadataContainer.Add(new UploadMetadataContainer(m_PageManager, m_AssetDataManager, m_ProjectOrganizationProvider, m_LinksProxy));

            RefreshUploadMetadataContainer(); // Hide the container by default

            m_FilesFoldoutComponent = new FileFoldoutComponent(m_ScrollView.Q("files-container"), m_StateManager, m_AssetDatabaseProxy);

            m_CloseButton = this.Q<Button>("closeButton");

            m_NoFilesWarningBox = this.Q<VisualElement>("no-files-warning-box");
            m_NoFilesWarningBox.Q<Label>().text = L10n.Tr(Constants.NoFilesText);

            m_SameFileNamesWarningBox = this.Q<VisualElement>("same-files-warning-box");
            m_SameFileNamesWarningBox.Q<Label>().text = L10n.Tr(Constants.SameFileNamesText);

            m_ScrollView.viewDataKey = "details-page-scrollview";

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            m_ViewModel.SelectedAssetData = m_PageManager.ActivePage?.SelectedAssets.Count > 1 ? null : m_AssetDataManager.GetAssetData(m_PageManager.ActivePage?.LastSelectedAssetId);
        }

        protected override void OnAttachToPanel(AttachToPanelEvent evt)
        {
            base.OnAttachToPanel(evt);

            m_CloseButton.clicked += OnCloseButton;
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            base.OnDetachFromPanel(evt);

            m_CloseButton.clicked -= OnCloseButton;
        }

        public override void EnableEditing(bool enable)
        {
            foreach (var editableComponent in m_EditableComponents)
            {
                editableComponent.EnableEditing(enable);
            }
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation) // Only import operation are displayed in the details page
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(m_ViewModel.AssetIdentifier))
                return;

            RefreshButtons(operation);
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if (operation is not ImportOperation and not IndefiniteOperation)
                return;

            if (!UIElementsUtils.IsDisplayed(this)
                || !operation.Identifier.Equals(m_ViewModel.AssetIdentifier)
                || operation.Status == OperationStatus.None)
                return;

            if (m_ViewModel.PreviouslySelectedAssetData != null &&
                operation.Status is OperationStatus.Cancelled or OperationStatus.Error)
            {
                m_ViewModel.SelectedAssetData = m_ViewModel.PreviouslySelectedAssetData;
            }

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this))
                return;

            if (args.Removed.Any(a => a.Equals(m_ViewModel.AssetIdentifier)))
            {
                // Asset was removed from project - refresh the inspector to reflect updated status
                TaskUtils.TrackException(SelectAssetDataAsync(new List<BaseAssetData> { m_ViewModel.SelectedAssetData }));
                return;
            }

            if (!args.Added.Concat(args.Updated).Any(a => a.Equals(m_ViewModel.AssetIdentifier)))
                return;

            // In case of an import, force a full refresh of the displayed information
            TaskUtils.TrackException(SelectAssetDataAsync(new List<BaseAssetData> { m_ViewModel.SelectedAssetData }));
        }

        void OnFieldEdited(AssetFieldEdit assetFieldEdit)
        {
            // Use the asset identifier from the edit to find the correct asset
            // This handles the case where selection changed before the edit was applied
            var assetData = m_AssetDataManager.GetAssetData(assetFieldEdit.AssetIdentifier) as UploadAssetData;

            switch (assetFieldEdit.Field)
            {
                case EditField.Name:
                    assetData?.SetName(assetFieldEdit.EditValue as string);
                    break;
                case EditField.Description:
                    assetData?.SetDescription(assetFieldEdit.EditValue as string);
                    break;
                case EditField.Tags:
                    assetData?.SetTags(assetFieldEdit.EditValue as IEnumerable<string>);
                    break;
                case EditField.Status:
                    assetData?.SetStatus(assetFieldEdit.EditValue as string);
                    break;
            }

            var uploadPage = m_PageManager.ActivePage as UploadPage;
            uploadPage?.OnAssetSelectionEdited(assetFieldEdit);
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        void RefreshUI(bool isLoading = false)
        {
            if (m_ViewModel.SelectedAssetData == null)
                return;

            RefreshPageComponents(isLoading);
            RefreshSourceFilesInformationUI();
            RefreshUploadMetadataContainer();

            if (m_PageManager.ActivePage is not UploadPage)
            {
                var operation = m_AssetOperationManager.GetAssetOperation(m_ViewModel.AssetIdentifier);
                RefreshButtons(operation);
            }
        }

        void RefreshPageComponents(bool isLoading = false)
        {
            if (m_ViewModel.SelectedAssetData == null)
                return;

            foreach (var component in m_PageComponents)
            {
                component.RefreshUI(isLoading);
            }
        }

        protected override async Task SelectAssetDataAsync(IReadOnlyCollection<BaseAssetData> assetData)
        {
            if (assetData == null || assetData.Count > 1)
            {
                m_ViewModel.SelectedAssetData = null;
                return;
            }

            m_ViewModel.SelectedAssetData = assetData.FirstOrDefault();

            if (m_ViewModel.SelectedAssetData == null)
                return;

            // Full refresh for all non-upload assets (including in-project) so linked projects, collections, and dependencies get loaded.
            var requiresLoading = m_ViewModel.SelectedAssetData is not UploadAssetData;

            foreach (var component in m_PageComponents)
            {
                component.OnSelection();
            }

            RefreshUI(requiresLoading);
            RefreshScrollView();

            await m_ViewModel.RefreshInfosAsync(requiresLoading);
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this)
                || !args.Added.Concat(args.Removed).Concat(args.Updated).Any(a => a.Equals(m_ViewModel.AssetIdentifier)))
                return;

            RefreshButtons(m_AssetImporter.GetImportOperation(m_ViewModel.AssetIdentifier));
        }

        void RefreshSourceFilesInformationUI()
        {
            m_FilesFoldoutComponent.RefreshSourceFilesInformationUI(m_ViewModel.AssetDatasets, m_ViewModel.SelectedAssetData);

            var files = m_ViewModel.GetSelectedAssetFiles();
            UIElementsUtils.SetDisplay(m_NoFilesWarningBox, files == null || !files.Any());
            UIElementsUtils.SetDisplay(m_SameFileNamesWarningBox, m_ViewModel.HasCaseInsensitiveMatch(m_ViewModel.GetFiles()?.Select(f => f.Path)));
        }

        void RefreshButtons()
        {
            if (m_PageManager.ActivePage is UploadPage) return;

            RefreshButtons(m_ViewModel.GetAssetOperation());
        }

        void RefreshButtons(BaseOperation importOperation)
        {
            var enabled = m_ViewModel.GetUIEnabledStates();

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, importOperation);
            }

            TaskUtils.TrackException(RefreshButtonsAsync(importOperation, enabled));
        }

        async Task RefreshButtonsAsync(BaseOperation importOperation, UIEnabledStates enabled)
        {
            var hasPermissions = m_ViewModel.IsAssetFromLibrary || await m_ViewModel.CheckPermissionAsync();

            enabled |= UIEnabledStates.HasPermissions.GetFlag(hasPermissions);

            foreach (var component in m_PageComponents)
            {
                component.RefreshButtons(enabled, importOperation);
            }
        }

        bool IsAnyFilterActive()
        {
            var pageFiltersStrategy = m_PageManager?.PageFilterStrategy;
            return pageFiltersStrategy?.SelectedFilters?.Count > 0 || pageFiltersStrategy?.SearchFilters?.Count > 0;
        }
    }
}
