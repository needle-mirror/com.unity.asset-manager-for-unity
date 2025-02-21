using System;
using System.Collections.Generic;
using System.Data;
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

        public override async Task<bool> Contains(BaseAssetData assetData, CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (SelectedFilters == null || !SelectedFilters.Any())
            {
                return true;
            }

            var status = assetData.AssetDataAttributeCollection?.GetAttribute<ImportAttribute>()?.Status;
            return status.HasValue && SelectedFilters.Any(selectedFilter => status == Map(selectedFilter));
        }

        static ImportAttribute.ImportStatus Map(string importStatus)
        {
            return importStatus switch
            {
                Constants.UpToDate => ImportAttribute.ImportStatus.UpToDate,
                Constants.Outdated => ImportAttribute.ImportStatus.OutOfDate,
                Constants.Deleted => ImportAttribute.ImportStatus.ErrorSync,
                _ => ImportAttribute.ImportStatus.NoImport
            };
        }
    }
}
