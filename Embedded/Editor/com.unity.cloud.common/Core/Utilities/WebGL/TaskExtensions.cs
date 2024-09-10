using System.Threading.Tasks;

namespace Unity.Cloud.CommonEmbedded
{
    internal static class TaskExtensions
    {
        internal static async Task<T> UnityConfigureAwait<T>(this Task<T> task, bool continueOnCapturedContext)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await task;
#else
            return await task.ConfigureAwait(continueOnCapturedContext);
#endif
        }

        internal static async Task UnityConfigureAwait(this Task task, bool continueOnCapturedContext)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            await task;
#else
            await task.ConfigureAwait(continueOnCapturedContext);
#endif
        }
    }
}
