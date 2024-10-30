using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.Cloud.CommonEmbedded.Runtime
{
    static class UnitySynchronizationContextGrabber
    {
        static SynchronizationContext s_UnitySynchronizationContextValue;
        public static SynchronizationContext s_UnitySynchronizationContext
        {
            get => s_UnitySynchronizationContextValue;
        }

        static TaskScheduler s_UnityMainThreadSchedulerValue;
        internal static TaskScheduler s_UnityMainThreadScheduler
        {
            get => s_UnityMainThreadSchedulerValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RuntimeGrab()
        {
            s_UnitySynchronizationContextValue = SynchronizationContext.Current;
            s_UnityMainThreadSchedulerValue = TaskScheduler.FromCurrentSynchronizationContext();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void EditorGrab()
        {
            RuntimeGrab();
        }
#endif

    }
}
