using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class FileFoldoutItemViewModel
    {
        readonly BaseAssetData m_AssetData;
        readonly BaseAssetDataFile m_AssetDataFile;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        public string Filename => m_AssetDataFile.Path;
        public string Guid { get; }
        public bool Uploaded => m_AssetDataFile.Available;
        public bool CanRemove => m_AssetData.CanRemovedFile(m_AssetDataFile);

        public FileFoldoutItemViewModel(BaseAssetData assetData, BaseAssetDataFile assetDataFile, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetDataFile = assetDataFile;
            m_AssetData = assetData;

            var guid = assetDataFile.Guid;
            if (string.IsNullOrEmpty(guid))
            {
                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                guid = assetDataManager.GetImportedFileGuid(assetData?.Identifier, assetDataFile.Path);
            }

            Guid = guid;

            m_AssetDatabaseProxy = assetDatabaseProxy;
        }

        public void Remove()
        {
            m_AssetData.RemoveFile(m_AssetDataFile);
        }

        public bool CanPingAsset()
        {
            return m_AssetDatabaseProxy.CanPingAssetByGuid(Guid);
        }

        public void PingAsset()
        {
            m_AssetDatabaseProxy.PingAssetByGuid(Guid);
        }
    }
}
