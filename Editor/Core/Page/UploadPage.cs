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
        static class UploadPageEditorHook
        {
            [MenuItem("Assets/Upload to Asset Manager", false, 21)]
            static void UploadToAssetManagerMenuItem()
            {
                if (!Utilities.IsDevMode)
                {
                    Debug.Log("This feature is not available yet");
                    return;
                }

                if (AssetManagerWindow.instance == null)
                {
                    AssetManagerWindow.Enabled += OnWindowEnabled;
                    AssetManagerWindow.Open();
                }
                else
                {
                    LoadUploadPage();
                }
            }

            static void OnWindowEnabled()
            {
                AssetManagerWindow.Enabled -= OnWindowEnabled;

                var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();

                if (provider.SelectedOrganization == null)
                {
                    provider.OrganizationChanged += OnOrganizationLoaded;
                }
                else
                {
                    LoadUploadPage();
                }
            }

            static void OnOrganizationLoaded(OrganizationInfo organization)
            {
                if (organization?.id == null)
                    return;

                var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
                provider.OrganizationChanged -= OnOrganizationLoaded;

                LoadUploadPage();
            }

            static void LoadUploadPage()
            {
                var pageManager = ServicesContainer.instance.Resolve<IPageManager>();

                if (pageManager.activePage is not UploadPage)
                {
                    pageManager.SetActivePage<UploadPage>();
                }

                var uploadPage = pageManager.activePage as UploadPage;
                uploadPage?.AddAssets(Selection.assetGUIDs);
            }
        }

        [MenuItem("Assets/Upload to Asset Manager", true, 21)]
        static bool UploadToAssetManagerMenuItemValidation()
        {
            return Selection.assetGUIDs is { Length: > 0 } && Selection.activeObject != null;
        }

        public override bool DisplayTopBar => false;

        public override bool DisplaySideBar => false;

        public override string Title => L10n.Tr("Upload to Dashboard");

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>();
        }

        [Serializable]
        class UploadContext
        {
            [SerializeField]
            string m_OrganizationId;

            [SerializeField]
            string m_ProjectId;

            [SerializeField]
            string m_CollectionPath;

            [SerializeReference]
            List<IUploadAssetEntry> m_UploadAssetEntries = new();

            public IReadOnlyCollection<IUploadAssetEntry> UploadAssetEntries => m_UploadAssetEntries;

            public bool BundleDependencies;

            public string ProjectId => m_ProjectId;
            public string CollectionPath => m_CollectionPath;

            public event Action ProjectIdChanged;

            public UploadSettings GetUploadSettings()
            {
                return new UploadSettings { OrganizationId = m_OrganizationId, ProjectId = m_ProjectId, CollectionPath = m_CollectionPath };
            }

            public void SetUploadAssetEntries(IEnumerable<IUploadAssetEntry> uploadAssetEntries)
            {
                m_UploadAssetEntries.Clear();
                m_UploadAssetEntries.AddRange(uploadAssetEntries);
                ProjectIdChanged?.Invoke();
            }

            public void SetProjectId(string id)
            {
                m_ProjectId = id;
                ProjectIdChanged?.Invoke();
            }

            public void SetCollectionPath(string collection)
            {
                m_CollectionPath = collection;
            }
        }

        [SerializeField]
        UploadContext m_UploadContext = new();

        VisualElement m_UploadAssetsButton;

        public UploadPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IProjectOrganizationProvider projectOrganizationProvider)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider)
        {
        }

        void AddAssets(IEnumerable<string> assetGuids)
        {
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
            if (Utilities.IsDevMode)
            {
                Debug.Log("Analysing Selection for upload to cloud...");
            }

            var allAssetGuids = ProcessAssetGuids(m_AssetSelection, out var mainAssetGuids);

            var uploadAssetEntries = AssetManagerUploader.GenerateAssetEntries(allAssetGuids, m_UploadContext.BundleDependencies).ToList();

            m_UploadContext.SetUploadAssetEntries(uploadAssetEntries);

            var uploadAssetData = new List<UploadAssetData>();
            foreach (var uploadEntry in uploadAssetEntries)
            {
                var assetData = new UploadAssetData(uploadEntry, !mainAssetGuids.Contains(uploadEntry.Guid));
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
                if (Utilities.IsDevMode)
                {
                    Debug.Log($"Uploading {m_UploadContext.UploadAssetEntries.Count} assets...");
                }

                _ = (new AssetManagerUploader(m_UploadContext.GetUploadSettings())).UploadAssetEntries(m_UploadContext.UploadAssetEntries);
            }
            else
            {
                if (Utilities.IsDevMode)
                {
                    Debug.Log("No assets to upload");
                }
            }
        }

        public override VisualElement CreateCustomUISection()
        {
            m_UploadContext.SetProjectId(m_ProjectOrganizationProvider.SelectedProject?.id);
            m_UploadContext.SetCollectionPath(m_ProjectOrganizationProvider.SelectedCollection?.GetFullPath());

            var selectedOrganization = m_ProjectOrganizationProvider.SelectedOrganization;

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

            selectionSection.Add(new Button(() =>
            {
                m_AssetSelection.Clear();
                Clear(true);
            }) { text = "Clear" });

            selectionSection.Add(new Button(() =>
            {
                AddAssets(Selection.assetGUIDs);
            }) { text = "Add Selected" });

            m_UploadAssetsButton = new Button(UploadAssets) { text = "Upload As Cloud Assets" };
            actionSection.Add(m_UploadAssetsButton);

            var projectInfo = selectedOrganization?.projectInfos.Find(p =>
                p.id == m_UploadContext.ProjectId);

            var collections = BuildCollectionChoices(projectInfo);

            var collectionDropdown = new DropdownField("Collection")
            {
                choices = collections,
                index = string.IsNullOrEmpty(m_UploadContext.CollectionPath) ? 0 : collections.FindIndex(c => c == m_UploadContext.CollectionPath)
            };

            collectionDropdown.RegisterValueChangedCallback(evt =>
            {
                var collectionPath = collectionDropdown.index == 0 ? null : collections.ElementAtOrDefault(collectionDropdown.index);
                m_UploadContext.SetCollectionPath(collectionPath);
            });

            var projects = selectedOrganization?.projectInfos.OrderBy(p => p.name).ToList();

            var projectDropdown = new DropdownField("Project")
            {
                choices = projects?.Select(p => p.name).ToList() ?? new List<string>(),
                index = projects?.FindIndex(p => p.id == m_UploadContext.ProjectId) ?? -1
            };

            projectDropdown.RegisterValueChangedCallback(evt =>
            {
                var project = projects?.ElementAtOrDefault(projectDropdown.index);
                m_UploadContext.SetProjectId(project?.id);

                collectionDropdown.choices = BuildCollectionChoices(project);
                collectionDropdown.index = 0;
            });

            projectSelection.Add(projectDropdown);
            projectSelection.Add(collectionDropdown);

            settings.Add(projectSelection);

            var dependenciesAsAssetsToggle = new Toggle("Dependencies As Assets") { value = !m_UploadContext.BundleDependencies };
            dependenciesAsAssetsToggle.RegisterValueChangedCallback(v =>
            {
                m_UploadContext.BundleDependencies = !v.newValue;
                Clear(true);
            });

            settings.Add(dependenciesAsAssetsToggle);

            m_UploadContext.ProjectIdChanged += UpdateUploadAssetButtonState;

            UpdateUploadAssetButtonState();

            return root;
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
    }
}
