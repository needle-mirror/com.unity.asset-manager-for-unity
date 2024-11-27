using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.AssetsEmbedded;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class AssetDataFile : BaseAssetDataFile
    {
        public AssetDataFile(IFile file)
        {
            if (string.IsNullOrEmpty(file.Descriptor.Path))
                return;

            Path = file.Descriptor.Path;
            Extension = System.IO.Path.GetExtension(Path).ToLower();

            if (file.Tags != null)
            {
                Tags = file.Tags.ToList(); // Replace existing tags by new list of tags
            }

            Available = string.IsNullOrEmpty(file.Status) ||
                          file.Status.Equals("Uploaded", StringComparison.OrdinalIgnoreCase);
            Description = file.Description ?? string.Empty;
            FileSize = file.SizeBytes;
            Guid = null;
        }

        public AssetDataFile(string path, string extension, string guid, string description, IEnumerable<string> tags, long fileSize, bool available)
        {
            Path = path;
            Extension = extension;
            Guid = guid;
            Available = available;
            Description = description;
            Tags = tags.ToList();
            FileSize = fileSize;
        }
    }
}
