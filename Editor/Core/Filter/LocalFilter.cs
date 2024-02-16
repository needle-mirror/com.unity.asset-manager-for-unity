using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal abstract class LocalFilter : BaseFilter
    {
        IPageManager m_PageManager;

        internal LocalFilter(IPageManager pageManager)
        {
            m_PageManager = pageManager;
        }
        
        public abstract Task<bool> Contains(IAssetData assetData);

        public override bool ApplyFilter(string selection)
        {
            bool reload = selection != SelectedFilter;

            if (reload)
            {
                if (SelectedFilter == null)
                {
                    m_PageManager.activePage.AddLocalFilter(this);
                }
                else if (selection == null)
                {
                    m_PageManager.activePage.RemoveLocalFilter(this);
                }
            }

            SelectedFilter = selection;

            return reload;
        }
    }
}
