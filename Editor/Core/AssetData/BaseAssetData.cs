using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    enum AssetDataStatusType
    {
        None,
        Imported,
        UpToDate,
        OutOfDate,
        Error,
        Linked,
        UploadAdd,
        UploadSkip,
        UploadOverride,
        UploadDuplicate,
        UploadOutside
    }

    enum AssetDataEventType
    {
        None,
        ThumbnailChanged,
        PreviewStatusChanged,
        PrimaryFileChanged,
        ToggleValueChanged
    }

    [Serializable]
    abstract class BaseAssetData
    {
        public delegate void AssetDataChangedDelegate(BaseAssetData assetData, AssetDataEventType eventType);

        public event AssetDataChangedDelegate AssetDataChanged;

        public abstract string Name { get; }
        public abstract AssetIdentifier Identifier { get; }
        public abstract int SequenceNumber { get; }
        public abstract int ParentSequenceNumber { get; }
        public abstract string Changelog { get; }
        public abstract AssetType AssetType { get; }
        public abstract string Status { get; }
        public abstract DateTime? Updated { get; }
        public abstract DateTime? Created { get; }
        public abstract IEnumerable<string> Tags { get; }
        public abstract string Description { get; }
        public abstract string CreatedBy { get; }
        public abstract string UpdatedBy { get; }

        public abstract IEnumerable<AssetIdentifier> Dependencies { get; }
        public abstract IEnumerable<BaseAssetDataFile> UVCSFiles { get; }
        public abstract IEnumerable<BaseAssetData> Versions { get; }
        public abstract List<IMetadata> Metadata { get; set; }

        public abstract Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default);
        public abstract Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetDataStatusType>> callback = null, CancellationToken token = default);
        public abstract Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback = null, CancellationToken token = default);

        public abstract Task RefreshPropertiesAsync(CancellationToken token = default);
        public abstract Task RefreshVersionsAsync(CancellationToken token = default);
        public abstract Task RefreshDependenciesAsync(CancellationToken token = default);

        public string PrimaryExtension => m_PrimarySourceFile?.Extension;

        [SerializeField]
        TextureReference m_Thumbnail = new();

        [SerializeField]
        List<AssetDataStatusType> m_PreviewStatus;

        [SerializeReference]
        BaseAssetDataFile m_PrimarySourceFile;

        [SerializeReference]
        List<BaseAssetDataFile> m_SourceFiles = new();

        public virtual IEnumerable<BaseAssetDataFile> SourceFiles
        {
            get => m_SourceFiles;
            protected set
            {
                m_SourceFiles = value?.ToList();

                m_PrimarySourceFile = m_SourceFiles
                    ?.FilterUsableFilesAsPrimaryExtensions()
                    .OrderBy(x => x, new AssetDataFileComparerByExtension())
                    .LastOrDefault();

                InvokeEvent(AssetDataEventType.PrimaryFileChanged);
            }
        }

        public virtual Texture2D Thumbnail
        {
            get => m_Thumbnail.Value;
            protected set
            {
                if (m_Thumbnail.Value == value)
                    return;

                m_Thumbnail.Value = value;

                if (m_Thumbnail.Value != null)
                {
                    // Unity destroys textures created during runtime when existing play mode.
                    // Make sure they are flagged to stay in memory
                    m_Thumbnail.Value.hideFlags = HideFlags.HideAndDontSave;
                }

                InvokeEvent(AssetDataEventType.ThumbnailChanged);
            }
        }

        public virtual IEnumerable<AssetDataStatusType> PreviewStatus
        {
            get => m_PreviewStatus;
            protected set
            {
                m_PreviewStatus = value?.ToList();
                InvokeEvent(AssetDataEventType.PreviewStatusChanged);
            }
        }

        public BaseAssetDataFile PrimarySourceFile => m_PrimarySourceFile;

        public void ResetPreviewStatus()
        {
            PreviewStatus = Array.Empty<AssetDataStatusType>();
        }

        protected void InvokeEvent(AssetDataEventType eventType)
        {
            AssetDataChanged?.Invoke(this, eventType);
        }

        public virtual bool CanRemovedFile(BaseAssetDataFile assetDataFile)
        {
            return false;
        }

        public virtual void RemoveFile(BaseAssetDataFile assetDataFile)
        {
            // Do nothing
        }
    }

    // Texture in memory are not serializable by the Editor,
    // This class is used to serialize the texture as a byte array
    [Serializable]
    class TextureReference : ISerializationCallbackReceiver
    {
        [SerializeField]
        byte[] m_Bytes;

        [SerializeField]
        HideFlags m_HideFlags;

        Texture2D m_Texture;

        public Texture2D Value
        {
            get
            {
                if (m_Texture != null)
                    return m_Texture;

                if (m_Bytes == null || m_Bytes.Length == 0)
                    return null;

                m_Texture = new Texture2D(1, 1)
                {
                    hideFlags = m_HideFlags
                };
                m_Texture.LoadImage(m_Bytes);

                return m_Texture;
            }
            set => m_Texture = value;
        }

        public void OnBeforeSerialize()
        {
            m_Bytes = null;
            if (m_Texture != null)
            {
                m_Bytes = m_Texture.EncodeToPNG();
                m_HideFlags = m_Texture.hideFlags;
            }
        }

        public void OnAfterDeserialize()
        {
            // m_Texture will be deserialized on demand
        }
    }
}
