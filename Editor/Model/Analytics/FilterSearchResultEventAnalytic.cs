using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [AnalyticInfo(eventName:"assetExplorerFilterSearchResult", vendorKey:"unity.asset-explorer", version:1, maxEventsPerHour:1000,maxNumberOfElements:1000)]
    internal class FilterSearchResultEventAnalytic : IAnalytic
    {
        string filterName;
        string filterValue;

        public FilterSearchEventResultAnalytic(string filterName, string filterValue, int resultCount)
        {
            this.filterName = filterName;
            this.filterValue = filterValue;
            this.resultCount = resultCount;
        }

        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            var elapsedTime = finishTime - startTime;
            var parameters = new FilterSearchEventData()
            {
                FilterName = filterName,
                FilterValue = filterValue,
                ResultCount = resultCount
            };
            data = parameters;
            return data != null;
        }
    }
#endif
}
