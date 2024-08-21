using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class TaskUtils
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

        public static async Task WaitForTasksWithHandleExceptions(IReadOnlyCollection<Task> tasks)
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

        public static async Task<IReadOnlyCollection<Task>> RunWithMaxConcurrentTasks<T>(IEnumerable<T> inputs,
            CancellationToken token, Func<T, Task> taskCreation, int maxConcurrentTasks)
        {
            var semaphore = new SemaphoreSlim(maxConcurrentTasks);

            var allTasks = new List<Task>();

            foreach (var input in inputs)
            {
                await semaphore.WaitAsync(token);

                var task = taskCreation.Invoke(input);

                var awaiter = task.GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    semaphore.Release();
                });

                allTasks.Add(task);
            }

            await Task.WhenAll(allTasks);

            return allTasks;
        }
    }
}
