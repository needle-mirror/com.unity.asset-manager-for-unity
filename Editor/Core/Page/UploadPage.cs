using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    // We need the context to be both static and serializable.
    // A ScriptableSingleton is a good way to achieve this.
    class UploadContextScriptableObject : ScriptableSingleton<UploadContextScriptableObject>
    {
        [SerializeField]
        public UploadContext UploadContext = new();
    }

    static partial class UssStyle
    {
        public const string UploadPageSettingsPanel = "upload-page-settings-panel";
        public const string UploadPageCustomSection = "upload-page-custom-section";
        public const string UploadPageActionSection = "upload-page-action-section";
        public const string UploadPageAllActionsSection = "upload-page-all-actions-section";
        public const string UploadPageUploadButton = "upload-page-upload-button";
        public const string UploadPageResetButton = "upload-page-reset-button";
    }

    [Serializable]
    class UploadPage : BasePage
    {
        static readonly float k_UploadSettingPanelWidth = 280f;

        UploadContext m_UploadContext => UploadContextScriptableObject.instance.UploadContext;

        IUploadManager m_UploadManager;
        IReadOnlyCollection<AssetIdentifier> m_SelectionToRestore;

        Button m_UploadAssetsButton;
        Button m_ClearUploadButton;

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
        public override bool DisplayFooter => false;
        public override bool DisplaySort => false;

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
            var uploadManager = ServicesContainer.instance.Resolve<IUploadManager>();

            return Selection.assetGUIDs is { Length: > 0 } &&
                   Selection.activeObject != null &&
                   !uploadManager.IsUploading;
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
                UpdateButtonsState();
                DisplayScalingIssuesHelpBoxIfNecessary();

                InvokeToggleAssetChanged(assetData.Identifier, checkState);
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
            if (m_UploadManager.IsUploading)
            {
                Utilities.DevLog("You cannot add assets during upload.");
            }

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

        VisualElement CreateSettingsPanel()
        {
            var settingsPanel = new VisualElement();
            settingsPanel.AddToClassList(UssStyle.UploadPageSettingsPanel);

            var foldout = new Foldout { text = L10n.Tr(Constants.UploadSettings) };
            foldout.value = m_UploadContext.Settings.SavedUploadSettingsPanelOpened;
            foldout.RegisterValueChangedCallback(evt => m_UploadContext.Settings.SavedUploadSettingsPanelOpened = evt.newValue);
            settingsPanel.Add(foldout);

            var toggle = foldout.Q<Toggle>();
            toggle.focusable = false;

            // Upload Mode
            var uploadModeDropdown = CreateEnumDropdown(L10n.Tr(Constants.UploadMode), m_UploadContext.Settings.UploadMode,
                mode => { m_UploadContext.Settings.UploadMode = mode; }, UploadSettings.GetUploadModeTooltip);

            foldout.Add(uploadModeDropdown);

            // Dependency Mode
            var dependencyModeDropdown = CreateEnumDropdown(L10n.Tr(Constants.Dependencies), m_UploadContext.Settings.DependencyMode,
                mode => { m_UploadContext.Settings.DependencyMode = mode; }, UploadSettings.GetDependencyModeTooltip);

            foldout.Add(dependencyModeDropdown);

            // File Paths Mode
            var filePathModeDropdown = CreateEnumDropdown(L10n.Tr(Constants.FilePaths), m_UploadContext.Settings.FilePathMode,
                mode => { m_UploadContext.Settings.FilePathMode = mode; }, UploadSettings.GetFilePathModeTooltip);

            foldout.Add(filePathModeDropdown);

            // Reset Button
            var resetButton = new Button(() =>
            {
                m_UploadContext.Settings.Reset();
                uploadModeDropdown.index = (int)m_UploadContext.Settings.UploadMode;
                dependencyModeDropdown.index = (int)m_UploadContext.Settings.DependencyMode;
                filePathModeDropdown.index = (int)m_UploadContext.Settings.FilePathMode;
            })
            {
                text = L10n.Tr(Constants.UploadSettingsReset)
            };

            resetButton.AddToClassList(UssStyle.UploadPageResetButton);
            foldout.Add(resetButton);

            return settingsPanel;
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
                if (!DisplayScalingIssuesHelpBoxIfNecessary())
                {
                    SetMessageData(!m_AssetList.Any()
                        ? L10n.Tr(Constants.UploadNoAssetsMessage) : string.Empty, RecommendedAction.None);
                }
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
                        L10n.Tr(Constants.IgnoreDependenciesDialogMessage), L10n.Tr(Constants.Continue), L10n.Tr(Constants.Cancel));

                if (!userWantsToUploadWithMissingDependencies)
                    return;
            }

            var assetsToUpload = hasIgnoredAssets
                ? m_UploadContext.UploadAssets.Where(uae => !m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid))
                : m_UploadContext.UploadAssets;

            if (!CheckDirtyAssets(assetsToUpload))
                return;

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

        bool CheckDirtyAssets(IEnumerable<IUploadAsset> assetsToUpload)
        {
            var dirtyObjects = new List<Object>();
            var dirtyScenes = new List<Scene>();
            foreach (var uploadAsset in assetsToUpload)
            {
                foreach (var path in uploadAsset.Files.Select(f => f.SourcePath))
                {
                    if(Utilities.IsFileDirty(path))
                    {
                        Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                        if (asset is SceneAsset)
                        {
                            dirtyScenes.Add(SceneManager.GetSceneByPath(path));
                        }
                        else
                        {
                            dirtyObjects.Add(asset);
                        }

                        break;
                    }
                }
            }

            if (dirtyObjects.Any() || dirtyScenes.Any())
            {
                var userWantsToUploadWithDirtyAssets = ServicesContainer.instance.Resolve<IEditorUtilityProxy>()
                    .DisplayDialog(L10n.Tr(Constants.DirtyAssetsDialogTitle),
                        L10n.Tr(Constants.DirtyAssetsDialogMessage),
                        L10n.Tr(Constants.DirtyAssetsDialogOk),
                        L10n.Tr(Constants.DirtyAssetsDialogCancel));

                if (!userWantsToUploadWithDirtyAssets)
                    return false;

                foreach (var obj in dirtyObjects)
                {
                    AssetDatabase.SaveAssetIfDirty(obj);
                }

                foreach (var scene in dirtyScenes)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            return true;
        }

        async Task UploadAssetEntries()
        {
            IReadOnlyCollection<IUploadAsset> uploadEntries = m_UploadContext.UploadAssets.Where(uae => !m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid)).ToList();

            // If assets are dirty and needed to save, we need to wait for the save to complete
            // and preview state to be reloaded before continuing
            await WaitForStatusUpdate();

            var task = UploadManager.UploadAsync(uploadEntries, m_UploadContext.Settings);

            m_UploadAssetsButton.SetEnabled(false);
            m_UploadAssetsButton.text = L10n.Tr(Constants.UploadingText);
            UpdateCancelButtonLabel();

            // TODO remove file that have not been uploaded from uploadEntries
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
                if (e is ServiceException serviceException)
                {
                    DisplayErrorMessageFromServiceException(serviceException);
                }
            }
            finally
            {
                m_UploadAssetsButton.SetEnabled(true);
                m_UploadAssetsButton.text = L10n.Tr(Constants.UploadActionText);

                if (task.IsCompletedSuccessfully)
                {
                    m_UploadContext.ClearAll();
                    UpdateCancelButtonLabel();
                    GoBackToCollectionPage();
                }
            }
        }

        async Task WaitForStatusUpdate()
        {
            var preventFailureCounter = 1000;
            while((m_AssetList == null || !m_AssetList.Any()) && preventFailureCounter > 0)
            {
                preventFailureCounter--;
                await Task.Delay(1);
            }

            var uploadAssetData = m_AssetList.OfType<UploadAssetData>().ToList();

            while (preventFailureCounter > 0)
            {
                preventFailureCounter--;
                if (uploadAssetData.Exists(uad => !uad.HasAnExistingStatus))
                {
                    await Task.Delay(1);
                }
                else
                {
                    break;
                }
            }
        }

        void DisplayErrorMessageFromServiceException(ServiceException exp)
        {
            var expDetail = string.IsNullOrEmpty(exp.Detail) ? exp.Message : exp.Detail;
            var uploadErrorMessage = $"Upload operation failed : [{exp.StatusCode}] {expDetail}";
            SetMessageData(uploadErrorMessage, RecommendedAction.OpenAssetManagerDocumentationPage,
                false, HelpBoxMessageType.Error);
        }

        public override VisualElement CreateCustomUISection()
        {
            var root = new VisualElement();
            root.AddToClassList(UssStyle.UploadPageCustomSection);

            var actions = new VisualElement();
            actions.AddToClassList(UssStyle.UploadPageAllActionsSection);

            var actionsSection = new VisualElement();
            actionsSection.AddToClassList(UssStyle.UploadPageActionSection);

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
            m_UploadAssetsButton.AddToClassList(UssStyle.UploadPageUploadButton);
            actionsSection.Add(m_UploadAssetsButton);

            m_UploadContext.ProjectIdChanged += () =>
            {
                UpdateButtonsState();
                Reload();
            };

            m_UploadContext.UploadAssetEntriesChanged += UpdateButtonsState;

            UpdateButtonsState();

            var settingsPanel = CreateSettingsPanel();
            settingsPanel.style.width = k_UploadSettingPanelWidth;
            root.RegisterCallback<GeometryChangedEvent>( evt =>
            {
                if (evt.newRect.width < k_UploadSettingPanelWidth + settingsPanel.resolvedStyle.marginRight)
                {
                    settingsPanel.style.width = evt.newRect.width - settingsPanel.resolvedStyle.marginRight;
                }
                else
                {
                    settingsPanel.style.width = k_UploadSettingPanelWidth;
                }
            });

            root.Add(settingsPanel);

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

        bool DisplayScalingIssuesHelpBoxIfNecessary()
        {
            var numberOfAssetsToUpload = m_UploadContext.UploadAssets.Count(x =>
                !m_UploadContext.IgnoredAssetGuids.Contains(x.Guid));

            if (numberOfAssetsToUpload > Constants.ScalingIssuesThreshold)
            {
                SetMessageData(L10n.Tr(Constants.ScalingIssuesMessage),
                    RecommendedAction.None, false, HelpBoxMessageType.Warning);

                return true;
            }

            SetMessageData(string.Empty, RecommendedAction.None);

            return false;
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
            else if (anyOutsideProject)
            {
                tooltip = L10n.Tr(Constants.UploadOutsideProjectTooltip);
            }
            else
            {
                await WaitForStatusUpdate();
                var uploadAssetData = m_AssetList.OfType<UploadAssetData>();
                var allAssetSkipped = m_UploadContext.UploadAssets.Count > 0 && uploadAssetData.All(uad => uad.IsIgnored || uad.IsSkipped);
                if (allAssetSkipped)
                {
                    tooltip = L10n.Tr(Constants.UploadAllSkippedTooltip);
                }
                else
                {
                    m_UploadAssetsButton.SetEnabled(true);
                    tooltip = L10n.Tr(Constants.UploadAssetsTooltip);
                }
            }

            m_UploadAssetsButton.tooltip = tooltip;
        }

        async Task<string> UpdateUploadButtonIgnoreModeAsync()
        {
            switch (m_UploadContext.Settings.UploadMode)
            {
                case UploadAssetMode.SkipIdentical:
                    return await UpdateUploadButtonNewVersionModeAsync();
                default:
                    return string.Empty;
            }
        }

        async Task<string> UpdateUploadButtonNewVersionModeAsync()
        {
            var uploadAssets =
                m_UploadContext.UploadAssets
                    .ToList(); // Make a copy of the selection in case it changes while we're checking

            // Check if it contains any non-existing assets
            var existingAssets = new List<(IUploadAsset, IAssetData)>();
            foreach (var uploadAsset in uploadAssets)
            {
                var assetGuid = uploadAsset.Guid;
                var existingAsset = await AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(assetGuid,
                    m_UploadContext.Settings.OrganizationId, m_UploadContext.Settings.ProjectId,
                    CancellationToken.None);

                if (!m_UploadContext.IgnoredAssetGuids.Contains(assetGuid))
                {
                    if (existingAsset == null)
                    {
                        m_UploadAssetsButton.SetEnabled(true);
                        return L10n.Tr(Constants.UploadAssetsTooltip);
                    }

                    existingAssets.Add((uploadAsset, existingAsset));
                }
            }

            foreach (var (uploadAsset, existingAsset) in existingAssets)
            {
                var hasModifiedFiles = await Utilities.IsLocallyModifiedAsync(uploadAsset, existingAsset);
                if (hasModifiedFiles || await Utilities.CheckDependenciesModifiedAsync(existingAsset))
                {
                    m_UploadAssetsButton.SetEnabled(true);
                    return L10n.Tr(Constants.UploadAssetsTooltip);
                }
            }

            return L10n.Tr(Constants.UploadAssetsNotModifiedTooltip);
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
