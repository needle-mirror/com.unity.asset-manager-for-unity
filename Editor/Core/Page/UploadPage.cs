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

        [SerializeField]
        List<string> m_AssetGuidSelection = new();

        static readonly string k_PopupUssClassName = "upload-page-settings-popup";

        IUploadManager m_UploadManager;
        IReadOnlyCollection<AssetIdentifier> m_SelectionToRestore;

        Button m_UploadAssetsButton;
        Button m_CancelUploadButton;
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

            m_UploadContext.IgnoredAssetGuids.Clear();
            m_UploadContext.DependencyAssetGuids.Clear();
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
                    m_UploadContext.IgnoredAssetGuids.Remove(uploadAssetData.Guid);
                }
                else
                {
                    m_UploadContext.IgnoredAssetGuids.Add(uploadAssetData.Guid);
                }

                uploadAssetData.IsIgnored = !checkState;
                ServicesContainer.instance.Resolve<IPageManager>().ActivePage.Clear(true, true);
                UpdateButtonsState();
            }
        }

        public void AddAssets(List<Object> objects)
        {
            AddAssets(objects.Select(AssetDatabase.GetAssetPath).Select(AssetDatabase.AssetPathToGUID), false);
        }

        public void RefreshSelection(IEnumerable<string> importedAssets, IEnumerable<string> deletedAssets)
        {
            if (m_AssetGuidSelection.Count == 0)
                return;

            var dirty = false;

            // Make sure to remove any deleted assets from the selection
            foreach (var assetPath in deletedAssets)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);

                if (!m_AssetGuidSelection.Contains(guid))
                    continue;

                m_AssetGuidSelection.Remove(guid);
                dirty = true;
            }

            // If any of the imported assets are part of the selection, mark the page as dirty
            if (!dirty && importedAssets.Select(AssetDatabase.AssetPathToGUID).Any(guid => m_AssetGuidSelection.Contains(guid)))
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
                m_AssetGuidSelection.Clear();
            }

            foreach (var assetGuid in assetGuids)
            {
                if (m_AssetGuidSelection.Contains(assetGuid))
                    continue;

                m_AssetGuidSelection.Add(assetGuid);
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

            var allAssetGuids = ProcessAssetGuids(m_AssetGuidSelection, out var mainAssetGuids);

            var uploadAssetEntries = GenerateAssetEntries(allAssetGuids, m_UploadContext.EmbedDependencies, m_UploadContext.IgnoredAssetGuids).ToList();

            foreach (var uploadAssetEntry in uploadAssetEntries.Where(uae => m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid)))
            {
                uploadAssetEntry.IsIgnored = true;
            }

            m_UploadContext.SetUploadAssetEntries(uploadAssetEntries);

            var uploadAssetData = new List<UploadAssetData>();
            foreach (var uploadEntry in uploadAssetEntries)
            {
                var isADependency = !mainAssetGuids.Contains(uploadEntry.Guid);
                if (isADependency && !m_UploadContext.DependencyAssetGuids.Contains(uploadEntry.Guid))
                {
                    m_UploadContext.DependencyAssetGuids.Add(uploadEntry.Guid);
                }

                var assetData = new UploadAssetData(uploadEntry, m_UploadContext.Settings, isADependency);
                uploadAssetData.Add(assetData);
            }

            // Sort the result before displaying it
            foreach (var assetData in uploadAssetData.OrderBy(a => a.IsADependency)
                         .ThenByDescending(a => a.PrimaryExtension))
            {
                yield return assetData;
            }

            m_CanLoadMoreItems = false;

            await Task.CompletedTask; // Remove warning about async
        }

        IEnumerable<string> ProcessAssetGuids(IEnumerable<string> assetGuids, out IList<string> mainAssets)
        {
            var processedGuids = new HashSet<string>();

            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    foreach (var subAssetGuid in AssetDatabaseProxy.GetAssetsInFolder(assetPath))
                    {
                        processedGuids.Add(subAssetGuid);
                    }
                }
                else
                {
                    processedGuids.Add(assetGuid);
                }
            }

            mainAssets = processedGuids.Where(IsInsideAssetsFolder).ToList();

            if (m_UploadContext.EmbedDependencies)
            {
                return mainAssets;
            }

            foreach (var assetGuid in AssetDatabaseProxy.GetAssetDependencies(processedGuids))
            {
                if (processedGuids.Contains(assetGuid))
                    continue;

                processedGuids.Add(assetGuid);
            }

            return processedGuids.Where(IsInsideAssetsFolder);
        }

        bool IsInsideAssetsFolder(string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return assetPath.ToLower().StartsWith("assets/");
        }

        void CreateSettingsPopup()
        {
            m_SettingsPopup = new VisualElement();
            m_SettingsPopup.AddToClassList(k_PopupUssClassName);

            var uploadModeDropdown = new DropdownField("Upload Mode")
            {
                choices = Enum.GetNames(typeof(AssetUploadMode)).Select(ObjectNames.NicifyVariableName).Where(mode => mode != AssetUploadMode.None.ToString()).ToList(),
                index = (int)m_UploadContext.Settings.AssetUploadMode,
                tooltip = GetUploadModeTooltip(m_UploadContext.Settings.AssetUploadMode)
            };

            uploadModeDropdown.RegisterValueChangedCallback(v =>
            {
                var value = (AssetUploadMode)uploadModeDropdown.index;
                m_UploadContext.Settings.AssetUploadMode = value;
                uploadModeDropdown.tooltip = GetUploadModeTooltip(value);
                Reload();
            });

            m_SettingsPopup.Add(uploadModeDropdown);

            var dependenciesAsAssetsToggle = new Toggle("Embed dependencies")
            {
                value = m_UploadContext.EmbedDependencies,
                tooltip = L10n.Tr("If enabled, all assets will have its dependencies embedded in a single Cloud Asset. If disabled, each asset and its dependencies will be uploaded as separate Cloud Asset")
            };

            dependenciesAsAssetsToggle.AddToClassList("unity-page-settings-popup-dependencies");
            dependenciesAsAssetsToggle.RegisterValueChangedCallback(v =>
            {
                m_UploadContext.EmbedDependencies = v.newValue;
                Reload();
            });

            m_SettingsPopup.Add(dependenciesAsAssetsToggle);
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
            if (!m_UploadContext.IgnoredAssetGuids.Any() ||
                m_UploadContext.IgnoredAssetGuids.TrueForAll(ignoreGuid => !m_UploadContext.DependencyAssetGuids.Contains(ignoreGuid)) ||
                ServicesContainer.instance.Resolve<IEditorUtilityProxy>().DisplayDialog(L10n.Tr(Constants.IgnoreDependenciesDialogTitle),
                    L10n.Tr(Constants.IgnoreDependenciesDialogMessage),
                    L10n.Tr("Continue"), L10n.Tr("Cancel")))
            {
                var nonIgnoredAssetEntries = m_UploadContext.UploadAssetEntries.Where(uae => !uae.IsIgnored).ToList();
                if (nonIgnoredAssetEntries.Any())
                {
                    Utilities.DevLog($"Uploading {nonIgnoredAssetEntries.Count} assets...");
                    TaskUtils.TrackException(UploadAssetEntries());
                }
                else
                {
                    Utilities.DevLog("No assets to upload");
                }
            }
        }

        async Task UploadAssetEntries()
        {
            IReadOnlyCollection<IUploadAssetEntry> uploadEntries = m_UploadContext.UploadAssetEntries.Where(uae => !uae.IsIgnored).ToList();

            var task = UploadManager.UploadAsync(uploadEntries, m_UploadContext.Settings);

            m_UploadAssetsButton.SetEnabled(false);
            m_UploadAssetsButton.text = L10n.Tr(Constants.UploadingText);
            UpdateCancelButtonLabel();

            AnalyticsSender.SendEvent(new UploadEvent(uploadEntries.Count,
                uploadEntries.SelectMany(e => e.Files).Select(f => Path.GetExtension(f).Substring(1)).Where(ext => ext != "meta").ToArray(),
                m_UploadContext.EmbedDependencies,
                !string.IsNullOrEmpty(m_UploadContext.CollectionPath),
                m_UploadContext.Settings.AssetUploadMode));

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
            SetMessageData(uploadErrorMessage, RecommendedAction.OpenAssetManagerDocumentationPage, false);
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

            m_CancelUploadButton = new Button(() =>
            {
                if (UploadManager.IsUploading)
                {
                    UploadManager.CancelUpload();
                }
                else
                {
                    m_AssetGuidSelection.Clear();
                    Reload();
                }

                UpdateCancelButtonLabel();
            });
            UpdateCancelButtonLabel();
            actionsSection.Add(m_CancelUploadButton);

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

        static string GetUploadModeTooltip(AssetUploadMode mode)
        {
            return mode switch
            {
                AssetUploadMode.Duplicate => L10n.Tr("Uploads new assets and potentially duplicates without checking for existing matches"),

                AssetUploadMode.Override => L10n.Tr("Replaces and overrides any existing asset with the same id on the cloud"),

                AssetUploadMode.Ignore => L10n.Tr("Ignores and skips the upload if an asset with the same id already exists on the cloud"),

                _ => null
            };
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

            m_CancelUploadButton?.SetEnabled(m_UploadContext.UploadAssetEntries.Count > 0);
        }

        async Task UpdateUploadButtonAsync()
        {
            string tooltip;
            var permissionsManager = ServicesContainer.instance.Resolve<IPermissionsManager>();
            var hasUploadPermission = await permissionsManager.CheckPermissionAsync(m_UploadContext.Settings.OrganizationId, m_UploadContext.ProjectId, Constants.UploadPermission);
            var allAssetsIgnored = m_UploadContext.UploadAssetEntries.Count > 0 && m_UploadContext.UploadAssetEntries.All(uae => uae.IsIgnored);

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
            else if (m_UploadContext.UploadAssetEntries.Count == 0)
            {
                tooltip = L10n.Tr(Constants.UploadNoAssetsTooltip);
            }
            else if (m_UploadContext.Settings.AssetUploadMode == AssetUploadMode.Ignore)
            {
                tooltip = L10n.Tr(Constants.UploadWaitStatusTooltip);
                TaskUtils.TrackException(UpdateUploadButtonIgnoreModeAsync());
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
            bool hasNonExistingAssets = false;
            foreach (var uploadAssetEntry in m_UploadContext.UploadAssetEntries)
            {
                var existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(uploadAssetEntry.Guid,
                    m_UploadContext.Settings.OrganizationId, m_UploadContext.Settings.ProjectId, CancellationToken.None);

                if (existingAsset == null && !uploadAssetEntry.IsIgnored)
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
            m_CancelUploadButton.text = UploadManager.IsUploading ? L10n.Tr(Constants.CancelUploadActionText) : L10n.Tr(Constants.ClearAllActionText);
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

        static IEnumerable<IUploadAssetEntry> GenerateAssetEntries(IEnumerable<string> mainAssetGuids, bool bundleDependencies, List<string> ignoredGuids)
        {
            var processedGuids = new HashSet<string>();

            var uploadEntries = new List<IUploadAssetEntry>();

            foreach (var assetGuid in mainAssetGuids)
            {
                if (processedGuids.Contains(assetGuid))
                    continue;

                uploadEntries.Add(new AssetUploadEntry(assetGuid, bundleDependencies, ignoredGuids));
                processedGuids.Add(assetGuid);
            }

            return uploadEntries;
        }
    }
}
