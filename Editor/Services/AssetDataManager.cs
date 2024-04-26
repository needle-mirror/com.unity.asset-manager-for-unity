using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class AssetChangeArgs
    {
        public IReadOnlyCollection<AssetIdentifier> Added = Array.Empty<AssetIdentifier>();
        public IReadOnlyCollection<AssetIdentifier> Removed = Array.Empty<AssetIdentifier>();
        public IReadOnlyCollection<AssetIdentifier> Updated = Array.Empty<AssetIdentifier>();
    }

    interface IAssetDataManager : IService
    {
        event Action<AssetChangeArgs> ImportedAssetInfoChanged;
        event Action<AssetChangeArgs> AssetDataChanged;

        IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos { get; }

        void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos);
        void AddGuidsToImportedAssetInfo(IAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos);
        void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> filesToRemove);
        void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas);
        ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id);
        void RemoveImportedAssetInfo(AssetIdentifier id);
        List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string guid);
        IAssetData GetAssetData(AssetIdentifier id);
        List<IAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids);
        Task<IAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token);
        bool IsInProject(AssetIdentifier id);
    }

    [Serializable]
    class AssetDataManager : BaseService<IAssetDataManager>, IAssetDataManager, ISerializationCallbackReceiver
    {
        [SerializeField] 
        ImportedAssetInfo[] m_SerializedImportedAssetInfos = Array.Empty<ImportedAssetInfo>();

        [SerializeReference] 
        IAssetData[] m_SerializedAssetData = Array.Empty<IAssetData>();

        [SerializeReference] 
        IUnityConnectProxy m_UnityConnect;

        readonly Dictionary<AssetIdentifier, IAssetData> m_AssetData = new();
        readonly Dictionary<AssetIdentifier, ImportedAssetInfo> m_AssetIdToImportedAssetInfoLookup = new();
        readonly Dictionary<string, List<ImportedAssetInfo>> m_FileGuidToImportedAssetInfosMap = new();

        public event Action<AssetChangeArgs> ImportedAssetInfoChanged = delegate { };
        public event Action<AssetChangeArgs> AssetDataChanged = delegate { };

        public IReadOnlyCollection<ImportedAssetInfo> ImportedAssetInfos => m_AssetIdToImportedAssetInfoLookup.Values;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnect)
        {
            m_UnityConnect = unityConnect;
        }

        public override void OnEnable()
        {
            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public override void OnDisable()
        {
            Services.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }

        void OnAuthenticationStateChanged()
        {
            if (Services.AuthenticationState != AuthenticationState.LoggedIn)
            {
                m_AssetData.Clear();
            }
        }

        public void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos)
        {
            allImportedInfos ??= Array.Empty<ImportedAssetInfo>();
            var oldAssetIds = m_AssetIdToImportedAssetInfoLookup.Keys.ToHashSet();
            m_FileGuidToImportedAssetInfosMap.Clear();
            m_AssetIdToImportedAssetInfoLookup.Clear();

            var added = new HashSet<AssetIdentifier>();
            var updated = new HashSet<AssetIdentifier>();
            foreach (var info in allImportedInfos)
            {
                AddImportedAssetInfo(info);
                if (oldAssetIds.Contains(info.id))
                {
                    updated.Add(info.id);
                }
                else
                {
                    added.Add(info.id);
                }
            }

            foreach (var newInfo in m_AssetIdToImportedAssetInfoLookup.Values)
            {
                oldAssetIds.Remove(newInfo.id);
            }

            if (added.Count + updated.Count + oldAssetIds.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs
                    { Added = added, Removed = oldAssetIds, Updated = updated });
            }
        }

        public void AddGuidsToImportedAssetInfo(IAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos)
        {
            fileInfos ??= Array.Empty<ImportedFileInfo>();
            if (fileInfos.Count <= 0)
                return;

            var added = new HashSet<AssetIdentifier>();
            var updated = new HashSet<AssetIdentifier>();
            var info = GetImportedAssetInfo(assetData
                .Identifier); // this could be a file in an asset and not an asset but/ w/e
            if (info == null)
            {
                info = new ImportedAssetInfo(assetData, fileInfos);
                AddImportedAssetInfo(info);
                added.Add(assetData.Identifier);
            }
            else
            {
                foreach (var fileInfo in fileInfos)
                {
                    if (info.FileInfos.Exists(i => i.Guid == fileInfo.Guid)) // won't this always continue
                        continue;

                    info.FileInfos.Add(fileInfo);
                    if (m_FileGuidToImportedAssetInfosMap.TryGetValue(fileInfo.Guid, out var value))
                    {
                        value.Add(info);
                    }
                    else
                    {
                        m_FileGuidToImportedAssetInfosMap.Add(fileInfo.Guid, new List<ImportedAssetInfo> { info });
                    }

                    updated.Add(assetData.Identifier);
                }
            }

            if (added.Count + updated.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs
                    { Added = added, Removed = Array.Empty<AssetIdentifier>(), Updated = updated });
            }
        }

        public void RemoveFilesFromImportedAssetInfos(IReadOnlyCollection<string> filesToRemove)
        {
            filesToRemove ??= Array.Empty<string>();
            if (filesToRemove.Count <= 0)
                return;

            var updated = new List<AssetIdentifier>();
            var removed = new List<AssetIdentifier>();
            foreach (var fileGuid in filesToRemove)
            {
                var assetInfos = GetImportedAssetInfosFromFileGuid(fileGuid);
                if (assetInfos == null)
                    continue;

                m_FileGuidToImportedAssetInfosMap.Remove(fileGuid);
                foreach (var asset in assetInfos)
                {
                    asset.FileInfos.RemoveAll(i => i.Guid == fileGuid);
                    if (asset.FileInfos.Count > 0)
                    {
                        updated.Add(asset.id);
                    }
                    else
                    {
                        updated.Remove(asset.id);
                        removed.Add(asset.id);
                        m_AssetIdToImportedAssetInfoLookup.Remove(asset.id);
                    }
                }
            }

            if (updated.Count + removed.Count > 0)
            {
                ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs
                    { Added = Array.Empty<AssetIdentifier>(), Removed = removed, Updated = updated });
            }
        }

        public void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas)
        {
            var assetChangeArgs = new AssetChangeArgs();
            var updated = new HashSet<AssetIdentifier>();
            var added = new HashSet<AssetIdentifier>();

            foreach (var assetData in assetDatas)
            {
                if (m_AssetData.TryGetValue(assetData.Identifier, out var existingAssetData))
                {
                    if (existingAssetData.IsTheSame(assetData))
                        continue;

                    updated.Add(assetData.Identifier);
                }
                else
                {
                    added.Add(assetData.Identifier);
                }

                m_AssetData[assetData.Identifier] = assetData;
            }

            assetChangeArgs.Added = added;
            assetChangeArgs.Updated = updated;
            AssetDataChanged?.Invoke(assetChangeArgs);
        }

        public ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id)
        {
            return id?.IsIdValid() == true && m_AssetIdToImportedAssetInfoLookup.TryGetValue(id, out var result)
                ? result
                : null;
        }

        public void RemoveImportedAssetInfo(AssetIdentifier id)
        {
            m_AssetIdToImportedAssetInfoLookup.Remove(id);
            ImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { Removed = new[] { id } });
        }

        public List<ImportedAssetInfo> GetImportedAssetInfosFromFileGuid(string fileGuid)
        {
            return m_FileGuidToImportedAssetInfosMap.GetValueOrDefault(fileGuid);
        }

        public IAssetData GetAssetData(AssetIdentifier id)
        {
            if (id?.IsIdValid() != true)
            {
                return null;
            }

            if (m_AssetIdToImportedAssetInfoLookup.TryGetValue(id, out var info))
            {
                return info?.AssetData;
            }

            return m_AssetData.TryGetValue(id, out var result) ? result : null;
        }

        public List<IAssetData> GetAssetsData(IEnumerable<AssetIdentifier> ids)
        {
            List<IAssetData> result = new List<IAssetData>();

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

        public async Task<IAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var assetData = GetAssetData(assetIdentifier);

            if (assetData != null)
            {
                return assetData;
            }

            var assetRepository = Services.AssetRepository; // TODO investigate how to clean this dependency

            IAsset asset = null;

            try
            {
                asset = await assetRepository.GetAssetAsync(assetIdentifier.ToAssetDescriptor(), token);
            }
            catch (ForbiddenException)
            {
                // Unavailable
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (asset != null)
            {
                assetData = new AssetData(asset);
                AddOrUpdateAssetDataFromCloudAsset(new[] { assetData });

                return assetData;
            }

            return null;
        }

        public bool IsInProject(AssetIdentifier id)
        {
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
                AddImportedAssetInfo(info);
            }

            foreach (var data in m_SerializedAssetData)
            {
                m_AssetData[data.Identifier] = data;
            }
        }

        void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            if (!isUserLoggedIn)
            {
                m_AssetData.Clear();
            }
        }

        void AddImportedAssetInfo(ImportedAssetInfo info)
        {
            foreach (var fileInfo in info.FileInfos)
            {
                if (m_FileGuidToImportedAssetInfosMap.ContainsKey(fileInfo.Guid))
                {
                    m_FileGuidToImportedAssetInfosMap[fileInfo.Guid].Add(info);
                }
                else
                {
                    m_FileGuidToImportedAssetInfosMap[fileInfo.Guid] = new List<ImportedAssetInfo> { info };
                }
            }

            m_AssetIdToImportedAssetInfoLookup[info.id] = info;
        }
    }
}