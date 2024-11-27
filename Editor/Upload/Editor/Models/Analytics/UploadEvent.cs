using System;
using Unity.AssetManager.Core.Editor;

#if UNITY_2023_2_OR_NEWER
using UnityEngine.Analytics;
#endif

namespace Unity.AssetManager.Upload.Editor
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
            /// Upload mode.
            /// </summary>
            public string UploadMode;

            /// <summary>
            /// Dependency mode.
            /// </summary>
            public string DependencyMode;

            /// <summary>
            /// File Paths mode.
            /// </summary>
            public string FilePathMode;

            /// <summary>
            /// If the upload is done into a collection
            /// </summary>
            public bool UseCollection;
        }

        internal const string k_EventName = AnalyticsSender.EventPrefix + "Upload";
        internal const int k_EventVersion = 2;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        readonly UploadEventData m_Data;

        internal UploadEvent(int fileCount, string[] fileExtensions, bool useCollection, UploadStaging uploadStaging)
        {
            m_Data = new UploadEventData
            {
                FileCount = fileCount,
                FileExtensions = fileExtensions,
                UploadMode = uploadStaging.UploadMode.ToString(),
                DependencyMode = uploadStaging.DependencyMode.ToString(),
                FilePathMode = uploadStaging.FilePathMode.ToString(),
                UseCollection = useCollection
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        class UploadEventAnalytic : IAnalytic
        {
            readonly UploadEventData m_Data;

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
