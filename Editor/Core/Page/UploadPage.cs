using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadPage : BasePage
    {
        [SerializeField]
        UploadContext m_UploadContext = new();

        [SerializeField]
        List<string> m_AssetSelection = new();

        List<string> m_CollectionChoices;
        List<ProjectInfo> m_ProjectChoices;
        VisualElement m_UploadAssetsButton;

        public override bool DisplayTopBar => false;
        public override bool DisplaySideBar => false;
        public override string Title => L10n.Tr("Upload to Asset Manager");

        public UploadPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider) { }

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
                ServicesContainer.instance.Resolve<IPageManager>().ActivePage.Clear(true,true);
                UpdateUploadAssetButtonState();
            }
        }

        void AddAssets(IEnumerable<string> assetGuids, bool clear = true)
        {
            if (clear)
            {
                m_AssetSelection.Clear();
            }

            foreach (var assetGuid in assetGuids)
            {
                if (m_AssetSelection.Contains(assetGuid))
                    continue;

                m_AssetSelection.Add(assetGuid);
            }

            Clear(true);
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog("Analysing Selection for upload to cloud...");

            var allAssetGuids = ProcessAssetGuids(m_AssetSelection, out var mainAssetGuids);

            var uploadAssetEntries = GenerateAssetEntries(allAssetGuids, m_UploadContext.BundleDependencies, m_UploadContext.IgnoredAssetGuids).ToList();

            foreach (var uploadAssetEntry in uploadAssetEntries.Where(uae => m_UploadContext.IgnoredAssetGuids.Contains(uae.Guid)))
            {
                uploadAssetEntry.IsIgnored = true;
            }

            m_UploadContext.SetUploadAssetEntries(uploadAssetEntries);

            var uploadAssetData = new List<UploadAssetData>();
            foreach (var uploadEntry in uploadAssetEntries)
            {
                var isADependency = !mainAssetGuids.Contains(uploadEntry.Guid);
                if(isADependency && !m_UploadContext.DependencyAssetGuids.Contains(uploadEntry.Guid))
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

            if (m_UploadContext.BundleDependencies)
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

        protected override void OnLoadMoreSuccessCallBack()
        {
            SetErrorOrMessageData(
                !m_AssetList.Any()
                    ? L10n.Tr("Select assets or folders and click 'Add Selected' to prepare them for Cloud upload")
                    : string.Empty, ErrorOrMessageRecommendedAction.None);
        }

        public void UploadAssets()
        {
            if (!m_UploadContext.IgnoredAssetGuids.Any() ||
                m_UploadContext.IgnoredAssetGuids.All(ignoreGuid => !m_UploadContext.DependencyAssetGuids.Contains(ignoreGuid)) ||
                ServicesContainer.instance.Resolve<IEditorUtilityProxy>().DisplayDialog(L10n.Tr(Constants.IgnoreDependenciesDialogTitle),
                    L10n.Tr(Constants.IgnoreDependenciesDialogMessage),
                    L10n.Tr("Continue"),
                    L10n.Tr("Cancel")))
            {
                var nonIgnoredAssetEntries = m_UploadContext.UploadAssetEntries.Where(uae => !uae.IsIgnored).ToList();
                if (nonIgnoredAssetEntries.Any())
                {
                    Utilities.DevLog($"Uploading {nonIgnoredAssetEntries.Count} assets...");
                    _ = UploadAssetEntries();
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

            var uploadManager = ServicesContainer.instance.Resolve<IUploadManager>();
            var task = uploadManager.UploadAsync(uploadEntries, m_UploadContext.Settings);

            AnalyticsSender.SendEvent(new UploadEvent(uploadEntries.Count,
                uploadEntries.SelectMany(e => e.Files).Select(f => Path.GetExtension(f).Substring(1)).Where(ext => ext != "meta").ToArray(),
                m_UploadContext.BundleDependencies,
                !string.IsNullOrEmpty(m_UploadContext.CollectionPath),
                m_UploadContext.Settings.AssetUploadMode));

            try
            {
                await task;
            }
            catch (Exception)
            {
                // Errors are supposed to be logged in the IUploaderManager
            }

            if (task.IsCompletedSuccessfully)
            {
                GoBackToCollectionPage();
            }
        }

        public override VisualElement CreateCustomUISection()
        {
            var selectedOrganization = m_UploadContext.OrganizationInfo;
            var selectedProject = selectedOrganization?.ProjectInfos.Find(p => p.Id == m_UploadContext.ProjectId);
            var selectedCollectionPath = m_UploadContext.CollectionPath;

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

            var actionSection = new VisualElement();
            actionSection.AddToClassList("upload-page-action-section");

            actions.Add(actionSection);

            var projectSelection = new VisualElement();
            projectSelection.AddToClassList("upload-page-settings-project-section");

            root.Add(actions);

            actionSection.Add(new Button(() =>
            {
                m_AssetSelection.Clear();

                // TODO Have a proper way to go back to the previous page
                if (m_ProjectOrganizationProvider.SelectedProject != null)
                {
                    ServicesContainer.instance.Resolve<IPageManager>()?.SetActivePage<CollectionPage>();
                }
                else
                {
                    ServicesContainer.instance.Resolve<IPageManager>()?.SetActivePage<InProjectPage>();
                }
            }) { text = "Cancel" });

            m_UploadAssetsButton = new Button(UploadAssets) { text = "Upload As Cloud Assets" };
            actionSection.Add(m_UploadAssetsButton);

            m_CollectionChoices = BuildCollectionChoices(selectedProject);

            var collectionDropdown = new DropdownField("Collection")
            {
                choices = m_CollectionChoices,
                index = string.IsNullOrEmpty(selectedCollectionPath)
                    ? 0
                    : m_CollectionChoices.FindIndex(c => c == selectedCollectionPath)
            };

            collectionDropdown.RegisterValueChangedCallback(evt =>
            {
                var collectionPath = collectionDropdown.index == 0
                    ? null
                    : m_CollectionChoices.ElementAtOrDefault(collectionDropdown.index);
                m_UploadContext.SetCollectionPath(collectionPath);
            });

            m_ProjectChoices = selectedOrganization?.ProjectInfos.OrderBy(p => p.Name).ToList();
            var projectIndex = selectedProject != null && m_ProjectChoices != null
                ? m_ProjectChoices.FindIndex(p => p.Id == selectedProject.Id)
                : -1;

            var projectDropdown = new DropdownField("Project")
            {
                choices = m_ProjectChoices?.Select(p => p.Name).ToList() ?? new List<string>(),
                index = projectIndex
            };

            projectDropdown.RegisterValueChangedCallback(evt =>
            {
                var project = m_ProjectChoices?.ElementAtOrDefault(projectDropdown.index);
                m_UploadContext.SetProjectId(project?.Id);

                collectionDropdown.choices = m_CollectionChoices = BuildCollectionChoices(project);
                collectionDropdown.index = 0;
            });

            projectSelection.Add(projectDropdown);
            projectSelection.Add(collectionDropdown);

            settings.Add(projectSelection);

            var subSettings = new VisualElement();

            settings.Add(subSettings);

            var dependenciesAsAssetsToggle = new Toggle("Embed dependencies")
            {
                value = m_UploadContext.BundleDependencies,
                tooltip = L10n.Tr("If enabled, all assets will have its dependencies embedded in a single Cloud Asset. If disabled, each asset and its dependencies will be uploaded as separate Cloud Asset")
            };

            dependenciesAsAssetsToggle.RegisterValueChangedCallback(v =>
            {
                m_UploadContext.BundleDependencies = v.newValue;
                Clear(true);
            });

            var uploadModeDropdown = new DropdownField("Upload Mode")
            {
                choices = Enum.GetNames(typeof(AssetUploadMode)).Select(ObjectNames.NicifyVariableName).ToList(),
                index = (int)m_UploadContext.Settings.AssetUploadMode,
                tooltip = GetUploadModeTooltip(m_UploadContext.Settings.AssetUploadMode)
            };

            uploadModeDropdown.RegisterValueChangedCallback(v =>
            {
                var value = (AssetUploadMode)uploadModeDropdown.index;
                m_UploadContext.Settings.AssetUploadMode = value;
                uploadModeDropdown.tooltip = GetUploadModeTooltip(value);
                Clear(true);
            });

            subSettings.Add(uploadModeDropdown);
            subSettings.Add(dependenciesAsAssetsToggle);

            m_UploadContext.ProjectIdChanged += () =>
            {
                UpdateUploadAssetButtonState();
                Clear(true);
            };

            m_UploadContext.UploadAssetEntriesChanged += UpdateUploadAssetButtonState;

            UpdateUploadAssetButtonState();

            return root;
        }

        static string GetUploadModeTooltip(AssetUploadMode mode)
        {
            return mode switch
            {
                AssetUploadMode.DuplicateExistingAssets => L10n.Tr("Uploads new assets and potentially duplicates without checking for existing matches"),

                AssetUploadMode.OverrideExistingAssets => L10n.Tr("Replaces and overrides any existing asset with the same id on the cloud"),

                AssetUploadMode.IgnoreAlreadyUploadedAssets => L10n.Tr("Ignores and skips the upload if an asset with the same id already exists on the cloud"),

                _ => null
            };
        }

        static List<string> BuildCollectionChoices(ProjectInfo projectInfo)
        {
            var collections = new List<string> { "<none>" };
            collections.AddRange(projectInfo?.CollectionInfos.Select(c => c.GetFullPath()).ToList() ??
                new List<string>());

            return collections;
        }

        void UpdateUploadAssetButtonState()
        {
            if (m_UploadAssetsButton != null)
            {
                var permissionsManager = ServicesContainer.instance.Resolve<IPermissionsManager>();
                var hasUploadPermission = permissionsManager.CheckPermission(Constants.UploadPermission);
                m_UploadAssetsButton.SetEnabled(!string.IsNullOrEmpty(m_UploadContext.ProjectId) &&
                    hasUploadPermission &&
                    m_UploadContext.UploadAssetEntries.Count > 0 &&
                    m_UploadContext.UploadAssetEntries.Where(uae => !uae.IsIgnored).ToList().Count > 0);

                var allAssetsIgnored = m_UploadContext.UploadAssetEntries.Count > 0 && m_UploadContext.UploadAssetEntries.All(uae => uae.IsIgnored);
                m_UploadAssetsButton.tooltip = !hasUploadPermission ? L10n.Tr("You don’t have permissions to upload assets. \nSee your role from the project settings page on \nthe Asset Manager dashboard.") :
                    allAssetsIgnored ? L10n.Tr("All assets are ignored.") : string.Empty;
            }
        }

        void GoBackToCollectionPage()
        {
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

            if (pageManager == null)
                return;

            if (pageManager.ActivePage != this)
                return;

            var projectInfo = m_UploadContext.OrganizationInfo?.ProjectInfos.Find(p => p.Id == m_UploadContext.ProjectId);
            if (projectInfo != null)
            {
                m_ProjectOrganizationProvider.SelectProject(projectInfo, m_UploadContext.CollectionPath);
            }
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

        [Serializable]
        class UploadContext
        {
            [SerializeField]
            OrganizationInfo m_OrganizationInfo;

            [SerializeField]
            UploadSettings m_Settings;

            public bool BundleDependencies;

            [SerializeReference]
            List<IUploadAssetEntry> m_UploadAssetEntries = new();

            [SerializeReference]
            List<string> m_IgnoredAssetGuids = new();

            [SerializeReference]
            List<string> m_DependencyAssetGuids = new();

            public IReadOnlyCollection<IUploadAssetEntry> UploadAssetEntries => m_UploadAssetEntries;

            public UploadSettings Settings => m_Settings;
            public OrganizationInfo OrganizationInfo => m_OrganizationInfo;
            public string ProjectId => m_Settings.ProjectId;
            public string CollectionPath => m_Settings.CollectionPath;
            public List<string> IgnoredAssetGuids => m_IgnoredAssetGuids;
            public List<string> DependencyAssetGuids => m_DependencyAssetGuids;

            public event Action ProjectIdChanged;
            public event Action UploadAssetEntriesChanged;

            public UploadContext()
            {
                m_Settings = new UploadSettings();
            }

            public void SetUploadAssetEntries(IEnumerable<IUploadAssetEntry> uploadAssetEntries)
            {
                m_UploadAssetEntries.Clear();
                m_UploadAssetEntries.AddRange(uploadAssetEntries);
                UploadAssetEntriesChanged?.Invoke();
            }

            public void SetOrganizationInfo(OrganizationInfo organizationInfo)
            {
                m_OrganizationInfo = organizationInfo;
                m_Settings.OrganizationId = organizationInfo.Id;

                // TODO Check if the project is still valid
            }

            public void SetProjectId(string id)
            {
                m_Settings.ProjectId = id;
                ProjectIdChanged?.Invoke();
            }

            public void SetCollectionPath(string collection)
            {
                m_Settings.CollectionPath = collection;
            }
        }
    }
}
