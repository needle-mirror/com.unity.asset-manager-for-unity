using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadAssetData : IAssetData
    {
        [SerializeField]
        List<AssetIdentifier> m_Dependencies = new();

        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeField]
        string m_AssetGuid;

        [SerializeField]
        string m_AssetPath;

        [SerializeField]
        UploadSettings m_Settings;

        [SerializeField]
        string m_PrimaryExtension;

        [SerializeField]
        bool m_IsFolder;

        [SerializeField]
        bool m_Ignored;

        [SerializeField]
        bool m_IsDependency;

        [SerializeField]
        bool m_IsSkipped;

        [SerializeReference]
        IUploadAsset m_UploadAsset;

        [SerializeReference]
        List<IAssetDataFile> m_Files = new();

        [SerializeReference]
        IAssetDataFile m_PrimaryFile;

        Task<Texture2D> m_GetThumbnailTask;
        Task m_PreviewStatusTask;

        AssetPreview.IStatus m_ExistingStatus;

        static bool s_UseAdvancedPreviewer = false;
        static readonly List<string> k_Tags = new();

        public string Name => m_UploadAsset.Name;
        public AssetIdentifier Identifier => m_Identifier;
        public int SequenceNumber => -1;
        public int ParentSequenceNumber => -1;
        public string Changelog => "";
        public AssetType AssetType => m_UploadAsset.AssetType;
        public string Status => "Local";
        public DateTime? Updated => null;
        public DateTime? Created => null;
        public IEnumerable<string> Tags => m_UploadAsset.Tags;
        public string Description => "";
        public string CreatedBy => "";
        public string UpdatedBy => "";
        public string PrimaryExtension => m_PrimaryExtension;

        public bool IsIgnored
        {
            get => m_Ignored;
            set => m_Ignored = value;
        }

        public bool IsDependency
        {
            get => m_IsDependency;
            set => m_IsDependency = value;
        }

        public bool IsSkipped
        {
            get => m_IsSkipped;
            set => m_IsSkipped = value;
        }

        public string Guid => m_UploadAsset.Guid;

        public bool HasAnExistingStatus => m_ExistingStatus != null;

        public UploadAssetData(IUploadAsset uploadAsset, UploadSettings settings)
        {
            m_UploadAsset = uploadAsset;
            m_AssetGuid = uploadAsset.Guid;
            m_AssetPath = AssetDatabase.GUIDToAssetPath(m_AssetGuid);
            m_IsFolder = AssetDatabase.IsValidFolder(m_AssetPath);
            m_PrimaryExtension = m_IsFolder ? "folder" : Path.GetExtension(m_AssetPath);

            m_Settings = settings;

            m_Identifier = new AssetIdentifier(m_AssetGuid);

            // Files
            foreach (var file in uploadAsset.Files)
            {
                var guid = AssetDatabase.GUIDFromAssetPath(file.SourcePath);
                m_Files.Add(new AssetDataFile(file.DestinationPath,
                    Path.GetExtension(file.DestinationPath).ToLower(),
                    guid.Empty() ? null : guid.ToString(),
                    null,
                    k_Tags,
                    GetFileSize(file.SourcePath), true));
            }

            m_PrimaryFile = m_Files
                .FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();

            // Dependencies
            foreach (var dependency in m_UploadAsset.Dependencies)
            {
                var id = new AssetIdentifier(dependency);
                m_Dependencies.Add(id);
            }
        }

        public IEnumerable<AssetPreview.IStatus> PreviewStatus
        {
            get
            {
                if (m_IsDependency)
                {
                    yield return AssetDataStatus.Linked;
                }

                if (m_ExistingStatus != null)
                {
                    yield return m_ExistingStatus;
                }
            }
        }

        public IEnumerable<AssetIdentifier> Dependencies => m_Dependencies;

        public IEnumerable<IAssetData> Versions => Array.Empty<IAssetData>();

        public async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null,
            CancellationToken token = default)
        {
            if (m_GetThumbnailTask == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(m_AssetPath);

                if (asset != null)
                {
                    m_GetThumbnailTask = s_UseAdvancedPreviewer ?
                        AssetManagerPreviewer.GenerateAdvancedPreview(asset, m_AssetPath) :
                        AssetManagerPreviewer.GetDefaultPreviewTexture(asset);
                }
            }

            var texture = m_GetThumbnailTask != null ? await m_GetThumbnailTask : null;
            m_GetThumbnailTask = null;

            callback?.Invoke(Identifier, texture);
        }

        public async Task GetPreviewStatusAsync(
            Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null,
            CancellationToken token = default)
        {
            m_ExistingStatus = null;

            m_PreviewStatusTask ??= GetPreviewStatusInternalAsync(token);

            try
            {
                await m_PreviewStatusTask;
            }
            catch (Exception)
            {
                m_PreviewStatusTask = null;
                throw;
            }

            m_PreviewStatusTask = null;

            callback?.Invoke(m_Identifier, PreviewStatus);
        }

        async Task GetPreviewStatusInternalAsync(CancellationToken token)
        {
            var guidWasMatchedWithAnAsset = false;

            var statusTask = AssetDataDependencyHelper.GetAssetAssociatedWithGuidAsync(m_AssetGuid,
                m_Settings.OrganizationId, m_Settings.ProjectId, token);

            var result = await statusTask;
            guidWasMatchedWithAnAsset = result != null;

            var assetData = statusTask?.Result;

            if (guidWasMatchedWithAnAsset)
            {
                switch (m_Settings.UploadMode)
                {
                    case UploadAssetMode.SkipIdentical:

                        var hasModifiedFiles = await Utilities.IsLocallyModifiedAsync(m_UploadAsset, assetData, null, token);
                        if (hasModifiedFiles || await Utilities.CheckDependenciesModifiedAsync(assetData, null, token))
                        {
                            m_ExistingStatus = AssetDataStatus.UploadOverride;
                            m_IsSkipped = false;
                        }
                        else
                        {
                            m_ExistingStatus = AssetDataStatus.UploadSkip;
                            m_IsSkipped = true;
                        }

                        break;
                    case UploadAssetMode.ForceNewVersion:
                        m_ExistingStatus = assetData != null ? AssetDataStatus.UploadOverride : AssetDataStatus.UploadAdd;
                        break;
                    case UploadAssetMode.ForceNewAsset:
                        m_ExistingStatus = AssetDataStatus.UploadDuplicate;
                        break;
                    default:
                        m_ExistingStatus = AssetDataStatus.Imported;
                        break;
                }
            }
            else if (m_AssetPath.StartsWith("Packages") || m_AssetPath.StartsWith("../"))
            {
                m_ExistingStatus = AssetDataStatus.UploadOutside;
            }
            else
            {
                m_ExistingStatus = AssetDataStatus.UploadAdd;
            }
        }

        public Task RefreshVersionsAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task RefreshDependenciesAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback,
            CancellationToken token = default)
        {
            callback?.Invoke(Identifier, PrimaryExtension);
            return Task.CompletedTask;
        }

        public IEnumerable<IAssetDataFile> SourceFiles => m_Files;
        public IAssetDataFile PrimarySourceFile => m_PrimaryFile;
        public IEnumerable<IAssetDataFile> UVCSFiles => Array.Empty<IAssetDataFile>();

        public Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            callback?.Invoke(Identifier);
            return Task.CompletedTask;
        }

        public Task SyncWithCloudLatestAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            return SyncWithCloudAsync(callback, token);
        }

        static long GetFileSize(string assetPath)
        {
            var fullPath = Path.Combine(Application.dataPath, Utilities.GetPathRelativeToAssetsFolder(assetPath));

            if (File.Exists(fullPath))
            {
                return new FileInfo(fullPath).Length;
            }

            Debug.LogError("Asset does not exist: " + fullPath);
            return 0;
        }
    }
}
