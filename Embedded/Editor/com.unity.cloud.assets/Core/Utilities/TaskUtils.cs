using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    static class TaskUtils
    {
        internal static async Task TryFinallyAsync(Func<Task> tryFunc, Action finallyAction)
        {
            try
            {
                await tryFunc();
            }
            finally
            {
                finallyAction();
            }
        }

        internal static Task Run(Action action, CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            cancellationToken.ThrowIfCancellationRequested();
            return AwaitTask(() =>
            {
                action();
                return Task.CompletedTask;
            });
#else
            return Task.Run(action, cancellationToken);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        async static Task AwaitTask(Func<Task> function)
        {
            try
            {
                await function();
            }
            catch (Exception e)
            {
                throw new AggregateException(e);
            }
        }
#endif
    }
}
