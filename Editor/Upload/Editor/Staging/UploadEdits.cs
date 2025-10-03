using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Upload.Editor
{
    [Serializable]
    // Quick solution to hold manual edits information between two UploadStaging.GenerateUploadAssetData
    // Without this, if the user manually edits assets, then changes the Dependency Mode, edits will be lost
    // Ideally, we should only generate the UploadAssetData once or find a way to re-use the same UploadAssetData instances
    class UploadEdits
    {
        [SerializeField]
        // Assets manually selected by the user
        List<string> m_MainAssetGuids = new();

        [SerializeField]
        // Assets manually ignored by the user
        List<string> m_IgnoredAssetGuids = new();

        [SerializeField]
        // Assets that should include All Scripts
        List<string> m_IncludesAllScripts = new();

        [SerializeReference]
        AssetEdits m_ModifiedMetadata = new();

        public IReadOnlyCollection<string> MainAssetGuids => m_MainAssetGuids;
        public IReadOnlyCollection<string> IgnoredAssetGuids => m_IgnoredAssetGuids;
        public IReadOnlyCollection<string> IncludesAllScriptsForGuids => m_IncludesAllScripts;

        public void AddToSelection(string assetOrFolderGuid)
        {
            // Parse selection to extract folder content
            var mainGuids = UploadAssetStrategy.ResolveMainSelection(assetOrFolderGuid);

            foreach (var guid in mainGuids)
            {
                if (m_MainAssetGuids.Contains(guid))
                    continue;

                m_MainAssetGuids.Add(guid);
            }
        }

        public bool IsSelected(string guid)
        {
            return m_MainAssetGuids.Contains(guid);
        }

        public bool IsEmpty()
        {
            return m_MainAssetGuids.Count == 0;
        }

        public bool RemoveFromSelection(string guid)
        {
            if (!m_MainAssetGuids.Contains(guid))
                return false;

            m_MainAssetGuids.Remove(guid);
            m_ModifiedMetadata.ClearEdits(guid);
            return true;
        }

        public void Clear()
        {
            m_MainAssetGuids.Clear();
            m_IgnoredAssetGuids.Clear();
            m_IncludesAllScripts.Clear();
            m_ModifiedMetadata.Clear();
        }

        public void SetIgnore(string assetGuid, bool ignore)
        {
            if (ignore && !m_IgnoredAssetGuids.Contains(assetGuid))
            {
                m_IgnoredAssetGuids.Add(assetGuid);
            }
            else if (!ignore && m_IgnoredAssetGuids.Contains(assetGuid))
            {
                m_IgnoredAssetGuids.Remove(assetGuid);
            }
        }

        public bool IsIgnored(string assetGuid)
        {
            return m_IgnoredAssetGuids.Contains(assetGuid);
        }

        public bool IncludesAllScripts(string assetDataGuid)
        {
            return m_IncludesAllScripts.Contains(assetDataGuid);
        }

        public void SetIncludesAllScripts(string assetDataGuid, bool include)
        {
            if (include && !m_IncludesAllScripts.Contains(assetDataGuid))
            {
                m_IncludesAllScripts.Add(assetDataGuid);
            }
            else if (!include && m_IncludesAllScripts.Contains(assetDataGuid))
            {
                m_IncludesAllScripts.Remove(assetDataGuid);
            }
        }

        public bool HasEdits(string assetDataGuid)
        {
            return m_ModifiedMetadata.HasEdits(assetDataGuid);
        }

        public void SetModifiedName(string assetDataGuid, string name)
        {
            m_ModifiedMetadata.Names.Dictionary[assetDataGuid] = name;
        }

        public bool TryGetModifiedName(string assetDataGuid, out string name)
        {
            if (m_ModifiedMetadata.Names.Dictionary.TryGetValue(assetDataGuid, out var value))
            {
                name = value;
                return true;
            }

            name = null;
            return false;
        }

        public void SetModifiedDescription(string assetDataGuid, string description)
        {
            m_ModifiedMetadata.Descriptions.Dictionary[assetDataGuid] = description;
        }

        public bool TryGetModifiedDescription(string assetDataGuid, out string description)
        {
            if (m_ModifiedMetadata.Descriptions.Dictionary.TryGetValue(assetDataGuid, out var value))
            {
                description = value;
                return true;
            }

            description = null;
            return false;
        }

        public void SetModifiedTags(string assetDataGuid, IEnumerable<string> tags)
        {
            m_ModifiedMetadata.Tags.Dictionary[assetDataGuid] = tags;
        }

        public bool TryGetModifiedTags(string assetDataGuid, out IEnumerable<string> tags)
        {
            if (m_ModifiedMetadata.Tags.Dictionary.TryGetValue(assetDataGuid, out var value))
            {
                tags = value;
                return true;
            }

            tags = null;
            return false;
        }

        public void SetModifiedCustomMetadata(string assetDataGuid, IMetadataContainer metadataContainer)
        {
            m_ModifiedMetadata.CustomMetadata.Dictionary[assetDataGuid] = metadataContainer;
        }

        public bool TryGetModifiedCustomMetadata(string assetDataGuid, out IReadOnlyCollection<IMetadata> metadata)
        {
            if (m_ModifiedMetadata.CustomMetadata.Dictionary.TryGetValue(assetDataGuid, out var dictionary))
            {
                metadata = dictionary.ToList();
                return true;
            }

            metadata = null;
            return false;
        }
    }

    enum EditField
    {
        Name,
        Description,
        Status,
        Tags,
        Custom,
    }

    class AssetFieldEdit
    {
        readonly Dictionary<EditField, Type> k_FieldTypeMap = new()
        {
            { EditField.Name, typeof(string) },
            { EditField.Description, typeof(string) },
            { EditField.Status, typeof(string) },
            { EditField.Tags, typeof(IEnumerable<string>) },
            { EditField.Custom, typeof(IMetadata) },
        };

        public AssetIdentifier AssetIdentifier {get;}
        public EditField Field {get;}
        public object EditValue { get; }
        Type EditValueType => k_FieldTypeMap[Field];

        public AssetFieldEdit(AssetIdentifier assetIdentifier, EditField field, object editValue)
        {
            AssetIdentifier = assetIdentifier;
            Field = field;
            EditValue = editValue;

            ValidateFieldType();
        }

        void ValidateFieldType()
        {
            if (EditValue == null || !EditValueType.IsInstanceOfType(EditValue))
                throw new ArgumentException($"AssetFieldEdit with type \"{Field.ToString()}\" must be assignable to \"{EditValueType.Name}\"");
        }
    }

    [Serializable]
    class AssetEdits
    {
        [SerializeReference]
        public AssetEditDictionary<string> Names = new();

        [SerializeReference]
        public AssetEditDictionary<string> Descriptions = new();

        [SerializeReference]
        public AssetEditDictionary<IEnumerable<string>> Tags = new();

        [SerializeReference]
        public AssetEditDictionary<IMetadataContainer> CustomMetadata = new();

        public void Clear()
        {
            Names.Dictionary.Clear();
            Descriptions.Dictionary.Clear();
            Tags.Dictionary.Clear();
            CustomMetadata.Dictionary.Clear();
        }

        public bool HasEdits(string assetDataGuid)
        {
            var hasEdits = false;

            hasEdits |= Names.Dictionary.ContainsKey(assetDataGuid);
            hasEdits |= Descriptions.Dictionary.ContainsKey(assetDataGuid);
            hasEdits |= Tags.Dictionary.ContainsKey(assetDataGuid);
            hasEdits |= CustomMetadata.Dictionary.ContainsKey(assetDataGuid);

            return hasEdits;
        }

        public void ClearEdits(string assetDataGuid)
        {
            Names.Dictionary.Remove(assetDataGuid);
            Descriptions.Dictionary.Remove(assetDataGuid);
            Tags.Dictionary.Remove(assetDataGuid);
            CustomMetadata.Dictionary.Remove(assetDataGuid);
        }
    }

    [Serializable]
    class AssetEditDictionary<T> : ISerializationCallbackReceiver
    {
        [SerializeField]
        List<string> m_Keys = new();

        [SerializeField]
        List<T> m_Values = new();

        public Dictionary<string, T> Dictionary { get; private set; } = new();

        public void OnBeforeSerialize()
        {
            m_Keys.Clear();
            m_Values.Clear();

            foreach (var kvp in Dictionary)
            {
                m_Keys.Add(kvp.Key);
                m_Values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Dictionary = new Dictionary<string, T>();
            Utilities.DevAssert(m_Keys.Count == m_Values.Count);

            try
            {
                for (int i = 0; i < m_Keys.Count; i++)
                {
                    Dictionary[m_Keys[i]] = m_Values[i];
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}
