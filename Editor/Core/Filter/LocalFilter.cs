using System;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    abstract class LocalFilter : BaseFilter
    {
        internal LocalFilter(IPage page) : base(page) { }

        public abstract Task<bool> Contains(IAssetData assetData);
    }
}
