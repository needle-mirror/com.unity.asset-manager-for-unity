using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class LocalStatusFilter : LocalFilter
    {
        IAssetDataManager m_AssetDataManager;

        public override string DisplayName => "Status";

        List<string> m_CachedSelections;

        public LocalStatusFilter(IPage page, IAssetDataManager assetDataManager)
            : base(page)
        {
            m_AssetDataManager = assetDataManager;
        }

        public override Task<List<string>> GetSelections()
        {
            if (m_CachedSelections == null)
            {
                m_CachedSelections = m_AssetDataManager.ImportedAssetInfos.Select(i => i.AssetData.Status).Distinct().ToList();
            }

            return Task.FromResult(m_CachedSelections);
        }

        public override Task<bool> Contains(BaseAssetData assetData)
        {
            return SelectedFilter == null ? Task.FromResult(true) : Task.FromResult(SelectedFilter == assetData.Status);
        }
    }
}
