using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal abstract class BaseFilter
    {
        public abstract string DisplayName { get; }
        public abstract Task<List<string>> GetSelections();
        public abstract bool ApplyFilter(string selection);

        public bool IsDirty { get; set; } = true;
        public string SelectedFilter { get; protected set; }

        public virtual void Cancel() {}
        public virtual void Clear() {}
    }
}
