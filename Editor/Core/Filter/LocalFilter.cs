using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal abstract class LocalFilter : BaseFilter
    {
        internal LocalFilter(IPage page) : base(page)
        {
        }

        public abstract Task<bool> Contains(IAssetData assetData);
    }
}
