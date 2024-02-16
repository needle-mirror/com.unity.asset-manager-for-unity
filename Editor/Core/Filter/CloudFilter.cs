using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal abstract class CloudFilter : BaseFilter
    {
        protected abstract string Criterion { get; }
        protected abstract void ClearFilter();
        protected abstract void ResetSelectedFilter();
        protected abstract void IncludeFilter(string selection);

        protected List<string> m_CachedSelections = new();

        protected readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        protected readonly IAssetsProvider m_AssetsProvider;

        CancellationTokenSource m_TokenSource;

        internal CloudFilter(IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
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

        public override bool ApplyFilter(string selection)
        {
            bool reload = selection != SelectedFilter;

            SelectedFilter = selection;

            if (string.IsNullOrEmpty(selection))
            {
                ClearFilter();
            }
            else
            {
                IncludeFilter(selection);
            }

            return reload;
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
                if (m_ProjectOrganizationProvider.selectedProject == ProjectInfo.AllAssetsProjectInfo)
                {
                    projects = m_ProjectOrganizationProvider.organization.projectInfos.Select(p => p.id).ToList();
                }
                else
                {
                    projects = new List<string> { m_ProjectOrganizationProvider.selectedProject.id };
                }

                var selections = await m_AssetsProvider.GetFilterSelectionsAsync(m_ProjectOrganizationProvider.organization.id, projects, Criterion, m_TokenSource.Token);
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
