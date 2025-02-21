using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    enum AssetDataEventType
    {
        None,
        ThumbnailChanged,
        AssetDataAttributesChanged,
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
        public abstract IEnumerable<BaseAssetData> Versions { get; }
        public abstract IEnumerable<AssetLabel> Labels { get; }

        public abstract Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default);
        public abstract Task GetAssetDataAttributesAsync(Action<AssetIdentifier, AssetDataAttributeCollection> callback = null, CancellationToken token = default);
        public abstract Task ResolveDatasetsAsync(CancellationToken token = default);

        public abstract Task RefreshPropertiesAsync(CancellationToken token = default);
        public abstract Task RefreshVersionsAsync(CancellationToken token = default);
        public abstract Task RefreshDependenciesAsync(CancellationToken token = default);

        public string PrimaryExtension => m_PrimarySourceFile?.Extension;

        protected const string k_Source = "Source";
        protected const string k_NotSynced = "NotSynced";

        [SerializeField]
        TextureReference m_Thumbnail = new();

        [SerializeReference]
        AssetDataAttributeCollection m_AssetDataAttributeCollection;

        [SerializeReference]
        protected BaseAssetDataFile m_PrimarySourceFile;

        [SerializeReference]
        protected List<AssetDataset> m_Datasets = new();

        [SerializeReference]
        protected MetadataContainer m_Metadata = new();

        public IEnumerable<BaseAssetDataFile> SourceFiles => Datasets.FirstOrDefault(d => d.SystemTags.Contains(k_Source))?.Files;

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

        public IMetadataContainer Metadata => m_Metadata;

        public void SetMetadata(IEnumerable<IMetadata> metadata)
        {
            m_Metadata.Set(metadata);
        }

        public void CopyMetadata(IMetadataContainer metadataContainer)
        {
            // Clone the original IMetadata instead of using the reference so that the original is not modified when modifying this UploadAssetData in the UI
            m_Metadata.Set(metadataContainer.Select(m => m.Clone()));
        }

        public virtual AssetDataAttributeCollection AssetDataAttributeCollection
        {
            get => m_AssetDataAttributeCollection;
            set
            {
                m_AssetDataAttributeCollection = value;
                InvokeEvent(AssetDataEventType.AssetDataAttributesChanged);
            }
        }

        // Virtual to allow overriding in test classes
        public virtual IEnumerable<AssetDataset> Datasets
        {
            get => m_Datasets;
            set => m_Datasets = value?.ToList();
        }

        public BaseAssetDataFile PrimarySourceFile => m_PrimarySourceFile;

        public virtual void ResetAssetDataAttributes()
        {
            AssetDataAttributeCollection = null;
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

        public void ResolvePrimaryExtension()
        {
            if (m_Datasets == null || !m_Datasets.Any())
                return;

            var sourceDataset = Datasets.FirstOrDefault(d => d.SystemTags.Contains(k_Source));
            var sourceFiles = sourceDataset?.Files?.ToList();
            m_PrimarySourceFile = sourceFiles
                ?.FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();

            InvokeEvent(AssetDataEventType.PrimaryFileChanged);
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
