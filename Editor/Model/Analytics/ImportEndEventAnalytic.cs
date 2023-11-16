using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor.Model.Analytics
{
#if UNITY_2023_2_OR_NEWER
    [AnalyticInfo(eventName:"assetExplorerImportEnd", vendorKey:"unity.asset-explorer", version:2, maxEventsPerHour:1000,maxNumberOfElements:1000)]
    internal class ImportEndEventAnalytic : IAnalytic
    {

        ImportEndStatus status;
        string assetId;
        string importAction;
        DateTime startTime;
        DateTime finishTime;
        string error;
        ImportEndImportTarget importTarget;
        
        public ImportEndEventAnalytic(ImportEndStatus status, string assetId, string importAction, DateTime startTime, DateTime finishTime, string error = "", ImportEndImportTarget importTarget = ImportEndImportTarget.NotSet)
        {
            this.status = status;
            this.assetId = assetId;
            this.importAction = importAction;
            this.startTime = startTime;
            this.finishTime = finishTime;
            this.error = error;
            this.importTarget = importTarget;
        }
        
        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            var elapsedTime = finishTime - startTime;
            var parameters = new ImportEndEventData()
            {
                assetId = assetId,
                importAction = importAction,
                elapsedTime = (long)elapsedTime.TotalMilliseconds,
                errorMessage = error?.Message,
                importTarget = importTarget.ToString(),
                startTime = Utilities.DatetimeToTimestamp(startTime),
                status = status.ToString()
            };
            data = parameters;
            return data != null;
        }
    }
#endif
}
