using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.UI.Editor
{
    enum FilterSelectionType
    {
        None,
        MultiSelection,
        Number,
        SingleSelection,
        Timestamp,
        Url,
        Text,
        NumberRange
    }

    [Serializable]
    abstract class BaseFilter
    {
        [FormerlySerializedAs("m_SelectedFilter")]
        [SerializeField]
        protected List<string> m_SelectedFilters;

        [SerializeReference]
        protected IPage m_Page;

        public abstract string DisplayName { get; }
        public abstract Task<List<string>> GetSelections();
        public virtual FilterSelectionType SelectionType => FilterSelectionType.MultiSelection;

        public bool IsDirty { get; set; } = true;
        public List<string> SelectedFilters => m_SelectedFilters;

        public virtual void Cancel() { }
        public virtual void Clear() { }

        protected BaseFilter(IPage page)
        {
            m_Page = page;
        }

        public virtual bool ApplyFilter(List<string> selectedFilters)
        {
            bool reload = false;

            if(selectedFilters == null && m_SelectedFilters != null)
            {
                m_Page.PageFilters.RemoveFilter(this);
                reload = true;
            }
            else if (selectedFilters != null)
            {
                if(m_SelectedFilters != null)
                {
                    reload = !(selectedFilters.Count == m_SelectedFilters.Count && selectedFilters.All(m_SelectedFilters.Contains));
                }
                else
                {
                    reload = true;
                }
            }

            m_SelectedFilters = selectedFilters;

            return reload;
        }

        public virtual string DisplaySelectedFilters()
        {
            if(SelectedFilters == null || !SelectedFilters.Any())
            {
                return DisplayName;
            }

            if(SelectedFilters.Count == 1)
            {
                return $"{DisplayName} : {SelectedFilters[0]}";
            }

            return $"{DisplayName} : {SelectedFilters[0]} +{SelectedFilters.Count - 1}";
        }
    }
}
