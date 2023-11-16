using System;
using Unity.AssetManager.Editor.Model.Analytics;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor
{
    internal interface IAnalyticsEngine : IService
    {
        void SendImportEndEvent(ImportEndStatus status, string assetId, string importAction, DateTime startTime, DateTime finishTime, string error = "",
            ImportEndImportTarget importTarget = ImportEndImportTarget.NotSet);
    }

    class AnalyticsEngine : BaseService<IAnalyticsEngine>, IAnalyticsEngine
    {
        private const int k_MaxEventsPerHour = 1000;
        private const int k_MaxNumberOfElements = 1000;

        // Vendor key must start with unity.
        const string k_VendorKey = "unity.asset-explorer";

        private readonly IEditorAnalyticsWrapper m_EditorAnalytics;
        public AnalyticsEngine(IEditorAnalyticsWrapper analyticsWrapper)
        {
            m_EditorAnalytics = RegisterDependency(analyticsWrapper);
        }

        public void SendImportEndEvent(ImportEndStatus status, string assetId, string importAction, DateTime startTime, DateTime finishTime, string error = "", ImportEndImportTarget importTarget = ImportEndImportTarget.NotSet)
        {
#if !UNITY_2023_2_OR_NEWER
            var register = m_EditorAnalytics.RegisterEventWithLimit(ImportEndEventData.eventName, k_MaxEventsPerHour,
                k_MaxNumberOfElements,
                k_VendorKey, ImportEndEventData.eventVersion);

            // This is one here for each event, so that if the call fails we don't try to send the event for nothing
            if (register != AnalyticsResult.Ok)
                return;

            var elapsedTime = finishTime - startTime;
            var sent = m_EditorAnalytics.SendEventWithLimit(ImportEndEventData.eventName, new ImportEndEventData()
            {
                assetId = assetId,
                importAction = importAction,
                elapsedTime = (long)elapsedTime.TotalMilliseconds, // we don't need fractional milliseconds... also, event is set to numerical, so it fails if we send fractional
                errorMessage = error,
                importTarget = importTarget.ToString(),
                startTime = Utilities.DatetimeToTimestamp(startTime),
                status = status.ToString()
            }, ImportEndEventData.eventVersion);
#else
            ImportEndEventAnalytic analytic = new ImportEndEventAnalytic(status, assetId, importAction, startTime, finishTime, error, importTarget);
            var analyticsResult = m_EditorAnalytics.SendAnalytic(analytic);
#endif
        }
    }
}