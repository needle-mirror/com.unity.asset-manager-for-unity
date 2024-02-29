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

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadAssetData : IAssetData
    {
        public string name => m_AssetEntry.Name;
        public AssetIdentifier identifier => new(null, null, m_AssetGuid, "1");
        public AssetType assetType => m_AssetEntry.CloudType.ConvertCloudAssetTypeToAssetType();
        public string status => "Local";
        public DateTime? updated => null;
        public DateTime? created => null;
        public IEnumerable<string> tags => m_AssetEntry.Tags;
        public string description => "<none>";
        public string authorName => "<none>";
        public string defaultImportPath => m_AssetPath;
        public string primaryExtension => Path.GetExtension(m_AssetPath);
        public AssetPreview.IStatus previewStatus => m_IsADependency ? AssetDataStatus.Linked : null;
        public bool IsADependency => m_IsADependency;

        [SerializeField]
        string m_AssetGuid;

        [SerializeField]
        string m_AssetPath;

        [SerializeReference]
        List<IAssetDataFile> m_Files = new();

        [SerializeField]
        bool m_IsADependency;

        [SerializeReference]
        IUploadAssetEntry m_AssetEntry;

        static bool s_UseAdvancedPreviewer = false;

        static readonly List<string> k_Tags = new();

        public UploadAssetData(IUploadAssetEntry assetEntry, bool isADependency)
        {
            m_AssetEntry = assetEntry;
            m_IsADependency = isADependency;
            m_AssetGuid = assetEntry.Guid;
            m_AssetPath = assetEntry.Files.First();

            foreach (var file in assetEntry.Files)
            {
                m_Files.Add(new AssetDataFile(file, null, k_Tags, GetFileSize(file)));
            }
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

        public async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null)
        {
            Texture2D texture = null;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(m_AssetPath);

            if (asset != null)
            {
                if (s_UseAdvancedPreviewer)
                {
                    texture = await AssetManagerPreviewer.GenerateAdvancedPreview(asset, m_AssetPath);
                }
                else
                {
                    texture = await AssetManagerPreviewer.GetDefaultPreviewTexture(asset);
                }
            }

            callback?.Invoke(identifier, texture);
        }

        public Task GetPreviewStatusAsync(Action<AssetIdentifier, AssetPreview.IStatus> callback = null)
        {
            callback?.Invoke(identifier, previewStatus);
            return Task.CompletedTask;
        }

        public Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback)
        {
            callback?.Invoke(identifier, primaryExtension);
            return Task.CompletedTask;
        }

        public IEnumerable<IAssetDataFile> sourceFiles => m_Files;

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync([EnumeratorCancellation] CancellationToken token = default)
        {
            yield return null;
            await Task.CompletedTask; // Remove warning about async
        }

        public Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            callback?.Invoke(identifier);
            return Task.CompletedTask;
        }
    }
}