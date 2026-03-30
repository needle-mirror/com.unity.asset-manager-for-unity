using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Core.Editor
{
    class ImportEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class ImportEventData : IAnalytic.IData
#else
        internal class ImportEventData : BaseEventData
#endif
        {
            /// <summary>
            /// The location from which the event was triggered.
            /// </summary>
            public string Trigger;

            /// <summary>
            /// The ID of the asset being imported
            /// </summary>
            public string AssetId;

            /// <summary>
            /// The number of file contained in the asset being imported
            /// </summary>
            public int FileCount;

            /// <summary>
            /// The primary file extension of the asset being imported
            /// </summary>
            public string FileExtension;

            public string[] DatasetSystemTags;

            /// <summary>
            /// The organization ID of the project containing the asset
            /// </summary>
            public string OrganizationId;

            /// <summary>
            /// The project ID containing the asset being imported
            /// </summary>
            public string ProjectId;

            /// <summary>
            /// Collection paths where the asset is linked (if any)
            /// </summary>
            public string[] LinkedCollectionPaths;

            public ImportEventData(ImportTrigger trigger)
            {
                Utilities.DevAssert(trigger != null);

                Trigger = trigger?.ToString();
            }
        }

        internal const string k_EventName = AnalyticsSender.EventPrefix + "Import";
        internal const int k_EventVersion = 1;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        ImportEventData m_Data;

        internal ImportEvent(ImportTrigger trigger, string assetId, int fileCount = 0, string fileExtension = "",
            IEnumerable<string> datasetSystemTags = null, string organizationId = "", string projectId = "",
            IEnumerable<string> linkedCollectionPaths = null)
        {
            m_Data = new ImportEventData(trigger)
            {
                AssetId = assetId,
                FileCount = fileCount,
                FileExtension = fileExtension,
                DatasetSystemTags = datasetSystemTags?.ToArray(),
                OrganizationId = organizationId ?? string.Empty,
                ProjectId = projectId ?? string.Empty,
                LinkedCollectionPaths = linkedCollectionPaths?.ToArray() ?? Array.Empty<string>()
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class ImportEventAnalytic : IAnalytic
        {
            ImportEventData m_Data;

            public ImportEventAnalytic(ImportEventData data)
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
            return new ImportEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
