using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Represents cached asset data for an imported asset, stored in a non-version-controlled cache.
    /// Contains display-oriented data that supplements the tracking file.
    /// </summary>
    [Serializable]
    class AssetDataCacheEntry
    {
        public const int CurrentCacheVersion = 1;

        // Cache metadata - this data will allow us to determine if the cache is outdated
        // and whether we should try to parse data from it or just invalidate and re-fetch.
        [SerializeField]
        public int cacheVersion = CurrentCacheVersion;

        [SerializeField]
        public string cachedAt;

        // Asset identification
        [SerializeField]
        public string organizationId;

        [SerializeField]
        public string projectId;

        [SerializeField]
        public string assetId;

        [SerializeField]
        public string versionId;

        [SerializeField]
        public string versionLabel;

        [SerializeField]
        public string libraryId;

        [SerializeField]
        public int parentSequenceNumber;

        // Version info (display-oriented, cached for UI)
        [SerializeField]
        public string name;

        [SerializeField]
        public string changelog;

        [SerializeField]
        public AssetType assetType;

        [SerializeField]
        public string status;

        [SerializeField]
        public string statusFlowId;

        [SerializeField]
        public string description;

        [SerializeField]
        public string created;

        [SerializeField]
        public string createdBy;

        [SerializeField]
        public string updatedBy;

        [SerializeField]
        public string previewFilePath;

        [SerializeField]
        public bool isFrozen;

        [SerializeField]
        public List<string> tags = new();

        // Datasets
        [SerializeField]
        public List<AssetDataCacheDataset> datasets = new();

        // Files
        [SerializeField]
        public List<AssetDataCacheFile> files = new();

        // Metadata
        [SerializeField]
        public AssetDataCacheMetadata metadata = new();

        // Linked data
        [SerializeField]
        public List<AssetDataCacheLinkedProject> linkedProjects = new();

        [SerializeField]
        public List<AssetDataCacheLinkedCollection> linkedCollections = new();

        // Dependencies (asset identifiers this asset depends on)
        [SerializeField]
        public List<AssetDataCacheDependency> dependencyAssets = new();
    }

    [Serializable]
    class AssetDataCacheDependency
    {
        [SerializeField]
        public string organizationId;

        [SerializeField]
        public string projectId;

        [SerializeField]
        public string assetId;

        [SerializeField]
        public string versionId;

        [SerializeField]
        public string versionLabel;

        [SerializeField]
        public string libraryId;

        public AssetDataCacheDependency() { }

        public AssetDataCacheDependency(string organizationId, string projectId, string assetId, string versionId, string versionLabel, string libraryId)
        {
            this.organizationId = organizationId;
            this.projectId = projectId;
            this.assetId = assetId;
            this.versionId = versionId;
            this.versionLabel = versionLabel;
            this.libraryId = libraryId;
        }
    }

    [Serializable]
    class AssetDataCacheDataset
    {
        [SerializeField]
        public string id;

        [SerializeField]
        public string name;

        [SerializeField]
        public List<string> systemTags = new();

        [SerializeField]
        public List<string> fileKeys = new();

        public AssetDataCacheDataset() { }

        public AssetDataCacheDataset(string id, string name, List<string> systemTags, List<string> fileKeys)
        {
            this.id = id;
            this.name = name;
            this.systemTags = systemTags ?? new List<string>();
            this.fileKeys = fileKeys ?? new List<string>();
        }
    }

    [Serializable]
    class AssetDataCacheFile
    {
        [SerializeField]
        public string datasetId;

        [SerializeField]
        public string path;

        [SerializeField]
        public string extension;

        [SerializeField]
        public bool available;

        [SerializeField]
        public string description;

        [SerializeField]
        public long fileSize;

        [SerializeField]
        public List<string> tags = new();

        public AssetDataCacheFile() { }

        public AssetDataCacheFile(string datasetId, string path, string extension, bool available, string description, long fileSize, List<string> tags)
        {
            this.datasetId = datasetId;
            this.path = path;
            this.extension = extension;
            this.available = available;
            this.description = description;
            this.fileSize = fileSize;
            this.tags = tags ?? new List<string>();
        }
    }

    [Serializable]
    class AssetDataCacheMetadata
    {
        [SerializeField]
        public List<AssetDataCacheStringMetadata> textMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheBooleanMetadata> booleanMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheNumberMetadata> numberMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheUrlMetadata> urlMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheStringMetadata> timestampMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheStringMetadata> userMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheStringMetadata> singleSelectionMedatatas = new();

        [SerializeField]
        public List<AssetDataCacheStringListMetadata> multiSelectionMedatatas = new();
    }

    [Serializable]
    class AssetDataCacheStringMetadata
    {
        [SerializeField]
        public string key;

        [SerializeField]
        public string displayName;

        [SerializeField]
        public string value;

        public AssetDataCacheStringMetadata() { }

        public AssetDataCacheStringMetadata(string key, string displayName, string value)
        {
            this.key = key;
            this.displayName = displayName;
            this.value = value;
        }
    }

    [Serializable]
    class AssetDataCacheBooleanMetadata
    {
        [SerializeField]
        public string key;

        [SerializeField]
        public string displayName;

        [SerializeField]
        public bool value;

        public AssetDataCacheBooleanMetadata() { }

        public AssetDataCacheBooleanMetadata(string key, string displayName, bool value)
        {
            this.key = key;
            this.displayName = displayName;
            this.value = value;
        }
    }

    [Serializable]
    class AssetDataCacheNumberMetadata
    {
        [SerializeField]
        public string key;

        [SerializeField]
        public string displayName;

        [SerializeField]
        public double value;

        public AssetDataCacheNumberMetadata() { }

        public AssetDataCacheNumberMetadata(string key, string displayName, double value)
        {
            this.key = key;
            this.displayName = displayName;
            this.value = value;
        }
    }

    [Serializable]
    struct AssetDataCacheUri
    {
        [SerializeField]
        public string uri;

        [SerializeField]
        public string label;

        public AssetDataCacheUri(string uri, string label)
        {
            this.uri = uri;
            this.label = label;
        }
    }

    [Serializable]
    class AssetDataCacheUrlMetadata
    {
        [SerializeField]
        public string key;

        [SerializeField]
        public string displayName;

        [SerializeField]
        public AssetDataCacheUri value;

        public AssetDataCacheUrlMetadata() { }

        public AssetDataCacheUrlMetadata(string key, string displayName, AssetDataCacheUri value)
        {
            this.key = key;
            this.displayName = displayName;
            this.value = value;
        }
    }

    [Serializable]
    class AssetDataCacheStringListMetadata
    {
        [SerializeField]
        public string key;

        [SerializeField]
        public string displayName;

        [SerializeField]
        public List<string> value = new();

        public AssetDataCacheStringListMetadata() { }

        public AssetDataCacheStringListMetadata(string key, string displayName, List<string> value)
        {
            this.key = key;
            this.displayName = displayName;
            this.value = value ?? new List<string>();
        }
    }

    [Serializable]
    class AssetDataCacheLinkedProject
    {
        [SerializeField]
        public string organizationId;

        [SerializeField]
        public string projectId;

        public AssetDataCacheLinkedProject() { }

        public AssetDataCacheLinkedProject(string organizationId, string projectId)
        {
            this.organizationId = organizationId;
            this.projectId = projectId;
        }
    }

    [Serializable]
    class AssetDataCacheLinkedCollection
    {
        [SerializeField]
        public string organizationId;

        [SerializeField]
        public string projectId;

        [SerializeField]
        public string collectionPath;

        public AssetDataCacheLinkedCollection() { }

        public AssetDataCacheLinkedCollection(string organizationId, string projectId, string collectionPath)
        {
            this.organizationId = organizationId;
            this.projectId = projectId;
            this.collectionPath = collectionPath;
        }
    }
}
