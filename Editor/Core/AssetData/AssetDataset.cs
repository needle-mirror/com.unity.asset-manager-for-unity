using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class AssetDataset
    {
        internal const string k_SourceTag = "Source";
        const string k_SourceControlTag = "SourceControl";
        static readonly HashSet<string> k_OptimizeAndConvertTags = new()
        {
            "free-tier-optimize-and-convert",
            "higher-tier-optimize-and-convert"
        };
        internal static readonly IEnumerable<string> k_PrimaryDatasetSystemTags = new List<string>
        {
            k_SourceTag,
            "Preview"
        };

        [SerializeField]
        string m_Id;

        [SerializeField]
        string m_Name;

        [SerializeField]
        List<string> m_SystemTags;

        [SerializeReference]
        List<BaseAssetDataFile> m_Files;

        public string Id => m_Id;
        public string Name => m_Name;
        public IEnumerable<string> SystemTags => m_SystemTags ?? new List<string>();
        public IEnumerable<BaseAssetDataFile> Files => m_Files ?? new List<BaseAssetDataFile>();

        public bool IsSource => m_SystemTags.Contains(k_SourceTag);
        public bool IsSourceControlled => m_SystemTags.Contains(k_SourceControlTag);
        public bool CanBeImported => m_SystemTags.Contains(k_SourceTag) || m_SystemTags.Any(tag => k_OptimizeAndConvertTags.Contains(tag));

        internal AssetDataset(string id, string name, IEnumerable<string> systemTags, IEnumerable<BaseAssetDataFile> files = null)
        {
            m_Id = id;
            m_Name = name;
            m_SystemTags = systemTags?.ToList();
            m_Files = files?.ToList();
        }

        internal AssetDataset(string name, IEnumerable<string> systemTags, IEnumerable<BaseAssetDataFile> files)
        {
            m_Id = string.Empty;
            m_Name = name;
            m_SystemTags = systemTags?.ToList();
            m_Files = files?.ToList();
        }

        internal void Copy(AssetDataset other)
        {
            if (other == null || ReferenceEquals(this, other))
            {
                // Copying a dataset onto itself used to clear m_SystemTags before reading them back
                // through other.SystemTags, which is the same list: the dataset silently lost every
                // tag, stopped being a source dataset and disappeared from the inspector.
                return;
            }

            // Read the tags out before writing any field. The early return above already rules out
            // the aliasing that caused AMECO-5287, so this is belt and braces: it keeps the method
            // correct on its own terms, without depending on that guard staying in place.
            var systemTags = other.SystemTags.ToList();

            m_Id = other.Id;
            m_Name = other.Name;
            m_SystemTags = systemTags;
        }

        internal async Task GetFilesAsync(IAssetsProvider assetsProvider, AssetIdentifier assetIdentifier, CancellationToken token = default)
        {
            // If the dataset will not be imported, we don't need to fetch the files
            if (!CanBeImported)
                return;
            
            var files = new List<BaseAssetDataFile>();
            await foreach (var file in assetsProvider.ListFilesAsync(assetIdentifier, this, Range.All, token))
            {
                files.Add(file);
            }

            m_Files = files;
        }
    }
}
