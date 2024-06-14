using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    public static class TaskUtils
    {
        public static void TrackException(Task task)
        {
            var awaiter = task.GetAwaiter();
            awaiter.OnCompleted(() =>
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }
            });

            _ = task;
        }

        public static async Task WaitForTasksWithHandleExceptions(IEnumerable<Task> tasks)
        {
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                foreach (var task in tasks)
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        Debug.LogException(task.Exception);
                    }
                }
            }
        }
    }
}
