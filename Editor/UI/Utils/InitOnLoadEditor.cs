using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.AssetManager.UI.Editor
{
    class InitOnLoadEditor
    {
        static readonly string k_AssetManagerDeepLinkRoute = "com.unity3d.kharma://com.unity.asset-manager-for-unity/";

        [InitializeOnLoadMethod]
        static void ClearSerializedSelectionWhenPrivateCloudServicesToggled()
        {
            PrivateCloudSettings.ServicesEnabledChanged += _ =>
            {
                var stateManager = ServicesContainer.instance?.Get<IStateManager>();
                if (stateManager == null)
                    return;
                stateManager.SelectedOrganizationId = null;
                stateManager.SelectedOrganizationName = null;
                stateManager.SelectedProjectId = null;
                stateManager.SelectedCollectionPath = null;
            };
        }

        [InitializeOnLoadMethod]
        static void InitAssetManagerEditor()
        {
            UnityEditorApplicationFocusUtils.ApplicationFocusChange += receivedFocus =>
            {
                if (receivedFocus && EditorGUIUtility.systemCopyBuffer.Length > 0)
                {
                    var clipboardContent = EditorGUIUtility.systemCopyBuffer;
                    if (clipboardContent.StartsWith(k_AssetManagerDeepLinkRoute) &&
                        Uri.TryCreate(clipboardContent, UriKind.Absolute, out var assetManagerDeepLink))
                    {
                        var pathSegments = assetManagerDeepLink.Segments;
                        if (pathSegments.Length >= 7)
                        {
                            ExtractAssetIdAndVersion(pathSegments[6], out var id, out var version);

                            var assetIdentifier = new AssetIdentifier(RemoveSegmentDelimiter(pathSegments[2]),
                                RemoveSegmentDelimiter(pathSegments[4]), id, version);

                            var openAssetHook = new OpenAssetHook(assetIdentifier);
                            openAssetHook.OpenAssetManagerWindow();
                        }
                        else
                        {
                            AssetManagerWindow.Open();
                        }

                        EditorGUIUtility.systemCopyBuffer = string.Empty;
                    }
                }
            };
        }

        static string RemoveSegmentDelimiter(string segment)
        {
            return segment.Trim('/');
        }

        static void ExtractAssetIdAndVersion(string str, out string id, out string version)
        {
            str = RemoveSegmentDelimiter(str);
            var delimiter = str.IndexOf(':');

            if (delimiter == -1)
            {
                id = str;
                version = "1";
            }
            else
            {
                id = str[..delimiter];
                version = str[(delimiter + 1)..];
            }
        }
    }

    [InitializeOnLoad]
    class UnityEditorApplicationFocusUtils
    {
        static bool s_HasFocus;

        public static event Action<bool> ApplicationFocusChange = _ => { };

        static UnityEditorApplicationFocusUtils()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if (!s_HasFocus && InternalEditorUtility.isApplicationActive)
            {
                s_HasFocus = InternalEditorUtility.isApplicationActive;
                ApplicationFocusChange(true);
            }
            else if (s_HasFocus && !InternalEditorUtility.isApplicationActive)
            {
                s_HasFocus = InternalEditorUtility.isApplicationActive;
                ApplicationFocusChange(false);
            }
        }
    }

    class OpenAssetHook
    {
        readonly AssetIdentifier m_AssetIdentifier;
        IPageManager m_PageManager;
        IProjectOrganizationProvider m_ProjectProvider;
        IMessageManager m_MessageManager;

        OrganizationInfo m_SelectedOrganizationInfo;

        AssetManagerWindowHook m_AssetManagerWindowHook;

        public OpenAssetHook(AssetIdentifier assetIdentifier)
        {
            m_AssetIdentifier = assetIdentifier;
        }

        public void OpenAssetManagerWindow()
        {
            m_AssetManagerWindowHook = new AssetManagerWindowHook();
            m_AssetManagerWindowHook.OrganizationLoaded += OpenAsset;
            m_AssetManagerWindowHook.OpenAssetManagerWindow();
        }

        /// <summary>
        /// Invokes the open-asset flow directly for tests, without opening the Asset Manager window.
        /// </summary>
        internal void InvokeOpenAssetForTest()
        {
            OpenAsset();
        }

        async void OpenAsset()
        {
            if (m_AssetManagerWindowHook != null)
            {
                m_AssetManagerWindowHook.OrganizationLoaded -= OpenAsset;
                m_AssetManagerWindowHook = null;
            }

            m_PageManager = ServicesContainer.instance.Resolve<IPageManager>();
            m_ProjectProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            m_MessageManager = ServicesContainer.instance.Resolve<IMessageManager>();

            // Clear any previous deeplink warning so a valid second deeplink does not leave the old message visible.
            m_MessageManager.DismissDeeplinkHelpBoxMessage();

            // Use OrganizationListChanged and OrganizationList to verify access to the deeplink org.
            m_ProjectProvider.OrganizationListChanged += OnOrganizationListChanged;

            var organizationList = m_ProjectProvider.OrganizationList;
            if (organizationList != null && organizationList.Count > 0)
                TryProceedWithDeeplinkWithList(organizationList);
        }

        void OnOrganizationListChanged(IReadOnlyList<NameAndId> list)
        {
            if (list != null && list.Count > 0) 
            {
                m_ProjectProvider.OrganizationListChanged -= OnOrganizationListChanged;
                TryProceedWithDeeplinkWithList(list);
            }
        }

        void TryProceedWithDeeplinkWithList(IReadOnlyList<NameAndId> list)
        {
            m_ProjectProvider.OrganizationListChanged -= OnOrganizationListChanged;

            var hasAccessToDeeplinkOrg = list != null && list.Any(o => o.Id == m_AssetIdentifier.OrganizationId);
            if (!hasAccessToDeeplinkOrg)
            {
                DisplayOrganizationNotFoundWarning();
                m_ProjectProvider.RefreshOrganizationListAsync();
                return;
            }

            m_ProjectProvider.OrganizationChanged += OnOrganizationChanged;
            m_ProjectProvider.LoadingStateChanged += OnOrganizationLoadingStateChanged;
            m_ProjectProvider.SelectOrganization(m_AssetIdentifier.OrganizationId);
        }

        void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            m_SelectedOrganizationInfo = organizationInfo;
        }

        void OnOrganizationLoadingStateChanged(bool isLoading) 
        {
            if (!isLoading) 
            {
                m_ProjectProvider.OrganizationChanged -= OnOrganizationChanged;
                m_ProjectProvider.LoadingStateChanged -= OnOrganizationLoadingStateChanged;

                if (m_SelectedOrganizationInfo?.Id == m_AssetIdentifier.OrganizationId)
                {
                    TrySelectProject(); 
                }
                else 
                {
                    DisplayOrganizationNotFoundWarning();
                }
            }
        }

        void OnProjectSelectionChanged(ProjectOrLibraryInfo _, CollectionInfo __)
        {
            m_ProjectProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
            if (m_ProjectProvider.SelectedProjectOrLibrary?.Id != m_AssetIdentifier.ProjectId)
            {
                DisplayProjectNotFoundWarning();
            } 
            else
            {
                m_PageManager.ActivePage.LoadingStatusChanged += OnLoadingStatusChanged;
                ApplyDeeplinkFilterToStrategy(reloadImmediately: true);
            }
        }

        void OnLoadingStatusChanged(bool isLoading)
        {
            if (isLoading)
                return;
            m_PageManager.ActivePage.LoadingStatusChanged -= OnLoadingStatusChanged;
            TrySelectDeeplinkAssetOnPage((CollectionPage)m_PageManager.ActivePage);
        }

        void TrySelectProject()
        {
            if (m_PageManager.ActivePage is not CollectionPage)
            {
                m_PageManager.SetActivePage<CollectionPage>();
            }

            if (m_ProjectProvider.GetProject(m_AssetIdentifier.ProjectId) != null)
            {
                
                ApplyDeeplinkFilterToStrategy(reloadImmediately: false);
                m_ProjectProvider.ProjectSelectionChanged += OnProjectSelectionChanged;
                m_ProjectProvider.SelectProject(m_AssetIdentifier.ProjectId);       
            } 
            else
            {
                var orgProjects = m_ProjectProvider.SelectedOrganization?.ProjectInfos;
                    var firstProjectId = (orgProjects != null && orgProjects.Count > 0) ? orgProjects[0].Id : null;
                    if (!string.IsNullOrEmpty(firstProjectId))
                        m_ProjectProvider.SelectProject(firstProjectId);
                
                DisplayProjectNotFoundWarning();
            }
        }

        void ApplyDeeplinkFilterToStrategy(bool reloadImmediately)
        {
            var deeplinkFilter = new AssetSearchFilter
            {
                AssetIds = new List<string> { m_AssetIdentifier.AssetId }
            };

            m_PageManager.PageFilterStrategy.ApplyFilterFromAssetSearchFilter(deeplinkFilter, reloadImmediately);

            // Store deeplink version so the Asset Inspector Versions tab can expand that version's foldout when opened.
            if (!string.IsNullOrEmpty(m_AssetIdentifier.Version))
            {
                var uiPreferences = ServicesContainer.instance.Resolve<IUIPreferences>();
                uiPreferences.SetString($"deeplink-expand-version:{m_AssetIdentifier.AssetId}", m_AssetIdentifier.Version);
            }
        }

        void TrySelectDeeplinkAssetOnPage(CollectionPage page)
        {
            // Prefer exact match (assetId + version) when deeplink specified a version, so the detail panel shows the right version.
            foreach (var asset in page.AssetList)
            {
                if (asset.Identifier.Equals(m_AssetIdentifier))
                {
                    page.SelectAsset(asset.Identifier, false);
                    return;
                }
            }
            foreach (var asset in page.AssetList)
            {
                if (asset.Identifier.AssetId == m_AssetIdentifier.AssetId)
                {
                    page.SelectAsset(asset.Identifier, false);
                    // Deeplink requested a specific version but it was not found; show warning in grid banner
                    if (!string.IsNullOrEmpty(m_AssetIdentifier.Version))
                        DisplayAssetVersionNotFoundWarning();
                    return;
                }
            }
        }

        void DisplayProjectNotFoundWarning()
        {
            m_MessageManager?.SetHelpBoxMessage(new HelpBoxMessage(
                string.Format(L10n.Tr(Constants.DeeplinkProjectNotAccessibleText), m_AssetIdentifier.ProjectId ?? ""),
                RecommendedAction.None, HelpBoxMessageType.Warning, dismissable: true, category: MessageCategory.Deeplink));
        }

        void DisplayOrganizationNotFoundWarning()
        {
            m_MessageManager?.SetHelpBoxMessage(new HelpBoxMessage(
                string.Format(L10n.Tr(Constants.DeeplinkOrganizationNotAccessibleText), m_AssetIdentifier.OrganizationId ?? ""),
                RecommendedAction.None, HelpBoxMessageType.Warning, dismissable: true, category: MessageCategory.Deeplink));
        }

        void DisplayAssetVersionNotFoundWarning()
        {
            m_MessageManager?.SetHelpBoxMessage(new HelpBoxMessage(
                string.Format(L10n.Tr(Constants.DeeplinkVersionNotFoundForAssetText), m_AssetIdentifier.Version ?? ""),
                RecommendedAction.None, HelpBoxMessageType.Warning, dismissable: true, category: MessageCategory.Deeplink));
        }
    }
}
