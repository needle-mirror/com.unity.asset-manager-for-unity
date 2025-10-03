using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    static class TaskUtils
    {
        //number based on half the back-end rate limit and to keep it the UI reactive when running batches of tasks
        const int k_MaxConcurrentTasks = 40;

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

        public static async Task WaitForTaskWithHandleExceptions(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    Debug.LogException(task.Exception);
                }
            }
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

        public static async Task<IReadOnlyCollection<Task>> RunAllTasks<T>(IEnumerable<T> inputs, Func<T, Task> taskCreation)
        {
            var allTasks = inputs.Select(taskCreation.Invoke).ToList();
            await Task.WhenAll(allTasks);
            return allTasks;
        }

        public static async Task<IReadOnlyCollection<Task>> RunAllTasksWithHandledExceptions<T>(IEnumerable<T> inputs, Func<T, Task> taskCreation)
        {
            var allTasks = inputs.Select(taskCreation.Invoke).ToList();
            await WaitForTasksWithHandleExceptions(allTasks);
            return allTasks;
        }

        public static async Task<IReadOnlyCollection<Task>> RunAllTasksBatched<T>(IEnumerable<T> inputs, Func<T, Task> taskCreation, int maxConcurrentTasks = k_MaxConcurrentTasks)
        {
            var allTasks = new List<Task>();
            var batch = new List<Task>(maxConcurrentTasks);
            foreach (var input in inputs)
            {
                var task = taskCreation(input);
                allTasks.Add(task);
                batch.Add(task);
                if (batch.Count < maxConcurrentTasks) continue;
                await Task.WhenAll(batch);
                batch.Clear();
            }
            if (batch.Count > 0)
            {
                await Task.WhenAll(batch);
            }
            return allTasks;
        }

        public static async Task<IReadOnlyCollection<Task>> RunAllTasksBatchedWithHandledExceptions<T>(IEnumerable<T> inputs, Func<T, Task> taskCreation, int maxConcurrentTasks = k_MaxConcurrentTasks)
        {
            var allTasks = new List<Task>();
            var batch = new List<Task>(maxConcurrentTasks);
            foreach (var input in inputs)
            {
                var task = taskCreation(input);
                allTasks.Add(task);
                batch.Add(task);
                if (batch.Count < maxConcurrentTasks) continue;
                await WaitForTasksWithHandleExceptions(batch);
                batch.Clear();
            }
            if (batch.Count > 0)
            {
                await WaitForTasksWithHandleExceptions(batch);
            }
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
