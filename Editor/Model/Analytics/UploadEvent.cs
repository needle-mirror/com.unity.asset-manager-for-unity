using System;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor
{
    class UploadEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class UploadEventData : IAnalytic.IData
#else
        internal class UploadEventData : BaseEventData
#endif
        {
            /// <summary>
            /// The number of file selected to be uploaded
            /// </summary>
            public int FileCount;

            /// <summary>
            /// File extensions
            /// </summary>
            public string[] FileExtensions;

            /// <summary>
            /// If embeded dependency option is used
            /// </summary>
            public bool EmbedDependencies;

            /// <summary>
            /// If the upload is done into a collection
            /// </summary>
            public bool UseCollection;

            /// <summary>
            /// Upload mode.
            /// </summary>
            public string UploadMode;
        }

        const string k_EventName = AnalyticsSender.EventPrefix + "Upload";
        const int k_EventVersion = 1;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        UploadEventData m_Data;

        internal UploadEvent(int fileCount, string[] fileExtensions, bool embedDependency, bool useCollection,
            AssetUploadMode uploadMode)
        {
            m_Data = new UploadEventData
            {
                FileCount = fileCount,
                FileExtensions = fileExtensions,
                EmbedDependencies = embedDependency,
                UseCollection = useCollection,
                UploadMode = uploadMode.ToString()
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        class UploadEventAnalytic : IAnalytic
        {
            UploadEventData m_Data;

            public UploadEventAnalytic(UploadEventData data)
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
            return new UploadEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
