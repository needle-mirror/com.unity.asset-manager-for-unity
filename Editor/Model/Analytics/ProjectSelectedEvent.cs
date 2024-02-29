using System;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    class ProjectSelectedEvent : IBaseEvent
    {
        public enum ProjectType
        {
            AllAssets,
            Project,
            Collection,
            InProject
        }

        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class ProjectSelectedEventData : IAnalytic.IData
#else
        internal class ProjectSelectedEventData : BaseEventData
#endif
        {
            public ProjectType SelectProjectType;
            public int NumberOfCollections;
            public int ProjectCount;
            public int AssetCount;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}ProjectSelected";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        ProjectSelectedEventData m_Data;

        internal ProjectSelectedEvent(ProjectType selectProjectType, int count = 0)
        {
            m_Data = new ProjectSelectedEventData()
            {
                SelectProjectType = selectProjectType,
                NumberOfCollections = selectProjectType == ProjectType.Collection ? count : 0,
                ProjectCount = selectProjectType == ProjectType.AllAssets ? count : 0,
                AssetCount = selectProjectType == ProjectType.InProject ? count : 0
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class ProjectSelectedEventAnalytic : IAnalytic
        {
            ProjectSelectedEventData m_Data;

            public ProjectSelectedEventAnalytic(ProjectSelectedEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data
                return data != null;
            }
        }

        public IAnalytic GetAnalytic()
        {
            return new ProjectSelectedEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
