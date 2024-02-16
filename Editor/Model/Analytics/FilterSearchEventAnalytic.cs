using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [AnalyticInfo(eventName:"assetExplorerFilterSearch", vendorKey:"unity.asset-explorer", version:1, maxEventsPerHour:1000,maxNumberOfElements:1000)]
    internal class FilterSearchEventAnalytic : IAnalytic
    {
        string filterName;
        string filterValue;

        public FilterSearchEventAnalytic(string filterName, string filterValue)
        {
            this.filterName = filterName;
            this.filterValue = filterValue;
        }

        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            var elapsedTime = finishTime - startTime;
            var parameters = new FilterSearchEventData()
            {
                FilterName = filterName,
                FilterValue = filterValue
            };
            data = parameters;
            return data != null;
        }
    }
#endif
}
