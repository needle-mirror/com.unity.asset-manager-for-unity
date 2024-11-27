using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    abstract class BaseFilter
    {
        [SerializeField]
        protected string m_SelectedFilter;

        [SerializeReference]
        protected IPage m_Page;

        public abstract string DisplayName { get; }
        public abstract Task<List<string>> GetSelections();

        public bool IsDirty { get; set; } = true;
        public string SelectedFilter => m_SelectedFilter;

        public virtual void Cancel() { }
        public virtual void Clear() { }

        protected BaseFilter(IPage page)
        {
            m_Page = page;
        }

        public virtual bool ApplyFilter(string selection)
        {
            var reload = selection != SelectedFilter;

            if (reload && selection == null)
            {
                m_Page.PageFilters.RemoveFilter(this);
            }

            m_SelectedFilter = selection;

            return reload;
        }
    }
}
