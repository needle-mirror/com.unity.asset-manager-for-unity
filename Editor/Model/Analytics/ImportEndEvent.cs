using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class ImportEndEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class FilterSearchEventData : IAnalytic.IData
#else
        internal class ImportEndEventData : BaseEventData
#endif
        {
            /// <summary>
            /// The ID of the asset being imported
            /// </summary>
            public string AssetId;

            /// <summary>
            /// The amount of milliseconds the import operation took
            /// </summary>
            public long ElapsedTime;

            /// <summary>
            /// The error message if any
            /// </summary>
            public string ErrorMessage;

            /// <summary>
            /// Which action triggered the import operation. See Enums/ImportAction.cs
            /// </summary>
            public ImportAction ImportAction;

            /// <summary>
            /// A string identifying the target of the import operation (e.g. project browser, inspector). See Enums/ImportEndImportTarget.cs
            /// </summary>
            public ImportEndImportTarget ImportTarget;

            /// <summary>
            /// Timestamp when the operation started
            /// </summary>
            public long StartTime;

            /// <summary>
            /// The ending status of the operation. See Enums/ImportEndStatus.cs
            /// </summary>
            public ImportEndStatus Status;

            /// <summary>
            /// The number of file contained in the asset being imported
            /// </summary>
            public int FileCount;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}ImportEnd";
        static int s_EventVersion = 3;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        ImportEndEventData m_Data;

        internal ImportEndEvent(ImportEndStatus status, string assetId, ImportAction importAction, DateTime startTime, DateTime finishTime, int fileCount = 0, string error = "", ImportEndImportTarget importTarget = ImportEndImportTarget.NotSet)
        {
            var elapsedTime = finishTime - startTime;
            m_Data = new ImportEndEventData()
            {
                AssetId = assetId,
                ImportAction = importAction,
                ElapsedTime = (long)elapsedTime.TotalMilliseconds, // we don't need fractional milliseconds... also, event is set to numerical, so it fails if we send fractional
                ErrorMessage = error,
                ImportTarget = importTarget,
                StartTime = Utilities.DatetimeToTimestamp(startTime),
                Status = status,
                FileCount = fileCount
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class ImportEndEventAnalytic : IAnalytic
        {
            ImportEndEventData m_Data;

            public ImportEndEventAnalytic(ImportEndEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data;
                return data != null;
            }
        }

        public IAnalytic GetAnalytic()
        {
            return new ImportEndEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
