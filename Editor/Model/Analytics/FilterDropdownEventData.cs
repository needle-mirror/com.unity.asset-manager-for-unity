using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [Serializable]
    internal class FilterDropdownEventData : IAnalytic.IData
#else
    struct FilterDropdownEventData
#endif
    {
#if !UNITY_2023_2_OR_NEWER
        public const string eventName = "assetManagerFilterDropdown";
        public const int eventVersion = 1;
#endif
    }
}
