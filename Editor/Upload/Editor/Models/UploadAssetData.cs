using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    [Serializable]
    class UploadAssetData : BaseAssetData
    {
        // TODO Ideally we should use a class that contains both the status (pending or new) and actual sequence number
        public static readonly int NewVersionSequenceNumber = -42; // This can be any number as long as it's negative because versions from the server are always positive

        [SerializeField]
        // Technically we should be using UploadAssetData here but Unity maximum serialization depth prevents us from doing so.
        // Instead, we use AssetIdentifier and resolve the UploadAssetData later when needed.
        List<AssetIdentifier> m_Dependencies = new();

        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_AssetGuid;

        [SerializeField]
        string m_AssetPath;

        [SerializeField]
        bool m_Ignored;

        [SerializeField]
        bool m_IsDependency;

        [SerializeField]
        AssetType m_AssetType;

        [SerializeField]
        List<string> m_MainFileGuids = new();

        [SerializeField]
        List<string> m_AdditionalFileGuids = new();

        [SerializeField]
        List<string> m_Tags;

        Task<Texture2D> m_GetThumbnailTask;
        Task<AssetDataStatusType> m_PreviewStatusTask;

        [SerializeField]
        AssetIdentifier m_ExistingAssetIdentifier;

        [SerializeField]
        AssetDataStatusType m_SelfStatus; // This is the status of the asset itself, not the status of the asset in relation to dependencies

        [SerializeField]
        AssetDataStatusType m_ResolvedStatus; // This is the status of the asset in relation to dependencies

        [SerializeField]
        int m_ExistingSequenceNumber;

        [SerializeReference]
        List<IMetadata> m_Metadata = new();

        [SerializeField]
        UploadFilePathMode m_FilePathMode;

        static bool s_UseAdvancedPreviewer = false;

        public override string Name => m_Name;
        public string Guid => m_AssetGuid;
        public override AssetIdentifier Identifier => m_Identifier;

        //Upload Asset Data Should have it's own settings so it can rebuild itself. If settings are changed, it rebuilds itself. again mainly files
        //So you can then change the data from anywhere without going through the staging that lives in the upload page'

        public UploadFilePathMode FilePathMode
        {
            get => m_FilePathMode;
            set
            {
                if (m_FilePathMode == value)
                    return;

                m_FilePathMode = value;
                ResolveFilePaths();
            }
        }

        public override int SequenceNumber
        {
            get
            {
                if (!CanBeUploaded && m_ExistingAssetIdentifier != null)
                    return m_ExistingSequenceNumber;

                return NewVersionSequenceNumber;
            }
        }

        public override int ParentSequenceNumber => -1;
        public override string Changelog => "";
        public override AssetType AssetType => m_AssetType;
        public override string Status => "Local";
        public override DateTime? Updated => null;
        public override DateTime? Created => null;
        public override IEnumerable<string> Tags => m_Tags;
        public override string Description => "";
        public override string CreatedBy => "";
        public override string UpdatedBy => "";

        public override List<IMetadata> Metadata
        {
            get => m_Metadata;
            set => m_Metadata = value;
        }

        public override IEnumerable<BaseAssetDataFile> UVCSFiles => Array.Empty<BaseAssetDataFile>();

        public bool IsIgnored
        {
            get => m_Ignored;
            set
            {
                m_Ignored = value;
                InvokeEvent(AssetDataEventType.ToggleValueChanged);
            }
        }

        public void NotifyIgnoredChanged()
        {
            InvokeEvent(AssetDataEventType.ToggleValueChanged);
        }

        public bool IsDependency
        {
            get => m_IsDependency;
            set => m_IsDependency = value;
        }

        public bool CanBeIgnored
        {
            get
            {
                if (m_ResolvedStatus == AssetDataStatusType.UploadSkip)
                    return false;

                if (m_ResolvedStatus == AssetDataStatusType.None)
                    return false;

                return m_IsDependency;
            }
        }

        public bool CanBeRemoved => !m_IsDependency;

        public bool CanBeUploaded
        {
            get
            {
                if (m_IsDependency && m_Ignored)
                {
                    return false;
                }

                return m_ResolvedStatus is AssetDataStatusType.UploadOverride
                    or AssetDataStatusType.UploadDuplicate
                    or AssetDataStatusType.UploadAdd;
            }
        }

        public UploadAssetData(AssetIdentifier identifier, string assetGuid, IEnumerable<string> fileAssetGuids,
            IEnumerable<UploadAssetData> dependencies, UploadFilePathMode filePathMode)
        {
            m_AssetGuid = assetGuid;

            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            m_AssetPath = assetDatabaseProxy.GuidToAssetPath(m_AssetGuid);
            m_Name = Path.GetFileNameWithoutExtension(m_AssetPath);

            m_Identifier = identifier;

            // Dependencies
            if (dependencies != null)
            {
                m_Dependencies = dependencies.Select(d => d.Identifier).ToList();
            }

            // Tags
            m_Tags = TagExtractor.ExtractFromAsset(m_AssetPath).ToList();

            // Files
            m_FilePathMode = filePathMode;
            AddFiles(fileAssetGuids, true);
        }

        public IUploadAsset GenerateUploadAsset(string organizationId, string projectId, string collectionPath)

        {
            Utilities.DevAssert(m_ResolvedStatus != AssetDataStatusType.None && m_ResolvedStatus != AssetDataStatusType.UploadSkip && m_ResolvedStatus != AssetDataStatusType.UploadOutside);

            var uploadAsset = new UploadAsset(m_Name, m_AssetGuid, m_Identifier, m_AssetType,
                SourceFiles.Cast<UploadAssetDataFile>().Select(f => f.GenerateUploadFile()),
                m_Tags,
                ResolveDependencyIdentifiers(this),
                m_ResolvedStatus == AssetDataStatusType.UploadOverride ? m_ExistingAssetIdentifier : null,
                new ProjectIdentifier(organizationId, projectId), collectionPath);

            return uploadAsset;
        }

        public override IEnumerable<AssetIdentifier> Dependencies => m_Dependencies;

        public override IEnumerable<BaseAssetData> Versions => Array.Empty<BaseAssetData>();

        public override async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null,
            CancellationToken token = default)
        {
            if (m_GetThumbnailTask == null)
            {
                var asset = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>().LoadAssetAtPath(m_AssetPath);

                if (asset != null)
                {
                    m_GetThumbnailTask = s_UseAdvancedPreviewer
                        ? AssetPreviewer.GenerateAdvancedPreview(asset, m_AssetPath)
                        : AssetPreviewer.GetDefaultPreviewTexture(asset);
                }
            }

            var texture = m_GetThumbnailTask != null ? await m_GetThumbnailTask : null;
            m_GetThumbnailTask = null;

            Thumbnail = texture;
            callback?.Invoke(Identifier, texture);
        }

        public async Task ResolveSelfStatus(string organizationId, string projectId, UploadAssetMode uploadMode, CancellationToken token)
        {
            ResetPreviewStatus();

            m_PreviewStatusTask ??= ResolveSelfStatusInternalAsync(organizationId, projectId, uploadMode, token);

            try
            {
                m_SelfStatus = await m_PreviewStatusTask;
            }
            catch (Exception)
            {
                m_PreviewStatusTask = null;
                throw;
            }

            m_PreviewStatusTask = null;
        }

        public void ResolveFinalStatus(UploadAssetMode uploadMode)
        {
            Utilities.DevAssert(m_SelfStatus != AssetDataStatusType.None, "ResolveSelfStatus must be called before ResolveFinalStatus");

            // If the asset is already modified, no need to check for dependencies since they will not change the fact that the asset is modified
            // Also, if the asset is not re-uploading to a target, we have nothing to compare too
            AssetDataStatusType resolvedStatus;

            if (m_SelfStatus != AssetDataStatusType.UploadSkip || m_ExistingAssetIdentifier == null)
            {
                resolvedStatus = m_SelfStatus;
            }
            else if (uploadMode == UploadAssetMode.ForceNewAsset)
            {
                resolvedStatus = m_SelfStatus;
            }
            else
            {
                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                var importedAssetInfo = assetDataManager.GetImportedAssetInfo(m_ExistingAssetIdentifier);

                var hasModifiedDependencies = HasModifiedDependencies(importedAssetInfo?.AssetData);

                resolvedStatus = hasModifiedDependencies ? AssetDataStatusType.UploadOverride : m_SelfStatus;
            }

            m_ResolvedStatus = resolvedStatus;
            PreviewStatus = GetPreviewStatusTypes();
        }

        public void ResetPreviewStatus()
        {
            m_SelfStatus = AssetDataStatusType.None;
            m_ResolvedStatus = AssetDataStatusType.None;
            PreviewStatus = GetPreviewStatusTypes();
        }

        public async Task<bool> IsLocallyModifiedIgnoreDependenciesAsync(ImportedAssetInfo importedAssetInfo, CancellationToken token = default)
        {
            if (importedAssetInfo?.AssetData == null)
            {
                return false;
            }

            // Because Metadata files are not added to the imported asset info, we need to ignore them when comparing file count
            var sourceFiles = SourceFiles.Where(s => !MetafilesHelper.IsMetafile(s.Path)).Select(s => s.Path).ToList();

            return await Utilities.IsLocallyModifiedIgnoreDependenciesAsync(sourceFiles, importedAssetInfo, token);
        }

        public bool HasModifiedDependencies(BaseAssetData targetAssetData)
        {
            if (targetAssetData == null)
            {
                return false;
            }

            var dependencies = ResolveDependencyIdentifiers(this).ToList();

            return Utilities.CompareDependencies(dependencies, targetAssetData.Dependencies.ToList());
        }

        public override Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetDataStatusType>> callback = null,
            CancellationToken token = default)
        {
            callback?.Invoke(m_Identifier, PreviewStatus);
            return Task.CompletedTask;
        }

        public override Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback = null,
            CancellationToken token = default)
        {
            callback?.Invoke(Identifier, PrimaryExtension);
            return Task.CompletedTask;
        }

        public override Task RefreshPropertiesAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public override Task RefreshVersionsAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public override Task RefreshDependenciesAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        async Task<AssetDataStatusType> ResolveSelfStatusInternalAsync(string organizationId, string projectId, UploadAssetMode uploadMode, CancellationToken token)
        {
            if (!DependencyUtils.IsPathInsideAssetsFolder(m_AssetPath))
            {
                return AssetDataStatusType.UploadOutside;
            }

            var existingAssetData = AssetDataDependencyHelper.GetAssetAssociatedWithGuid(m_AssetGuid, organizationId, projectId);
            m_ExistingAssetIdentifier = existingAssetData?.Identifier;
            m_ExistingSequenceNumber = existingAssetData?.SequenceNumber ?? -1;

            if (m_ExistingAssetIdentifier == null)
            {
                return AssetDataStatusType.UploadAdd;
            }

            if (uploadMode == UploadAssetMode.ForceNewAsset)
            {
                return AssetDataStatusType.UploadDuplicate;
            }

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var importedAssetInfo = assetDataManager.GetImportedAssetInfo(m_ExistingAssetIdentifier);

            if (importedAssetInfo == null)
            {
                return AssetDataStatusType.UploadAdd;
            }

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var task = assetsProvider.AssetExistsOnCloudAsync(importedAssetInfo.AssetData, token);

            try
            {
                var exists = await task;

                if (!exists)
                {
                    return AssetDataStatusType.UploadAdd;
                }
            }
            catch (Exception e)
            {
                Utilities.DevLogException(e);
                // The user might not have access to that project
                // We cannot recycle the asset in this case.
            }

            switch (uploadMode)
            {
                case UploadAssetMode.SkipIdentical:

                    if (await IsLocallyModifiedIgnoreDependenciesAsync(importedAssetInfo, token))
                    {
                        return AssetDataStatusType.UploadOverride;
                    }

                    return AssetDataStatusType.UploadSkip;

                case UploadAssetMode.ForceNewVersion:
                    return AssetDataStatusType.UploadOverride;

                default:
                    return AssetDataStatusType.Imported;
            }
        }

        void ResolveFilePaths()
        {
            var allFiles = m_MainFileGuids.Union(m_AdditionalFileGuids).Distinct();

            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            var filePaths = allFiles.Select(guid => assetDatabaseProxy.GuidToAssetPath(guid)).ToList();

            var processedPaths = new HashSet<string>();

            var commonPath = m_FilePathMode == UploadFilePathMode.Compact ? Utilities.ExtractCommonFolder(filePaths) : null;

            var files = new List<UploadAssetDataFile>();

            foreach (var filePath in filePaths)
            {
                var sanitizedPath = filePath.Replace('\\', '/').ToLower();

                if (!processedPaths.Add(sanitizedPath))
                    continue;

                if (!AddAssetAndItsMetaFile(files, filePath, commonPath, m_FilePathMode))
                {
                    Debug.LogWarning($"Asset {filePath} is already added to the upload list.");
                }
            }

            // Update Asset Type since it might have changed
            var extensions = files.Select(e => Path.GetExtension(e.Path)).ToHashSet();
            var primaryExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions);
            var unityAssetType = AssetDataTypeHelper.GetUnityAssetType(primaryExtension);

            m_AssetType = unityAssetType.ConvertUnityAssetTypeToAssetType();

            // Asset Type
            SourceFiles = files;
        }

        IEnumerable<AssetDataStatusType> GetPreviewStatusTypes()
        {
            if (m_IsDependency)
            {
                yield return AssetDataStatusType.Linked;
            }

            if (m_ResolvedStatus != AssetDataStatusType.None)
            {
                yield return m_ResolvedStatus;
            }
        }

        static IEnumerable<AssetIdentifier> ResolveDependencyIdentifiers(UploadAssetData sourceAssetData)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            return sourceAssetData.m_Dependencies
                .Select(id => (UploadAssetData)assetDataManager.GetAssetData(id))
                .Select(dependency => dependency.CanBeUploaded ? dependency.m_Identifier : dependency.m_ExistingAssetIdentifier)
                .Where(identifier => identifier != null);
        }

        public void AddFiles(IEnumerable<string> fileAssetGuids, bool mainFiles)
        {
            var allFiles = m_MainFileGuids.Union(m_AdditionalFileGuids).ToHashSet();
            var targetList = mainFiles ? m_MainFileGuids : m_AdditionalFileGuids;

            foreach (var guid in fileAssetGuids)
            {
                if (allFiles.Contains(guid))
                    continue;

                targetList.Add(guid);
            }

            ResolveFilePaths();
        }

        public void RemoveFiles(IEnumerable<string> fileAssetGuids, bool mainFiles)
        {
            var targetList = mainFiles ? m_MainFileGuids : m_AdditionalFileGuids;

            foreach (var guid in fileAssetGuids)
            {
                targetList.Remove(guid);
            }

            ResolveFilePaths();
        }

        public override bool CanRemovedFile(BaseAssetDataFile assetDataFile)
        {
            return m_AdditionalFileGuids.Contains(assetDataFile.Guid);
        }

        public override void RemoveFile(BaseAssetDataFile assetDataFile)
        {
            m_AdditionalFileGuids.Remove(assetDataFile.Guid);
            ResolveFilePaths();
        }

        public void RemoveAllAdditionalFiles()
        {
            m_AdditionalFileGuids.Clear();
            ResolveFilePaths();
        }

        public bool HasAdditionalFiles()
        {
            return m_AdditionalFileGuids.Count > 0;
        }

        static bool AddAssetAndItsMetaFile(IList<UploadAssetDataFile> addedFiles, string assetPath, string commonPath, UploadFilePathMode filePathMode)
        {
            string dst;

            switch (filePathMode)
            {
                case UploadFilePathMode.Compact:
                    if (string.IsNullOrEmpty(commonPath))
                    {
                        dst = Utilities.GetPathRelativeToAssetsFolder(assetPath);
                    }
                    else
                    {
                        var normalizedPath = Utilities.NormalizePathSeparators(assetPath);
                        var commonPathNormalized = Utilities.NormalizePathSeparators(commonPath);

                        Utilities.DevAssert(normalizedPath.StartsWith(commonPathNormalized));
                        dst = normalizedPath[commonPathNormalized.Length..];
                    }

                    break;

                case UploadFilePathMode.Flatten:
                    dst = GetFlattenPath(addedFiles, assetPath);
                    break;

                default:
                    dst = Utilities.GetPathRelativeToAssetsFolder(assetPath);
                    break;
            }

            if (addedFiles.Any(e => Utilities.ComparePaths(e.Path, assetPath)))
            {
                return false;
            }

            addedFiles.Add(new UploadAssetDataFile(assetPath,
                dst, null, k_DefaultFileTags));

            var metaFileSourcePath = MetafilesHelper.AssetMetaFile(assetPath);
            if (!string.IsNullOrEmpty(metaFileSourcePath))
            {
                addedFiles.Add(new UploadAssetDataFile(metaFileSourcePath,
                    dst + MetafilesHelper.MetaFileExtension, null, k_DefaultFileTags));
            }

            return true;
        }

        static string GetFlattenPath(ICollection<UploadAssetDataFile> files, string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            return Utilities.GetUniqueFilename(files.Select(e => e.Path).ToArray(), fileName);
        }

        static readonly List<string> k_DefaultFileTags = new();
    }
}
