using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadAssetData : IAssetData
    {
        [SerializeField]
        List<DependencyAsset> m_Dependencies = new();

        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeField]
        string m_AssetGuid;

        [SerializeField]
        string m_AssetPath;

        [SerializeField]
        bool m_IsADependency;

        [SerializeField]
        UploadSettings m_Settings;

        [SerializeReference]
        IUploadAssetEntry m_AssetEntry;

        [SerializeReference]
        List<IAssetDataFile> m_Files = new();

        Task<Texture2D> m_GetThumbnailTask;
        Task<IAsset> m_PreviewStatusTask;

        AssetPreview.IStatus m_ExistingStatus;

        static bool s_UseAdvancedPreviewer = false;
        static readonly List<string> s_Tags = new();

        public bool IsADependency => m_IsADependency;
        public string Name => m_AssetEntry.Name;
        public AssetIdentifier Identifier => m_Identifier;
        public AssetType AssetType => m_AssetEntry.CloudType.ConvertCloudAssetTypeToAssetType();
        public string Status => "Local";
        public DateTime? Updated => null;
        public DateTime? Created => null;
        public IEnumerable<string> Tags => m_AssetEntry.Tags;
        public string Description => "";
        public string CreatedBy => "";
        public string UpdatedBy => "";
        public string PrimaryExtension => Path.GetExtension(m_AssetPath);
        public string AssetPath => m_AssetPath;
        public bool IsIgnored
        {
            get => m_AssetEntry.IsIgnored;
            set => m_AssetEntry.IsIgnored = value;
        }
        public string Guid => m_AssetEntry.Guid;

        public UploadAssetData(IUploadAssetEntry assetEntry, UploadSettings settings, bool isADependency)
        {
            m_AssetEntry = assetEntry;
            m_IsADependency = isADependency;
            m_AssetGuid = assetEntry.Guid;
            m_AssetPath = assetEntry.Files.First();
            m_Settings = settings;

            m_Identifier = LocalAssetIdentifier(m_AssetGuid);

            // Files
            foreach (var file in assetEntry.Files)
            {
                var guid = AssetDatabase.GUIDFromAssetPath(file);
                m_Files.Add(new AssetDataFile(file, guid.Empty() ? null : guid.ToString(), null, s_Tags, GetFileSize(file)));
            }

            // Dependencies
            foreach (var dependency in m_AssetEntry.Dependencies)
            {
                var id = LocalAssetIdentifier(dependency);
                m_Dependencies.Add(new DependencyAsset(id, null));
            }
        }

        public IEnumerable<AssetPreview.IStatus> PreviewStatus
        {
            get
            {
                if (m_IsADependency)
                {
                    yield return AssetDataStatus.Linked;
                }

                if (m_ExistingStatus != null)
                {
                    yield return m_ExistingStatus;
                }
            }
        }

        public IEnumerable<DependencyAsset> Dependencies => m_Dependencies;

        public async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null,
            CancellationToken token = default)
        {
            if (m_GetThumbnailTask == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(m_AssetPath);

                if (asset != null)
                {
                    m_GetThumbnailTask = s_UseAdvancedPreviewer
                        ? AssetManagerPreviewer.GenerateAdvancedPreview(asset, m_AssetPath)
                        : AssetManagerPreviewer.GetDefaultPreviewTexture(asset);
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

            m_PreviewStatusTask ??= AssetDataDependencyHelper.SearchForAssetWithGuid(m_Settings.OrganizationId,
                m_Settings.ProjectId, m_AssetGuid, token);

            IAsset result;

            try
            {
                result = await m_PreviewStatusTask;
            }
            catch (Exception)
            {
                m_PreviewStatusTask = null;
                throw;
            }

            if (result != null)
            {
                m_ExistingStatus = m_Settings.AssetUploadMode switch
                {
                    AssetUploadMode.DuplicateExistingAssets => AssetDataStatus.UploadDuplicate,
                    AssetUploadMode.OverrideExistingAssets => AssetDataStatus.UploadOverride,
                    AssetUploadMode.IgnoreAlreadyUploadedAssets => AssetDataStatus.UploadSkip,

                    _ => AssetDataStatus.Imported
                };
            }
            else
            {
                m_ExistingStatus = null;
            }

            m_PreviewStatusTask = null;

            callback?.Invoke(m_Identifier, PreviewStatus);
        }

        public Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback,
            CancellationToken token = default)
        {
            callback?.Invoke(Identifier, PrimaryExtension);
            return Task.CompletedTask;
        }

        public IEnumerable<IAssetDataFile> SourceFiles => m_Files;

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            yield return null;
            await Task.CompletedTask; // Remove warning about async
        }

        public Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            callback?.Invoke(Identifier);
            return Task.CompletedTask;
        }

        internal static AssetIdentifier LocalAssetIdentifier(string guid)
        {
            return new LocalAssetIdentifier(null, null, null, "1", guid);
        }

        static long GetFileSize(string assetPath)
        {
            var fullPath = Application.dataPath + assetPath["Assets".Length..];
            if (File.Exists(fullPath))
            {
                return new FileInfo(fullPath).Length;
            }

            Debug.LogError("Asset does not exist: " + fullPath);
            return 0;
        }
    }
}
