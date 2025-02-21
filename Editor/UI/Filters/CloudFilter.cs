using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    abstract class CloudFilter : BaseFilter
    {
        [SerializeReference]
        protected IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        protected IAssetsProvider m_AssetsProvider;

        List<string> m_CachedSelections = new();
        CancellationTokenSource m_TokenSource;

        protected abstract AssetSearchGroupBy GroupBy { get; }

        public abstract void ResetSelectedFilter(AssetSearchFilter assetSearchFilter);
        protected abstract void ClearFilter();
        protected abstract void IncludeFilter(List<string> selectedFilters);

        void ResetSelectedFilter()
        {
            ResetSelectedFilter(m_Page.PageFilters.AssetSearchFilter);
        }

        protected CloudFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(page)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_AssetsProvider = assetsProvider;
        }

        public override void Cancel()
        {
            if (m_TokenSource != null)
            {
                m_TokenSource.Cancel();
                m_TokenSource.Dispose();
                m_TokenSource = null;
                ResetSelectedFilter();
                IsDirty = true;
            }
        }

        public override void Clear()
        {
            ClearFilter();
        }

        public override bool ApplyFilter(List<string> selectedFilters)
        {
            if (selectedFilters == null)
            {
                ClearFilter();
            }
            else
            {
                IncludeFilter(selectedFilters);
            }

            return base.ApplyFilter(selectedFilters);
        }

        public override async Task<List<string>> GetSelections()
        {
            if (IsDirty)
            {
                m_CachedSelections = await GetSelectionsAsync();
                IsDirty = false;
            }

            return m_CachedSelections;
        }

        protected virtual async Task<List<string>> GetSelectionsAsync()
        {
            ClearFilter();

            m_TokenSource = new CancellationTokenSource();

            try
            {
                List<string> projects;
                if (m_Page is AllAssetsPage) // FixMe Each page should provide the list of projects or the filter selection but do not use a cast like this.
                {
                    projects = m_ProjectOrganizationProvider.SelectedOrganization.ProjectInfos.Select(p => p.Id)
                        .ToList();
                }
                else
                {
                    projects = new List<string> { m_ProjectOrganizationProvider.SelectedProject.Id };
                }

                var selections = await GetFilterSelectionsAsync(m_ProjectOrganizationProvider.SelectedOrganization.Id, projects, m_TokenSource.Token);
                return selections;
            }
            catch (OperationCanceledException)
            {
                // Do nothing
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
            finally
            {
                m_TokenSource?.Dispose();
                m_TokenSource = null;
                ResetSelectedFilter();
            }

            return null;
        }

        protected virtual async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, CancellationToken token)
        {
            return await m_AssetsProvider.GetFilterSelectionsAsync(organizationId, projectIds,
                m_Page.PageFilters.AssetSearchFilter, GroupBy, token);
        }
    }
}
