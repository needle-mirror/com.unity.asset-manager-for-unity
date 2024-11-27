using System;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    abstract class LocalFilter : BaseFilter
    {
        internal LocalFilter(IPage page) : base(page) { }

        public abstract Task<bool> Contains(BaseAssetData assetData);
    }
}
