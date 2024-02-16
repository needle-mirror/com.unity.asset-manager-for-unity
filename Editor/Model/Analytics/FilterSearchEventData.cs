using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [Serializable]
    internal class FilterSearchEventData : IAnalytic.IData
#else
    struct FilterSearchEventData
#endif
    {
#if !UNITY_2023_2_OR_NEWER
        public const string eventName = "assetManagerFilterSearch";
        public const int eventVersion = 1;
#endif

        /// <summary>
        /// The name of the filter that was used
        /// </summary>
        public string FilterName;

        /// <summary>
        /// The filter selected value
        /// </summary>
        public string FilterValue;
    }
}
