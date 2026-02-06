using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class FileFoldoutViewModel
    {
        readonly BaseAssetData m_AssetData;
        readonly AssetDataset m_AssetDataset;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        public string Name => m_AssetDataset.Name;
        public bool IsSourceControlled => m_AssetDataset.IsSourceControlled;

        public List<BaseAssetDataFile> Files
        {
            get;
        }

        public FileFoldoutViewModel(BaseAssetData assetData, AssetDataset assetDataset, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_AssetData = assetData;
            m_AssetDataset = assetDataset;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            Files = assetDataset.Files.OrderBy(f => f.Path).ToList();
        }

        public FileFoldoutItemViewModel GetFileFoldoutItemViewModel(int index)
        {
            return new FileFoldoutItemViewModel(m_AssetData, Files[index], m_AssetDatabaseProxy);
        }
    }
}
