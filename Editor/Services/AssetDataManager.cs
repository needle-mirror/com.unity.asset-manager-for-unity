using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal class AssetChangeArgs
    {
        public IReadOnlyCollection<AssetIdentifier> added = Array.Empty<AssetIdentifier>();
        public IReadOnlyCollection<AssetIdentifier> removed = Array.Empty<AssetIdentifier>();
        public IReadOnlyCollection<AssetIdentifier> updated = Array.Empty<AssetIdentifier>();
    }

    internal interface IAssetDataManager : IService
    {
        event Action<AssetChangeArgs> onImportedAssetInfoChanged;
        event Action<AssetChangeArgs> onAssetDataChanged;

        IReadOnlyCollection<ImportedAssetInfo> importedAssetInfos { get; }

        void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos);
        void AddGuidsToImportedAssetInfo(IAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos);
        void RemoveGuidsFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove);
        void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas);
        ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id);
        void RemoveImportedAssetInfo(AssetIdentifier id);
        ImportedAssetInfo GetImportedAssetInfo(string guid);
        IAssetData GetAssetData(AssetIdentifier id);
        Task<IAssetData> GetOrSearchAssetData(AssetIdentifier assetIdentifier, CancellationToken token);
        bool IsInProject(AssetIdentifier id);
    }

    [Serializable]
    class AssetDataManager : BaseService<IAssetDataManager>, IAssetDataManager, ISerializationCallbackReceiver
    {
        readonly Dictionary<string, ImportedAssetInfo> m_GuidToImportedAssetInfoLookup = new();
        readonly Dictionary<AssetIdentifier, ImportedAssetInfo> m_AssetIdToImportedAssetInfoLookup = new();
        readonly Dictionary<AssetIdentifier, IAssetData> m_AssetData = new();

        [SerializeField]
        ImportedAssetInfo[] m_SerializedImportedAssetInfos = Array.Empty<ImportedAssetInfo>();

        [SerializeReference]
        IAssetData[] m_SerializedAssetData = Array.Empty<IAssetData>();

        public event Action<AssetChangeArgs> onImportedAssetInfoChanged = delegate {};
        public event Action<AssetChangeArgs> onAssetDataChanged = delegate {};
        public IReadOnlyCollection<ImportedAssetInfo> importedAssetInfos => m_AssetIdToImportedAssetInfoLookup.Values;

        [SerializeReference]
        IUnityConnectProxy m_UnityConnect;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnect)
        {
            m_UnityConnect = unityConnect;
        }

        public override void OnEnable()
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
        }

        public override void OnDisable()
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            if (!isUserLoggedIn)
            {
                m_AssetData.Clear();
            }
        }

        public void SetImportedAssetInfos(IReadOnlyCollection<ImportedAssetInfo> allImportedInfos)
        {
            allImportedInfos ??= Array.Empty<ImportedAssetInfo>();
            var oldAssetIds = m_AssetIdToImportedAssetInfoLookup.Keys.ToHashSet();
            m_GuidToImportedAssetInfoLookup.Clear();
            m_AssetIdToImportedAssetInfoLookup.Clear();

            var added = new HashSet<AssetIdentifier>();
            var updated = new HashSet<AssetIdentifier>();
            foreach (var info in allImportedInfos)
            {
                AddImportedAssetInfo(info);
                if (oldAssetIds.Contains(info.id))
                    updated.Add(info.id);
                else
                    added.Add(info.id);
            }

            foreach (var newInfo in m_AssetIdToImportedAssetInfoLookup.Values)
                oldAssetIds.Remove(newInfo.id);

            if (added.Count + updated.Count + oldAssetIds.Count > 0)
                onImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { added = added, removed = oldAssetIds, updated = updated });
        }

        public void AddGuidsToImportedAssetInfo(IAssetData assetData, IReadOnlyCollection<ImportedFileInfo> fileInfos)
        {
            fileInfos ??= Array.Empty<ImportedFileInfo>();
            if (fileInfos.Count <= 0)
                return;

            var added = new HashSet<AssetIdentifier>();
            var updated = new HashSet<AssetIdentifier>();
            var info = GetImportedAssetInfo(assetData.identifier);
            if (info == null)
            {
                info = new ImportedAssetInfo(assetData, fileInfos);
                AddImportedAssetInfo(info);
                added.Add(assetData.identifier);
            }
            else
            {
                foreach (var fileInfo in fileInfos)
                {
                    if (info.fileInfos.Any(i => i.guid == fileInfo.guid))
                        continue;
                    info.fileInfos.Add(fileInfo);
                    m_GuidToImportedAssetInfoLookup[fileInfo.guid] = info;
                    updated.Add(assetData.identifier);
                }
            }

            if (added.Count + updated.Count > 0)
                onImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { added = added, removed =  Array.Empty<AssetIdentifier>(), updated = updated });
        }

        public void RemoveGuidsFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove)
        {
            guidsToRemove ??= Array.Empty<string>();
            if (guidsToRemove.Count <= 0)
                return;

            var updated = new HashSet<AssetIdentifier>();
            var removed = new HashSet<AssetIdentifier>();
            foreach (var guid in guidsToRemove)
            {
                var info = GetImportedAssetInfo(guid);
                if (info == null)
                    continue;

                m_GuidToImportedAssetInfoLookup.Remove(guid);
                info.fileInfos.RemoveAll(i => i.guid == guid);
                if (info.fileInfos.Count > 0)
                {
                    updated.Add(info.id);
                }
                else
                {
                    updated.Remove(info.id);
                    removed.Add(info.id);
                    m_AssetIdToImportedAssetInfoLookup.Remove(info.id);
                }
            }

            if (updated.Count + removed.Count > 0)
                onImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { added = Array.Empty<AssetIdentifier>(), removed = removed, updated = updated });
        }

        public void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas)
        {
            var assetChangeArgs = new AssetChangeArgs();
            var updated = new HashSet<AssetIdentifier>();
            var added = new HashSet<AssetIdentifier>();

            foreach (var assetData in assetDatas)
            {
                if (m_AssetData.TryGetValue(assetData.identifier, out var existingAssetData))
                {
                    if (existingAssetData.IsTheSame(assetData))
                        continue;

                    updated.Add(assetData.identifier);
                }
                else
                {
                    added.Add(assetData.identifier);
                }

                m_AssetData[assetData.identifier] = assetData;
            }

            assetChangeArgs.added = added;
            assetChangeArgs.updated = updated;
            onAssetDataChanged?.Invoke(assetChangeArgs);
        }

        public ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id)
        {
            return id?.IsValid() == true && m_AssetIdToImportedAssetInfoLookup.TryGetValue(id, out var result) ? result : null;
        }

        public void RemoveImportedAssetInfo(AssetIdentifier id)
        {
            m_AssetIdToImportedAssetInfoLookup.Remove(id);
            onImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { removed = new[] { id } });
        }

        public ImportedAssetInfo GetImportedAssetInfo(string guid)
        {
            return m_GuidToImportedAssetInfoLookup.TryGetValue(guid, out var result) ? result : null;
        }

        public IAssetData GetAssetData(AssetIdentifier id)
        {
            if (id?.IsValid() != true)
                return null;

            if (m_AssetIdToImportedAssetInfoLookup.TryGetValue(id, out var info))
            {
                return info?.assetData;
            }

            return m_AssetData.TryGetValue(id, out var result) ? result : null;
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

        private void AddImportedAssetInfo(ImportedAssetInfo info)
        {
            foreach (var fileInfo in info.fileInfos)
            {
                m_GuidToImportedAssetInfoLookup[fileInfo.guid] = info;
            }

            m_AssetIdToImportedAssetInfoLookup[info.id] = info;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedImportedAssetInfos = m_GuidToImportedAssetInfoLookup.Values.ToArray();
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
                m_AssetData[data.identifier] = data;
            }
        }
    }
}