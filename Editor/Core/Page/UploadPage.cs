using System;
using System.Collections.Generic;
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
            AssetManagerWindow.instance.Focus();

            var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            if (string.IsNullOrEmpty(provider.SelectedOrganization?.id))
                return;

            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

            if (pageManager.activePage is not UploadPage)
            {
                pageManager.SetActivePage<UploadPage>();
            }

            var uploadPage = pageManager.activePage as UploadPage;
            uploadPage?.AddAssets(Selection.assetGUIDs);
        }

        public override bool DisplayTopBar => false;

        public override bool DisplaySideBar => false;

        public override string Title => L10n.Tr("Upload to Asset Manager");

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>();
        }

        [Serializable]
        class UploadContext
        {
            [SerializeField]
            OrganizationInfo m_OrganizationInfo;

            [SerializeField]
            UploadSettings m_Settings;

            [SerializeReference]
            List<IUploadAssetEntry> m_UploadAssetEntries = new();

            public IReadOnlyCollection<IUploadAssetEntry> UploadAssetEntries => m_UploadAssetEntries;

            public bool BundleDependencies;

            public UploadSettings Settings => m_Settings;
            public OrganizationInfo OrganizationInfo => m_OrganizationInfo;
            public string ProjectId => m_Settings.ProjectId;
            public string CollectionPath => m_Settings.CollectionPath;

            public event Action ProjectIdChanged;
            public event Action OnUploadAssetEntriesChanged;

            public UploadContext()
            {
                m_Settings = new UploadSettings();
            }

            public void SetUploadAssetEntries(IEnumerable<IUploadAssetEntry> uploadAssetEntries)
            {
                m_UploadAssetEntries.Clear();
                m_UploadAssetEntries.AddRange(uploadAssetEntries);
                OnUploadAssetEntriesChanged?.Invoke();
            }


            public void SetOrganizationInfo(OrganizationInfo organizationInfo)
            {
                m_OrganizationInfo = organizationInfo;
                m_Settings.OrganizationId = organizationInfo.id;
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

        [SerializeField]
        UploadContext m_UploadContext = new();

        List<ProjectInfo> m_ProjectChoices;
        List<string> m_CollectionChoices;

        VisualElement m_UploadAssetsButton;

        public UploadPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
        }

        public override void OnActivated()
        {
            base.OnActivated();

            m_UploadContext.SetOrganizationInfo(m_ProjectOrganizationProvider.SelectedOrganization);
            m_UploadContext.SetProjectId(m_ProjectOrganizationProvider.SelectedProject?.id);
            m_UploadContext.SetCollectionPath(m_ProjectOrganizationProvider.SelectedCollection?.GetFullPath());
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

        protected override async IAsyncEnumerable<IAssetData> LoadMoreAssets([EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog("Analysing Selection for upload to cloud...");

            var allAssetGuids = ProcessAssetGuids(m_AssetSelection, out var mainAssetGuids);

            var uploadAssetEntries = GenerateAssetEntries(allAssetGuids, m_UploadContext.BundleDependencies).ToList();

            m_UploadContext.SetUploadAssetEntries(uploadAssetEntries);

            var uploadAssetData = new List<UploadAssetData>();
            foreach (var uploadEntry in uploadAssetEntries)
            {
                var assetData = new UploadAssetData(uploadEntry, m_UploadContext.Settings, !mainAssetGuids.Contains(uploadEntry.Guid));
                uploadAssetData.Add(assetData);
            }

            // Sort the result before displaying it
            foreach (var assetData in uploadAssetData.OrderBy(a => a.IsADependency).ThenByDescending(a => a.primaryExtension))
            {
                yield return assetData;
            }

            m_HasMoreItems = false;

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
                return mainAssets;

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

        [SerializeField]
        List<string> m_AssetSelection = new();

        protected override void OnLoadMoreSuccessCallBack()
        {
            SetErrorOrMessageData(!m_AssetList.Any() ? L10n.Tr("Select assets or folders and click 'Add Selected' to prepare them for Cloud upload") : string.Empty, ErrorOrMessageRecommendedAction.None);
        }

        public void UploadAssets()
        {
            if (m_UploadContext.UploadAssetEntries.Any())
            {
                Utilities.DevLog($"Uploading {m_UploadContext.UploadAssetEntries.Count} assets...");
                _ = UploadAssetEntries();
            }
            else
            {
                Utilities.DevLog("No assets to upload");
            }
        }

        async Task UploadAssetEntries()
        {
            var uploadEntries = m_UploadContext.UploadAssetEntries;

            var uploadManager = ServicesContainer.instance.Resolve<IUploadManager>();
            var task = uploadManager.UploadAsync(uploadEntries, m_UploadContext.Settings);

            AnalyticsSender.SendEvent(new UploadEvent(uploadEntries.Count, m_UploadContext.BundleDependencies, !string.IsNullOrEmpty(m_UploadContext.CollectionPath), m_UploadContext.Settings.AssetUploadMode));

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
            var selectedProject = selectedOrganization?.projectInfos.Find(p => p.id == m_UploadContext.ProjectId);
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
                index = string.IsNullOrEmpty(selectedCollectionPath) ? 0 : m_CollectionChoices.FindIndex(c => c == selectedCollectionPath)
            };

            collectionDropdown.RegisterValueChangedCallback(evt =>
            {
                var collectionPath = collectionDropdown.index == 0 ? null : m_CollectionChoices.ElementAtOrDefault(collectionDropdown.index);
                m_UploadContext.SetCollectionPath(collectionPath);
            });

            m_ProjectChoices = selectedOrganization?.projectInfos.OrderBy(p => p.name).ToList();
            var projectIndex = selectedProject != null && m_ProjectChoices != null ? m_ProjectChoices.FindIndex(p => p.id == selectedProject.id) : -1;

            var projectDropdown = new DropdownField("Project")
            {
                choices = m_ProjectChoices?.Select(p => p.name).ToList() ?? new List<string>(),
                index = projectIndex
            };

            projectDropdown.RegisterValueChangedCallback(evt =>
            {
                var project = m_ProjectChoices?.ElementAtOrDefault(projectDropdown.index);
                m_UploadContext.SetProjectId(project?.id);

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

            m_UploadContext.OnUploadAssetEntriesChanged += UpdateUploadAssetButtonState;

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
            collections.AddRange(projectInfo?.collectionInfos.Select(c => c.GetFullPath()).ToList() ?? new List<string>());

            return collections;
        }

        void UpdateUploadAssetButtonState()
        {
            m_UploadAssetsButton?.SetEnabled(!string.IsNullOrEmpty(m_UploadContext.ProjectId) && m_UploadContext.UploadAssetEntries.Count > 0);
        }

        void GoBackToCollectionPage()
        {
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

            if (pageManager == null)
                return;

            if (pageManager.activePage != this)
                return;

            var projectInfo = m_UploadContext.OrganizationInfo?.projectInfos.Find(p => p.id == m_UploadContext.ProjectId);
            if (projectInfo != null)
            {
                m_ProjectOrganizationProvider.SelectProject(projectInfo, m_UploadContext.CollectionPath);
            }
        }

        static IEnumerable<IUploadAssetEntry> GenerateAssetEntries(IEnumerable<string> mainAssetGuids, bool bundleDependencies)
        {
            var processedGuids = new HashSet<string>();

            var uploadEntries = new List<IUploadAssetEntry>();

            foreach (var assetGuid in mainAssetGuids)
            {
                if (processedGuids.Contains(assetGuid))
                    continue;

                uploadEntries.Add(new AssetUploadEntry(assetGuid, bundleDependencies));
                processedGuids.Add(assetGuid);
            }

            return uploadEntries;
        }
    }
}
