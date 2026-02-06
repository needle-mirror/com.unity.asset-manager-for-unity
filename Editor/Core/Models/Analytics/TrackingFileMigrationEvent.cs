using System;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Core.Editor
{
    class TrackingFileMigrationEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class TrackingFileMigrationEventData : IAnalytic.IData
#else
        internal class TrackingFileMigrationEventData : BaseEventData
#endif
        {
            /// <summary>
            /// Number of tracking files successfully migrated to the current format.
            /// </summary>
            public int SuccessCount;

            /// <summary>
            /// Number of tracking files that failed to migrate.
            /// </summary>
            public int FailureCount;

            /// <summary>
            /// Number of failures due to version upgrade chain (MigrateToLatestSingleFileFormat) returning null.
            /// </summary>
            public int VersionUpgradeFailedCount;

            /// <summary>
            /// Number of failures due to deserialization (ConvertToImportedAssetInfo null or invalid data).
            /// </summary>
            public int DeserializationFailedCount;

            /// <summary>
            /// Number of failures due to write or delete exception during migration.
            /// </summary>
            public int WriteFailedCount;

            /// <summary>
            /// Number of failures due to read or parse exception in the migration loop.
            /// </summary>
            public int ReadFailedCount;
        }

        internal const string k_EventName = AnalyticsSender.EventPrefix + "TrackingFileMigration";
        internal const int k_EventVersion = 1;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        TrackingFileMigrationEventData m_Data;

        internal TrackingFileMigrationEvent(int successCount, int failureCount, int versionUpgradeFailedCount, int deserializationFailedCount, int writeFailedCount, int readFailedCount)
        {
            m_Data = new TrackingFileMigrationEventData
            {
                SuccessCount = successCount,
                FailureCount = failureCount,
                VersionUpgradeFailedCount = versionUpgradeFailedCount,
                DeserializationFailedCount = deserializationFailedCount,
                WriteFailedCount = writeFailedCount,
                ReadFailedCount = readFailedCount
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class TrackingFileMigrationEventAnalytic : IAnalytic
        {
            TrackingFileMigrationEventData m_Data;

            public TrackingFileMigrationEventAnalytic(TrackingFileMigrationEventData data)
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
            return new TrackingFileMigrationEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
