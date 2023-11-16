using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IStateManager : IService
    {
        string lastSceneName { get; set; }
        float sideBarScrollValue { get; set; }
        bool detailsFileFoldoutValue { get; set; }
        float sideBarWidth { get; set; }
    }

    [Serializable]
    internal class StateManager : BaseService<IStateManager>, IStateManager
    {
        [SerializeField]
        private string m_LastSceneName;
        public string lastSceneName
        {
            get => m_LastSceneName;
            set => m_LastSceneName = value;
        }

        [SerializeField]
        private float m_SideBarScrollValue;
        public float sideBarScrollValue
        {
            get => m_SideBarScrollValue;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                m_SideBarScrollValue = value;
            }
        }

        [SerializeField]
        private bool m_DetailsFileFoldoutValue;
        public bool detailsFileFoldoutValue
        {
            get => m_DetailsFileFoldoutValue;
            set => m_DetailsFileFoldoutValue = value;
        }
        
        [SerializeField] 
        private float m_SideBarWidth = 160;

        public float sideBarWidth
        {
            get => m_SideBarWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                m_SideBarWidth = value;
            }
        }

    }
}

