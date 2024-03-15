using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal class LocalStatusFilter : LocalFilter
    {
        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public override string DisplayName => "Status";

        List<string> m_CachedSelections;

        public LocalStatusFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        void OnOrganizationChanged(OrganizationInfo _, bool __)
        {
            m_CachedSelections = null;
        }

        public override async Task<List<string>> GetSelections()
        {
            if (m_CachedSelections == null)
            {
                List<string> projects = m_ProjectOrganizationProvider.SelectedOrganization.projectInfos.Select(p => p.id).ToList();

                m_CachedSelections = await m_Page.GetFilterSelectionsAsync(m_ProjectOrganizationProvider.SelectedOrganization.id, projects, GroupableField.Status, CancellationToken.None);
            }

            return m_CachedSelections;
        }

        public override Task<bool> Contains(IAssetData assetData)
        {
            return SelectedFilter == null ? Task.FromResult(true) : Task.FromResult(SelectedFilter == assetData.status);
        }
    }
}
