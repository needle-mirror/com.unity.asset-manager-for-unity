using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    class UploadAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // If an asset was modified, we need to refresh the upload page so any changes are reflected if those assets are being prepared for uploaded.
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
            if (pageManager?.ActivePage is UploadPage uploadPage)
            {
                uploadPage.RefreshSelection(importedAssets, deletedAssets);
            }
        }
    }

    [Serializable]
    class UploadPage : BasePage
    {
        [SerializeField]
        UploadContext m_UploadContext = new();

        static readonly string k_PopupUssClassName = "upload-page-settings-popup";

        IUploadManager m_UploadManager;
        IReadOnlyCollection<AssetIdentifier> m_SelectionToRestore;

        Button m_UploadAssetsButton;
        Button m_ClearUploadButton;
        VisualElement m_SettingsPopup;

        IUploadManager UploadManager
        {
            get
            {
                if (m_UploadManager == null)
                {
                    m_UploadManager = ServicesContainer.instance.Resolve<IUploadManager>();
                }

                return m_UploadManager;
            }
        }

        public override bool DisplaySearchBar => false;
        public override bool DisplayBreadcrumbs => true;
        public override bool DisplayFilters => false;
        public override bool DisplaySettings => true;

        public UploadPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider, pageManager) { }

        [MenuItem("Assets/Upload to Asset Manager", false, 21)]
        static void UploadToAssetManagerMenuItem()
        {
            var windowHook = new AssetManagerWindowHook();
            windowHook.OrganizationLoaded += LoadUploadPage;
            windowHook.OpenAssetManagerWindow();
        }

        [MenuItem("Assets/Upload to Asset Manager", true, 21)]
        static bool UploadToAssetManagerMenuItemValidation()
        {
            return Selection.assetGUIDs is { Length: > 0 } && Selection.activeObject != null;
        }

        static void LoadUploadPage()
        {
            AssetManagerWindow.Instance.Focus();

            var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            if (string.IsNullOrEmpty(provider.SelectedOrganization?.Id))
                return;

            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

            if (pageManager.ActivePage is not UploadPage)
            {
                pageManager.SetActivePage<UploadPage>();
            }

            var uploadPage = pageManager.ActivePage as UploadPage;
            uploadPage?.AddAssets(Selection.assetGUIDs);
        }

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>();
        }

        public override void OnActivated()
        {
            base.OnActivated();

            m_UploadContext.SetOrganizationInfo(m_ProjectOrganizationProvider.SelectedOrganization);
            m_UploadContext.SetProjectId(m_ProjectOrganizationProvider.SelectedProject?.Id);
            m_UploadContext.SetCollectionPath(m_ProjectOrganizationProvider.SelectedCollection?.GetFullPath());

            m_UploadContext.ClearAll();
        }

        public override void OpenSettings(VisualElement target)
        {
            if (m_SettingsPopup == null)
            {
                CreateSettingsPopup();
            }

            target.Add(m_SettingsPopup);
        }

        public override void ToggleAsset(IAssetData assetData, bool checkState)
        {
            if (assetData is UploadAssetData uploadAssetData)
            {
                if (checkState)
                {
                    m_UploadContext.RemoveFromIgnoreList(uploadAssetData.Guid);
                }
                else
                {
                    m_UploadContext.AddToIgnoreList(uploadAssetData.Guid);
                }

                uploadAssetData.IsIgnored = !checkState;
                Reload();
                UpdateButtonsState();
            }
        }

        public void AddAssets(List<Object> objects)
        {
            AddAssets(objects.Select(AssetDatabase.GetAssetPath).Select(AssetDatabase.AssetPathToGUID), false);
        }

        public void RefreshSelection(IEnumerable<string> importedAssets, IEnumerable<string> deletedAssets)
        {
            if (m_UploadContext.IsEmpty())
                return;

            var dirty = false;

            // Make sure to remove any deleted assets from the selection
            foreach (var assetPath in deletedAssets)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);

                if (!m_UploadContext.RemoveFromSelection(guid))
                    continue;

                dirty = true;
            }

            // If any of the imported assets are part of the selection, mark the page as dirty
            if (!dirty && importedAssets.Select(AssetDatabase.AssetPathToGUID).Any(guid => m_UploadContext.IsSelected(guid)))
            {
                dirty = true;
            }

            if (dirty)
            {
                Reload();
            }
        }

        void AddAssets(IEnumerable<string> assetGuids, bool clear = true)
        {
            if (clear)
            {
                m_UploadContext.ClearAll();
            }

            foreach (var assetGuid in assetGuids)
            {
                m_UploadContext.AddToSelection(assetGuid);
            }

            Reload();
        }

        void Reload()
        {
            m_SelectionToRestore = SelectedAssets.ToList();
            Clear(true);
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog("Analysing Selection for upload to cloud...");

            var allGuids = m_UploadContext.ResolveFullAssetSelection();

            var uploadAssets = UploadAssetStrategy.GenerateUploadAssets(allGuids,
                m_UploadContext.IgnoredAssetGuids, m_UploadContext.Settings.DependencyMode, m_UploadContext.Settings.FilePathMode).ToList();

            m_UploadContext.SetUploadAssetEntries(uploadAssets);

            var uploadAssetData = new List<UploadAssetData>();
            foreach (var uploadAsset in uploadAssets)
            {
                var isIgnored = m_UploadContext.IgnoredAssetGuids.Contains(uploadAsset.Guid);
                var isDependency = m_UploadContext.IsDependency(uploadAsset.Guid);

                var assetData = new UploadAssetData(uploadAsset, m_UploadContext.Settings)
                {
                    IsIgnored = isIgnored,
                    IsDependency = isDependency
                };

                uploadAssetData.Add(assetData);
            }

            // Sort the result before displaying it
            foreach (var assetData in uploadAssetData.OrderBy(a => a.IsDependency)
                         .ThenByDescending(a => a.PrimaryExtension))
            {
                yield return assetData;
            }

            m_CanLoadMoreItems = false;

            await Task.CompletedTask; // Remove warning about async
        }

        void CreateSettingsPopup()
        {
            m_SettingsPopup = new VisualElement();
            m_SettingsPopup.AddToClassList(k_PopupUssClassName);

            // Upload Mode
            var uploadModeDropdown = CreateEnumDropdown("Upload Mode", m_UploadContext.Settings.UploadMode,
                mode => { m_UploadContext.Settings.UploadMode = mode; }, UploadSettings.GetUploadModeTooltip);

            m_SettingsPopup.Add(uploadModeDropdown);

            // Dependency Mode
            var dependencyModeDropdown = CreateEnumDropdown("Dependencies", m_UploadContext.Settings.DependencyMode,
                mode => { m_UploadContext.Settings.DependencyMode = mode; }, UploadSettings.GetDependencyModeTooltip);

            m_SettingsPopup.Add(dependencyModeDropdown);

            // File Paths Mode
            var filePathModeDropdown = CreateEnumDropdown("File Paths", m_UploadContext.Settings.FilePathMode,
                mode => { m_UploadContext.Settings.FilePathMode = mode; }, UploadSettings.GetFilePathModeTooltip);

            m_SettingsPopup.Add(filePathModeDropdown);
        }

        DropdownField CreateEnumDropdown<TEnum>(string name, TEnum defaultValue, Action<TEnum> onValueChanged, Func<TEnum, string> tooltipProvider) where TEnum : Enum
        {
            var dropDown = new DropdownField(name)
            {
                choices = Enum.GetNames(typeof(TEnum)).Select(ObjectNames.NicifyVariableName).ToList(),
                index = (int)Enum.ToObject(typeof(TEnum), defaultValue),
                tooltip = tooltipProvider.Invoke(defaultValue)
            };

            dropDown.RegisterValueChangedCallback(v =>
            {
                var value = (TEnum)Enum.ToObject(typeof(TEnum), dropDown.index);
                dropDown.tooltip = tooltipProvider.Invoke(value);
                onValueChanged?.Invoke(value);
                Reload();
            });

            return dropDown;
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            if (m_ProjectOrganizationProvider.SelectedProject == null)
            {
                // If no project is selected (coming from AllAssets or AM window never was opened), show select project message
                SetMessageData(MissingSelectedProjectErrorData);
            }
            else
            {
                SetMessageData(
                    !m_AssetList.Any() ? L10n.Tr(Constants.UploadNoAssetsMessage) : string.Empty, RecommendedAction.None);
            }

            if (m_SelectionToRestore != null)
            {
                // Some selected asset might not exist anymore in this page, so we remove them.
                var parsedSelection = m_SelectionToRestore.Where(identifier => m_AssetList.Exists(assetData => assetData.Identifier == identifier));
                m_SelectionToRestore = null;

                SelectAssets(parsedSelection);
            }
        }

        protected override void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            // Stay in upload page

            m_UploadContext.SetProjectId(projectInfo?.Id);
            m_UploadContext.SetCollectionPath(collectionInfo?.GetFullPath());
        }

        public void UploadAssets()
        {
            var hasIgnoredAssets = m_UploadContext.IgnoredAssetGuids.Count > 0;
            var hasIgnoredDependencies = hasIgnoredAssets && m_UploadContext.HasIgnoredDependencies();

            if (hasIgnoredDependencies)
            {
                var userWantsToUploadWithMissingDependencies = ServicesContainer.instance.Resolve<IEditorUtilityProxy>()
                    .DisplayDialog(L10n.Tr(Constants.IgnoreDependenciesDialogTitle),
                        L10n.Tr(Constants.IgnoreDependenciesDialogMessage), L10n.Tr("Continue"), L10n.Tr("Cancel"));

                if (!userWantsToUploadWithMissingDependencies)
                    return;
            }

            var assetsToUpload = hasIgnoredAssets
                ? m_UploadContext.UploadAssets.Where(uae => !m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid))
                : m_UploadContext.UploadAssets;

            if (assetsToUpload.Any())
            {
                Utilities.DevLog($"Uploading assets...");
                TaskUtils.TrackException(UploadAssetEntries());
            }
            else
            {
                Debug.LogError("No assets to upload");
            }
        }

        async Task UploadAssetEntries()
        {
            IReadOnlyCollection<IUploadAsset> uploadEntries = m_UploadContext.UploadAssets.Where(uae => !m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid)).ToList();

            var task = UploadManager.UploadAsync(uploadEntries, m_UploadContext.Settings);

            m_UploadAssetsButton.SetEnabled(false);
            m_UploadAssetsButton.text = L10n.Tr(Constants.UploadingText);
            UpdateCancelButtonLabel();

            AnalyticsSender.SendEvent(new UploadEvent(uploadEntries.Count,
                uploadEntries.SelectMany(e => e.Files).Select(f => Path.GetExtension(f.SourcePath)[1..]).Where(ext => ext != "meta").ToArray(),
                !string.IsNullOrEmpty(m_UploadContext.CollectionPath), m_UploadContext.Settings));

            try
            {
                await task;
            }
            catch (Exception e)
            {
                // All Exception are logged in the IUploaderManager
                // If it happens to be a ServiceClientException, we provide visual feedback to user
                if (e is ServiceClientException serviceClientException)
                {
                    DisplayErrorMessageFromServiceClientException(serviceClientException);
                }
            }
            finally
            {
                m_UploadAssetsButton.SetEnabled(true);
                m_UploadAssetsButton.text = L10n.Tr(Constants.UploadActionText);

                if (task.IsCompletedSuccessfully)
                {
                    UpdateCancelButtonLabel();
                    GoBackToCollectionPage();
                }
            }
        }

        void DisplayErrorMessageFromServiceClientException(ServiceClientException serviceClientException)
        {
            var uploadErrorMessage = $"Upload operation was refused. {serviceClientException.Detail}.";
            SetMessageData(uploadErrorMessage, RecommendedAction.OpenAssetManagerDocumentationPage,
                false, HelpBoxMessageType.Error);
        }

        public override VisualElement CreateCustomUISection()
        {
            var root = new VisualElement();
            root.AddToClassList("upload-page-custom-section");

            var settings = new VisualElement();
            settings.AddToClassList("upload-page-all-settings-section");

            root.Add(settings);

            var actions = new VisualElement();
            actions.AddToClassList("upload-page-all-actions-section");

            var selectionSection = new VisualElement();
            selectionSection.AddToClassList("upload-page-selection-section");

            actions.Add(selectionSection);

            var actionsSection = new VisualElement();
            actionsSection.AddToClassList("upload-page-action-section");

            actions.Add(actionsSection);

            root.Add(actions);

            m_ClearUploadButton = new Button(() =>
            {
                if (UploadManager.IsUploading)
                {
                    UploadManager.CancelUpload();
                }
                else
                {
                    m_UploadContext.ClearSelection();
                    Reload();
                }

                UpdateCancelButtonLabel();
            });
            UpdateCancelButtonLabel();
            actionsSection.Add(m_ClearUploadButton);

            m_UploadAssetsButton = new Button(UploadAssets) { text = L10n.Tr(Constants.UploadActionText) };
            actionsSection.Add(m_UploadAssetsButton);

            m_UploadContext.ProjectIdChanged += () =>
            {
                UpdateButtonsState();
                Reload();
            };

            m_UploadContext.UploadAssetEntriesChanged += UpdateButtonsState;

            UpdateButtonsState();

            return root;
        }

        void UpdateButtonsState()
        {
            if (m_UploadAssetsButton != null)
            {
                var areCloudServicesReachable = ServicesContainer.instance.Resolve<IUnityConnectProxy>().AreCloudServicesReachable;

                m_UploadAssetsButton.SetEnabled(false);

                if (!areCloudServicesReachable)
                {
                    m_UploadAssetsButton.tooltip = L10n.Tr(Constants.UploadCloudServicesNotReachableTooltip);
                }
                else
                {
                    m_UploadAssetsButton.tooltip = L10n.Tr(Constants.UploadWaitStatusTooltip);
                    TaskUtils.TrackException(UpdateUploadButtonAsync());
                }
            }

            m_ClearUploadButton?.SetEnabled(m_UploadContext.UploadAssets.Count > 0);
        }

        async Task UpdateUploadButtonAsync()
        {
            string tooltip;
            var permissionsManager = ServicesContainer.instance.Resolve<IPermissionsManager>();
            var hasUploadPermission = await permissionsManager.CheckPermissionAsync(m_UploadContext.Settings.OrganizationId, m_UploadContext.ProjectId, Constants.UploadPermission);
            var allAssetsIgnored = m_UploadContext.UploadAssets.Count > 0 && m_UploadContext.UploadAssets.All(uae => m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid));
            var anyOutsideProject = m_UploadContext.UploadAssets.Any(uae =>m_UploadContext.UploadAssets.Any(ua => ua.Files.Any(f => f.IsDestinationOutsideProject) && !m_UploadContext.IgnoredAssetGuids.Contains(ua.Guid)));

            if (!hasUploadPermission)
            {
                tooltip = L10n.Tr(Constants.UploadNoPermissionTooltip);
            }
            else if (allAssetsIgnored)
            {
                tooltip = L10n.Tr(Constants.UploadAllIgnoredTooltip);
            }
            else if (string.IsNullOrEmpty(m_UploadContext.ProjectId))
            {
                tooltip = L10n.Tr(Constants.UploadNoProjectSelectedTooltip);
            }
            else if (m_UploadContext.UploadAssets.Count == 0)
            {
                tooltip = L10n.Tr(Constants.UploadNoAssetsTooltip);
            }
            else if (m_UploadContext.Settings.UploadMode == UploadAssetMode.SkipExisting)
            {
                tooltip = L10n.Tr(Constants.UploadWaitStatusTooltip);
                TaskUtils.TrackException(UpdateUploadButtonIgnoreModeAsync());
            }
            else if (anyOutsideProject)
            {
                tooltip = L10n.Tr(Constants.UploadOutsideProjectTooltip);
            }
            else
            {
                m_UploadAssetsButton.SetEnabled(true);
                tooltip = L10n.Tr(Constants.UploadAssetsTooltip);
            }

            m_UploadAssetsButton.tooltip = tooltip;
        }

        async Task<string> UpdateUploadButtonIgnoreModeAsync()
        {
            var hasNonExistingAssets = false;
            var uploadAssets = m_UploadContext.UploadAssets.ToList(); // Make a copy of the selection in case it changes while we're checking

            foreach (var assetGuid in uploadAssets.Select(u => u.Guid))
            {
                var existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(assetGuid,
                    m_UploadContext.Settings.OrganizationId, m_UploadContext.Settings.ProjectId, CancellationToken.None);

                if (existingAsset == null && !m_UploadContext.IgnoredAssetGuids.Contains(assetGuid))
                {
                    hasNonExistingAssets = true;
                    break;
                }
            }

            if (hasNonExistingAssets)
            {
                m_UploadAssetsButton.SetEnabled(true);
                return L10n.Tr(Constants.UploadAssetsTooltip);
            }

            return L10n.Tr(Constants.UploadAssetsExistsTooltip);
        }

        void UpdateCancelButtonLabel()
        {
            m_ClearUploadButton.text = UploadManager.IsUploading ? L10n.Tr(Constants.CancelUploadActionText) : L10n.Tr(Constants.ClearAllActionText);
        }

        void GoBackToCollectionPage()
        {
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

            if (pageManager == null)
                return;

            if (pageManager.ActivePage != this)
                return;

            pageManager.SetActivePage<CollectionPage>();
        }
    }
}
