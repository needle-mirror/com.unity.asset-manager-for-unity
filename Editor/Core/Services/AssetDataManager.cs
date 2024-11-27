using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    class AssetChangeArgs
    {
        public IReadOnlyCollection<TrackedAssetIdentifier> Added = Array.Empty<TrackedAssetIdentifier>();
        public IReadOnlyCollection<TrackedAssetIdentifier> Removed = Array.Empty<TrackedAssetIdentifier>();
        public IReadOnlyCollection<TrackedAssetIdentifier> Updated = Array.Empty<TrackedAssetIdentifier>();
    }

    interface IAssetDataManager : IService
    {
        event Action<AssetChangeArgs> ImportedAssetInfoChanged;
        event Action<AssetChangeArgs> AssetDataChanged;

        IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos { get; }

        void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos);
        void AddOrUpdateGuidsToImportedAssetInfo(BaseAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos);
        void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove);
        void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<BaseAssetData> assetDatas);
        ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier assetIdentifier);
        void RemoveImportedAssetInfo(AssetIdentifier assetIdentifier);
        List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string guid);
        string GetImportedFileGuid(AssetIdentifier assetIdentifier, string path);
        BaseAssetData GetAssetData(AssetIdentifier assetIdentifier);
        List<BaseAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids);
        Task<BaseAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token);
        bool IsInProject(AssetIdentifier id);
    }

    [Serializable]
    class AssetDataManager : BaseService<IAssetDataManager>, IAssetDataManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        ImportedAssetInfo[] m_SerializedImportedAssetInfos = Array.Empty<ImportedAssetInfo>();

        [SerializeReference]
        BaseAssetData[] m_SerializedAssetData = Array.Empty<BaseAssetData>();

        readonly Dictionary<TrackedAssetIdentifier, BaseAssetData> m_AssetData = new();
        readonly Dictionary<TrackedAssetIdentifier, ImportedAssetInfo> m_TrackedIdentifierToImportedAssetInfoLookup = new();
        readonly Dictionary<string, List<ImportedAssetInfo>> m_FileGuidToImportedAssetInfosMap = new();

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        public event Action<AssetChangeArgs> ImportedAssetInfoChanged = delegate { };
        public event Action<AssetChangeArgs> AssetDataChanged = delegate { };

        public IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos => m_TrackedIdentifierToImportedAssetInfoLookup.Values;

        [ServiceInjection]
        public void Inject(IAssetsProvider assetsProvider)
        {
            m_AssetsProvider = assetsProvider;
        }

        public override void OnEnable()
        {
            m_AssetsProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override void OnDisable()
        {
            m_AssetsProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }

        void OnAuthenticationStateChanged(AuthenticationState newState)
        {
            if (newState != AuthenticationState.LoggedIn)
            {
                m_AssetData.Clear();
            }
        }

        public void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos)
        {
            allImportedInfos ??= Array.Empty<ImportedAssetInfo>();
            var oldAssetIds = m_TrackedIdentifierToImportedAssetInfoLookup.Keys.ToHashSet();
            m_FileGuidToImportedAssetInfosMap.Clear();
            m_TrackedIdentifierToImportedAssetInfoLookup.Clear();

            var added = new HashSet<TrackedAssetIdentifier>();
            var updated = new HashSet<TrackedAssetIdentifier>();
            foreach (var info in allImportedInfos)
            {
                var id = new TrackedAssetIdentifier(info.Identifier);

                AddOrUpdateImportedAssetInfo(info);
                if (oldAssetIds.Contains(id))
                {
                    updated.Add(id);
                }
                else
                {
                    added.Add(id);
                }
            }

            foreach (var newInfo in m_TrackedIdentifierToImportedAssetInfoLookup.Values)
            {
                oldAssetIds.Remove(new TrackedAssetIdentifier(newInfo.Identifier));
            }

            if (added.Count + updated.Count + oldAssetIds.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs
                    {Added = added, Removed = oldAssetIds, Updated = updated});
            }
        }

        public void AddOrUpdateGuidsToImportedAssetInfo(BaseAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos)
        {
            if (assetData == null)
                return;

            var added = new HashSet<TrackedAssetIdentifier>();
            var updated = new HashSet<TrackedAssetIdentifier>();

            var id = new TrackedAssetIdentifier(assetData.Identifier);
            var info = new ImportedAssetInfo(assetData, fileInfos ?? Array.Empty<ImportedFileInfo>());

            if (GetImportedAssetInfo(assetData.Identifier) == null)
            {
                added.Add(id);
            }
            else
            {
                updated.Add(id);
            }

            AddOrUpdateImportedAssetInfo(info);

            ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { Added = added, Updated = updated });
        }

        public void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove)
        {
            guidsToRemove ??= Array.Empty<string>();
            if (guidsToRemove.Count <= 0)
                return;

            var updated = new List<TrackedAssetIdentifier>();
            var removed = new List<TrackedAssetIdentifier>();
            foreach (var fileGuid in guidsToRemove)
            {
                var assetInfos = GetImportedAssetInfosFromFileGuid(fileGuid);
                if (assetInfos == null)
                    continue;

                m_FileGuidToImportedAssetInfosMap.Remove(fileGuid);
                foreach (var asset in assetInfos)
                {
                    var id = new TrackedAssetIdentifier(asset.Identifier);

                    asset.FileInfos.RemoveAll(i => i.Guid == fileGuid);
                    if (asset.FileInfos.Count > 0)
                    {
                        updated.Add(id);
                    }
                    else
                    {
                        updated.Remove(id);
                        removed.Add(id);
                        m_TrackedIdentifierToImportedAssetInfoLookup.Remove(id);
                    }
                }
            }

            if (updated.Count + removed.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { Removed = removed, Updated = updated });
            }
        }

        public void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<BaseAssetData> assetDatas)
        {
            var assetChangeArgs = new AssetChangeArgs();
            var updated = new HashSet<TrackedAssetIdentifier>();
            var added = new HashSet<TrackedAssetIdentifier>();

            foreach (var assetData in assetDatas)
            {
                var id = new TrackedAssetIdentifier(assetData.Identifier);

                if (m_AssetData.TryGetValue(id, out var existingAssetData))
                {
                    if (existingAssetData.IsTheSame(assetData))
                        continue;

                    updated.Add(id);
                }
                else
                {
                    added.Add(id);
                }

                m_AssetData[id] = assetData;
            }

            assetChangeArgs.Added = added;
            assetChangeArgs.Updated = updated;
            AssetDataChanged?.Invoke(assetChangeArgs);
        }

        public ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier assetIdentifier)
        {
            return assetIdentifier?.IsIdValid() == true && m_TrackedIdentifierToImportedAssetInfoLookup.TryGetValue(new TrackedAssetIdentifier(assetIdentifier), out var result)
                ? result
                : null;
        }

        public void RemoveImportedAssetInfo(AssetIdentifier assetIdentifier)
        {
            var id = new TrackedAssetIdentifier(assetIdentifier);

            if (m_TrackedIdentifierToImportedAssetInfoLookup.TryGetValue(id, out var importedAssetInfo))
            {
                m_TrackedIdentifierToImportedAssetInfoLookup.Remove(id);

                // Remove all file guids related to that imported asset too
                foreach (var fileInfo in importedAssetInfo.FileInfos)
                {
                    if (m_FileGuidToImportedAssetInfosMap.TryGetValue(fileInfo.Guid, out var importedAssetInfos))
                    {
                        var entry = importedAssetInfos.Find(info => info.Identifier.Equals(assetIdentifier));

                        if (entry == null)
                            continue;

                        importedAssetInfos.Remove(entry);

                        if (importedAssetInfos.Count == 0)
                        {
                            m_FileGuidToImportedAssetInfosMap.Remove(fileInfo.Guid);
                        }
                    }
                }
            }

            ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { Removed = new[] { id } });
        }

        public List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string guid)
        {
            return m_FileGuidToImportedAssetInfosMap.GetValueOrDefault(guid);
        }

        public string GetImportedFileGuid(AssetIdentifier assetIdentifier, string path)
        {
            if (assetIdentifier == null)
                return null;

            var importedInfo = GetImportedAssetInfo(assetIdentifier);
            var importedFileInfo = importedInfo?.FileInfos?.Find(f => Utilities.ComparePaths(f.OriginalPath, path));

            return importedFileInfo?.Guid;
        }

        public BaseAssetData GetAssetData(AssetIdentifier assetIdentifier)
        {
            if (assetIdentifier?.IsIdValid() != true)
            {
                return null;
            }

            var id = new TrackedAssetIdentifier(assetIdentifier);

            if (m_TrackedIdentifierToImportedAssetInfoLookup.TryGetValue(id, out var info))
            {
                return info?.AssetData;
            }

            return m_AssetData.GetValueOrDefault(id);
        }

        public List<BaseAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids)
        {
            var result = new List<BaseAssetData>();

            foreach (var id in ids)
            {
                var assetData = GetAssetData(id);
                if (assetData != null)
                {
                    result.Add(assetData);
                }
            }

            return result;
        }

        public async Task<BaseAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var assetData = GetAssetData(assetIdentifier);

            if (assetData != null)
            {
                return assetData;
            }

            try
            {
                var assetProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                assetData = await assetProvider.GetAssetAsync(assetIdentifier, token);
            }
            catch (ForbiddenException)
            {
                // Unavailable
            }
            catch (NotFoundException)
            {
                // Not found
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (assetData != null)
            {
                AddOrUpdateAssetDataFromCloudAsset(new[] { assetData });
            }

            return assetData;
        }

        public bool IsInProject(AssetIdentifier id)
        {
            Utilities.DevAssert(!id.IsLocal(), "Calling IsInProject on a local identifier doesn't make sense.");

            if (id.IsLocal())
            {
                return false;
            }

            return GetImportedAssetInfo(id) != null;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedImportedAssetInfos = m_FileGuidToImportedAssetInfosMap.Values.SelectMany(x => x).ToArray();
            m_SerializedAssetData = m_AssetData.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var info in m_SerializedImportedAssetInfos)
            {
                AddOrUpdateImportedAssetInfo(info);
            }

            foreach (var data in m_SerializedAssetData)
            {
                m_AssetData[new TrackedAssetIdentifier(data.Identifier)] = data;
            }
        }

        void AddOrUpdateImportedAssetInfo(ImportedAssetInfo info)
        {
            // This method updates both m_TrackedIdentifierToImportedAssetInfoLookup and m_FileGuidToImportedAssetInfosMap

            var trackId = new TrackedAssetIdentifier(info.Identifier);

            // Iterate through every imported file related to that imported asset
            foreach (var fileInfo in info.FileInfos)
            {
                if (!m_FileGuidToImportedAssetInfosMap.TryGetValue(fileInfo.Guid, out var importedAssetInfos))
                {
                    // If that file Guid is not related to any existing imported asset, we can simply create a new entry to track it
                    m_FileGuidToImportedAssetInfosMap[fileInfo.Guid] = new List<ImportedAssetInfo> { info };
                }
                else
                {
                    // Otherwise, we need to verify to which asset info this file is related to
                    // If the file was related to a different (or same) version, we need to untrack it first, before tracking it again using updated imported info
                    // Maybe a dictionary would have improved the readability of this code
                    var duplicate = importedAssetInfos.Find(i => new TrackedAssetIdentifier(i.Identifier).Equals(trackId));
                    if (duplicate != null)
                    {
                        importedAssetInfos.Remove(duplicate);
                    }

                    importedAssetInfos.Add(info);
                }
            }

            // m_TrackedIdentifierToImportedAssetInfoLookup must contain the updated imported info, ignoring it's version, because only one version can be imported at a time.
            m_TrackedIdentifierToImportedAssetInfoLookup[trackId] = info;
        }
    }
}
