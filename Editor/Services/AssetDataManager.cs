using System;
using System.Collections.Generic;
using System.Linq;
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
        void AddGuidsToImportedAssetInfo(AssetIdentifier id, IReadOnlyCollection<ImportedFileInfo> fileInfos);
        void RemoveGuidsFromImportedAssetInfos(IReadOnlyCollection<string> guidsToRemove);

        void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas);
        void UpdateAssetDataFileInfos(IAssetData assetData);

        ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id);
        ImportedAssetInfo GetImportedAssetInfo(string guid);
        IAssetData GetAssetData(AssetIdentifier id);

        bool IsInProject(AssetIdentifier id);
        void UpdateFilesStatus(IAssetData assetData, AssetDataFilesStatus status, bool triggerEvent = true);
    }

    [Serializable]
    internal class AssetDataManager : BaseService<IAssetDataManager>, IAssetDataManager, ISerializationCallbackReceiver
    {
        private readonly Dictionary<string, ImportedAssetInfo> m_GuidToImportedAssetInfoLookup = new();
        private readonly Dictionary<AssetIdentifier, ImportedAssetInfo> m_AssetIdToImportedAssetInfoLookup = new();
        private readonly Dictionary<AssetIdentifier, IAssetData> m_AssetData = new();
        [SerializeField]
        private ImportedAssetInfo[] m_SerializedImportedAssetInfos = Array.Empty<ImportedAssetInfo>();
        [SerializeField]
        private AssetData[] m_SerializedAssetData = Array.Empty<AssetData>();

        public event Action<AssetChangeArgs> onImportedAssetInfoChanged = delegate {};
        public event Action<AssetChangeArgs> onAssetDataChanged = delegate {};
        public IReadOnlyCollection<ImportedAssetInfo> importedAssetInfos => m_AssetIdToImportedAssetInfoLookup.Values;

        private AssetData.AssetDataFactory m_AssetDataFactory;
        private readonly IUnityConnectProxy m_UnityConnect;
        public AssetDataManager(IUnityConnectProxy unityConnect)
        {
            m_UnityConnect = RegisterDependency(unityConnect);
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
                m_AssetData.Clear();
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

        public void AddGuidsToImportedAssetInfo(AssetIdentifier id, IReadOnlyCollection<ImportedFileInfo> fileInfos)
        {
            fileInfos ??= Array.Empty<ImportedFileInfo>();
            if (fileInfos.Count <= 0)
                return;

            var added = new HashSet<AssetIdentifier>();
            var updated = new HashSet<AssetIdentifier>();
            var info = GetImportedAssetInfo(id);
            if (info == null)
            {
                info = new ImportedAssetInfo { id = id, fileInfos = fileInfos.ToList() };
                AddImportedAssetInfo(info);
                added.Add(id);
            }
            else
            {
                foreach (var fileInfo in fileInfos)
                {
                    if (info.fileInfos.Any(i => i.guid == fileInfo.guid))
                        continue;
                    info.fileInfos.Add(fileInfo);
                    m_GuidToImportedAssetInfoLookup[fileInfo.guid] = info;
                    updated.Add(id);
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
                onImportedAssetInfoChanged?.Invoke(new AssetChangeArgs { added = Array.Empty<AssetIdentifier>(), removed =  removed, updated = updated });
        }

        public void AddOrUpdateAssetDataFromCloudAsset(IEnumerable<IAssetData> assetDatas)
        {
            var assetChangeArgs = new AssetChangeArgs();
            var updated = new HashSet<AssetIdentifier>();
            var added = new HashSet<AssetIdentifier>();

            foreach (var assetData in assetDatas)
            { 
                
                if(m_AssetData.ContainsKey(assetData.id))
                {
                    if (!AssetData.AssetDataFactory.IsDifferent(assetData as AssetData, m_AssetData[assetData.id] as AssetData))
                        continue;
                    updated.Add(assetData.id);
                }
                else
                    added.Add(assetData.id);
                m_AssetData[assetData.id] = assetData;
            }

            assetChangeArgs.added = added;
            assetChangeArgs.updated = updated;
            onAssetDataChanged?.Invoke(assetChangeArgs);
        }

        public void UpdateAssetDataFileInfos(IAssetData assetData)
        {
            if (!m_AssetData.ContainsKey(assetData.id)) 
                return;
            
            var assetChangeArgs = new AssetChangeArgs();
            var updated = new HashSet<AssetIdentifier>();
            var updatedAssetData = m_AssetDataFactory.UpdateAssetDataFilesInfo(m_AssetData[assetData.id] as AssetData, assetData as AssetData);
            if (updatedAssetData == null) 
                return;
            updated.Add(assetData.id);
            m_AssetData[assetData.id] = updatedAssetData;
            assetChangeArgs.updated = updated;
            onAssetDataChanged?.Invoke(assetChangeArgs);
        }

        public ImportedAssetInfo GetImportedAssetInfo(AssetIdentifier id)
        {
            return id?.IsValid() == true && m_AssetIdToImportedAssetInfoLookup.TryGetValue(id, out var result) ? result : null;
        }

        public ImportedAssetInfo GetImportedAssetInfo(string guid)
        {
            return m_GuidToImportedAssetInfoLookup.TryGetValue(guid, out var result) ? result : null;
        }

        public IAssetData GetAssetData(AssetIdentifier id)
        {
            return id?.IsValid() == true && m_AssetData.TryGetValue(id, out var result) ? result : null;
        }

        public bool IsInProject(AssetIdentifier id)
        {
            return GetImportedAssetInfo(id) != null;
        }

        public void UpdateFilesStatus(IAssetData assetData, AssetDataFilesStatus status, bool triggerEvent = true)
        {
            if (assetData == null || assetData.filesInfosStatus == status) 
                return;
            
            m_AssetDataFactory ??= new AssetData.AssetDataFactory();
            m_AssetDataFactory.UpdateFilesStatus(assetData as AssetData, status);
            if (!triggerEvent) 
                return;
            var assetChangeArgs = new AssetChangeArgs {updated = new []{assetData.id}};
            onAssetDataChanged?.Invoke(assetChangeArgs);
        }

        private void AddImportedAssetInfo(ImportedAssetInfo info)
        {
            foreach (var fileInfo in info.fileInfos)
                m_GuidToImportedAssetInfoLookup[fileInfo.guid] = info;
            m_AssetIdToImportedAssetInfoLookup[info.id] = info;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedImportedAssetInfos = m_GuidToImportedAssetInfoLookup.Values.ToArray();
            m_SerializedAssetData = m_AssetData.Values.OfType<AssetData>().ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var info in m_SerializedImportedAssetInfos)
                AddImportedAssetInfo(info);
            foreach (var data in m_SerializedAssetData)
                m_AssetData[data.id] = data;
        }
    }
}