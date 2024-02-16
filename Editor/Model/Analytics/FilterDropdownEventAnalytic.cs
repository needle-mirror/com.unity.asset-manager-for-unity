using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [AnalyticInfo(eventName:"assetExplorerFilterDown", vendorKey:"unity.asset-explorer", version:1, maxEventsPerHour:1000,maxNumberOfElements:1000)]
    internal class FilterDropdownEventAnalytic : IAnalytic
    {
        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            var parameters = new FilterDropdownEventData();
            data = parameters;
            return data != null;
        }
    }
#endif
}
