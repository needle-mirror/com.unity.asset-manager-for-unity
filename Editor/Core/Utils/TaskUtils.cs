using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    static class TaskUtils
    {
        public static void TrackException(Task task, Action<Exception> exceptionCallback = null)
        {
            var awaiter = task.GetAwaiter();
            awaiter.OnCompleted(() =>
            {
                if (task.Exception != null)
                {
                    if (exceptionCallback != null)
                    {
                        exceptionCallback.Invoke(task.Exception);
                    }
                    else
                    {
                        Debug.LogException(task.Exception);
                    }
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

        public static async Task<IReadOnlyCollection<Task>> RunWithMaxConcurrentTasksAsync<T>(IEnumerable<T> inputs,
            CancellationToken token, Func<T, Task> taskCreation, int maxConcurrentTasks)
        {
            var semaphore = new SemaphoreSlim(maxConcurrentTasks);
            return await RunWithMaxConcurrentTasksAsync(inputs, token, taskCreation, semaphore);
        }

        public static async Task<IReadOnlyCollection<Task>> RunWithMaxConcurrentTasksAsync<T>(IEnumerable<T> inputs,
            CancellationToken token, Func<T, Task> taskCreation, SemaphoreSlim semaphore)
        {
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

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnumerable, CancellationToken token)
        {
            var list = new List<T>();
            await foreach (var item in asyncEnumerable.WithCancellation(token))
            {
                list.Add(item);
            }

            return list;
        }
    }
}
