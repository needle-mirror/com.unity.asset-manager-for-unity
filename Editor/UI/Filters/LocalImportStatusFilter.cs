using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class LocalImportStatusFilter : LocalFilter
    {
        public override string DisplayName => "Import Status";

        readonly List<string> m_Selections = new () { Constants.UpToDate, Constants.Outdated, Constants.Deleted};

        public LocalImportStatusFilter(IPage page) : base(page) { }

        public override Task<List<string>> GetSelections()
        {
            return Task.FromResult(m_Selections);
        }

        public override async Task<bool> Contains(BaseAssetData assetData)
        {
            if (SelectedFilter == null)
            {
                return true;
            }

            await assetData.GetPreviewStatusAsync();
            var comparisonResult = assetData.PreviewStatus.FirstOrDefault();

            return comparisonResult == Map(SelectedFilter);
        }

        AssetDataStatusType Map(string importStatus)
        {
            switch (importStatus)
            {
                case Constants.UpToDate:
                    return AssetDataStatusType.UpToDate;
                case Constants.Outdated:
                    return AssetDataStatusType.OutOfDate;
                case Constants.Deleted:
                    return AssetDataStatusType.Error;
                default:
                    return AssetDataStatusType.None;
            }
        }
    }
}
