using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class UserMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        UserMetadata m_UserMetadata;

        public override FilterSelectionType SelectionType => FilterSelectionType.SingleSelection;

        public UserMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_UserMetadata = metadata as UserMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            if (selectedFilters == null)
            {
                m_UserMetadata.Value = null;
                base.IncludeFilter(null);
                return;
            }

            TaskUtils.TrackException(IncludeFilterAsync(selectedFilters));
        }

        protected override async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, CancellationToken token)
        {
            var selections = await base.GetFilterSelectionsAsync(organizationId, projectIds, token);
            var selectionNames = await m_ProjectOrganizationProvider.SelectedOrganization.GetUserNamesAsync(selections);
            selectionNames.Sort();

            return selectionNames;
        }

        async Task IncludeFilterAsync(List<string> selectedFilters)
        {
            // Could be done with only the first selected filter, but we keep that implementation for future use,
            // when the backend will support multiple selected filters for custom metadata.
            var userIds = new List<string>();
            foreach (var selectedFilter in selectedFilters)
            {
                userIds.Add(await m_ProjectOrganizationProvider.SelectedOrganization.GetUserIdAsync(selectedFilter));
            }

            m_UserMetadata.Value = userIds[0];
            base.IncludeFilter(selectedFilters);
        }
    }
}
