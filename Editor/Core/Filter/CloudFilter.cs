using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal abstract class CloudFilter : BaseFilter
    {
        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public abstract void ResetSelectedFilter(AssetSearchFilter assetSearchFilter);
        protected abstract string Criterion { get; }
        protected abstract void ClearFilter();
        protected abstract void IncludeFilter(string selection);

        List<string> m_CachedSelections = new();

        CancellationTokenSource m_TokenSource;

        void ResetSelectedFilter()
        {
            ResetSelectedFilter(m_Page.pageFilters.assetFilter);
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

        async Task<List<string>> GetSelectionsAsync()
        {
            ClearFilter();

            m_TokenSource = new CancellationTokenSource();

            try
            {
                List<string> projects;
                if (m_ProjectOrganizationProvider.SelectedProject == ProjectInfo.AllAssetsProjectInfo)
                {
                    projects = m_ProjectOrganizationProvider.SelectedOrganization.projectInfos.Select(p => p.id).ToList();
                }
                else
                {
                    projects = new List<string> { m_ProjectOrganizationProvider.SelectedProject.id };
                }

                var selections = await m_Page.GetFilterSelectionsAsync(m_ProjectOrganizationProvider.SelectedOrganization.id, projects, Criterion, m_TokenSource.Token);
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
