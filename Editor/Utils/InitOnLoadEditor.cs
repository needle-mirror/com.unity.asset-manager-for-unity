using System;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.AssetManager.Editor
{
    class InitOnLoadEditor
    {
        static readonly string k_AssetManagerDeepLinkRoute = "com.unity3d.kharma://com.unity.asset-manager-for-unity/";

        [InitializeOnLoadMethod]
        static void InitAssetManagerEditor()
        {
            UnityEditorApplicationFocusUtils.OnApplicationFocusChange += (receivedFocus) =>
            {
                if (receivedFocus && EditorGUIUtility.systemCopyBuffer.Length > 0)
                {
                    var clipboardContent = EditorGUIUtility.systemCopyBuffer;
                    if (clipboardContent.StartsWith(k_AssetManagerDeepLinkRoute) &&
                        Uri.TryCreate(clipboardContent, UriKind.Absolute, out Uri assetManagerDeepLink))
                    {
                        var pathSegments = assetManagerDeepLink.Segments;
                        if (pathSegments.Length >= 7)
                        {
                            ExtractAssetIdAndVersion(pathSegments[6], out var id, out var version);

                            var assetIdentifier = new AssetIdentifier(RemoveSegmentDelimiter(pathSegments[2]),
                                RemoveSegmentDelimiter(pathSegments[4]), id, version);

                            // If the Asset Manager window is closed, select the project too, otherwise just select the asset
                            var selectProject = AssetManagerWindow.instance == null;

                            var openAssetHook = new OpenAssetHook(assetIdentifier, selectProject);
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
    public class UnityEditorApplicationFocusUtils
    {
        public static event Action<bool> OnApplicationFocusChange = _ => { };
        static bool m_HasFocus;

        static UnityEditorApplicationFocusUtils()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if (!m_HasFocus && InternalEditorUtility.isApplicationActive)
            {
                m_HasFocus = InternalEditorUtility.isApplicationActive;
                OnApplicationFocusChange(true);
            }
            else if (m_HasFocus && !InternalEditorUtility.isApplicationActive)
            {
                m_HasFocus = InternalEditorUtility.isApplicationActive;
                OnApplicationFocusChange(false);
            }
        }
    }

    class OpenAssetHook
    {
        readonly AssetIdentifier m_AssetIdentifier;
        readonly bool m_SelectProject;

        IProjectOrganizationProvider m_ProjectProvider;
        IPageManager m_PageManager;

        public OpenAssetHook(AssetIdentifier assetIdentifier, bool selectProject)
        {
            m_AssetIdentifier = assetIdentifier;
            m_SelectProject = selectProject;
        }

        public void OpenAssetManagerWindow()
        {
            var assetManagerWindowHook = new AssetManagerWindowHook();
            assetManagerWindowHook.OrganizationLoaded += OpenAsset;
            assetManagerWindowHook.OpenAssetManagerWindow();
        }

        void OpenAsset()
        {
            m_PageManager = ServicesContainer.instance.Resolve<IPageManager>();
            m_ProjectProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();

            if (string.IsNullOrEmpty(m_ProjectProvider.SelectedOrganization?.id))
                return;

            if (m_ProjectProvider.SelectedOrganization.id != m_AssetIdentifier.organizationId)
            {
                Debug.LogWarning("Organization mismatch. Cannot open asset details.");
                return;
            }

            var switchProject = false;

            if (m_ProjectProvider.SelectedProject?.id != m_AssetIdentifier.projectId)
            {
                switchProject = m_SelectProject
                                || string.IsNullOrEmpty(m_ProjectProvider.SelectedProject?.id)
                                || m_PageManager.activePage is not CollectionPage;
            }

            if (m_PageManager.activePage is not CollectionPage)
            {
                m_PageManager.SetActivePage<CollectionPage>();
            }

            if (switchProject)
            {
                m_ProjectProvider.ProjectSelectionChanged += SelectAsset;
                m_ProjectProvider.SelectProject(m_AssetIdentifier.projectId);
            }
            else
            {
                SelectAsset(null, null);
            }
        }

        void SelectAsset(ProjectInfo _, CollectionInfo __)
        {
            m_ProjectProvider.ProjectSelectionChanged -= SelectAsset;

            var collectionPage = (CollectionPage)m_PageManager.activePage;
            collectionPage.selectedAssetId = m_AssetIdentifier;
        }
    }
}