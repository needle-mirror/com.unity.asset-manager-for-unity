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

        List<string> m_CachedSelections = new();
        CancellationTokenSource m_TokenSource;

        protected abstract AssetSearchGroupBy GroupBy { get; }

        public abstract void ResetSelectedFilter(AssetSearchFilter assetSearchFilter);
        protected abstract void ClearFilter();
        protected abstract void IncludeFilter(string selection);

        void ResetSelectedFilter()
        {
            ResetSelectedFilter(m_Page.PageFilters.AssetSearchFilter);
        }

        internal CloudFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
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

        public override bool ApplyFilter(string selection)
        {
            if (string.IsNullOrEmpty(selection))
            {
                ClearFilter();
            }
            else
            {
                IncludeFilter(selection);
            }

            return base.ApplyFilter(selection);
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

                var selections = await m_Page.GetFilterSelectionsAsync(
                    m_ProjectOrganizationProvider.SelectedOrganization.Id, projects, GroupBy, m_TokenSource.Token);
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
    }
}
