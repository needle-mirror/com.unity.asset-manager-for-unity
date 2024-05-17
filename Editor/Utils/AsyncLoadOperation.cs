using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    // Nothing in this class actually needs to be serialized, but if we don't serialize it, we will encounter null exception errors when we update code
    [Serializable]
    class AsyncLoadOperation
    {
        Action m_CancelledCallback;

        CancellationTokenSource m_TokenSource;
        SemaphoreSlim m_Semaphore = new SemaphoreSlim(1, 1);

        public bool isLoading => m_TokenSource != null;

        public async Task Start<T>(Func<CancellationToken, IAsyncEnumerable<T>> createTaskFromToken,
            Action loadingStartCallback = null, Action<IEnumerable<T>> successCallback = null,
            Action cancelledCallback = null, Action<Exception> exceptionCallback = null)
        {
            if (createTaskFromToken == null)
                return;

            await m_Semaphore.WaitAsync();

            m_CancelledCallback = cancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            loadingStartCallback?.Invoke();

            try
            {
                var result = new List<T>();
                await foreach (var item in createTaskFromToken(m_TokenSource.Token))
                {
                    if (item != null)
                    {
                        result.Add(item); // TODO Add a callback for each item instead
                    }
                }

                m_TokenSource?.Dispose();
                m_TokenSource = null;
                successCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called, we already set m_TokenSource to null and set loading to false,
                // `onLoadingCancelled` is also triggered already, the only thing left is to dispose the token source
            }
            catch (Exception e)
            {
                exceptionCallback?.Invoke(e);
            }
            finally
            {
                m_TokenSource?.Dispose();
                m_TokenSource = null;

                m_Semaphore.Release(1);
            }
        }

        public async Task Start<T>(Func<CancellationToken, Task<T>> createTaskFromToken,
            Action loadingStartCallback = null, Action<T> successCallback = null, Action cancelledCallback = null,
            Action<Exception> exceptionCallback = null)
        {
            if (isLoading || createTaskFromToken == null)
                return;

            m_CancelledCallback = cancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            loadingStartCallback?.Invoke();

            try
            {
                var result = await createTaskFromToken(m_TokenSource.Token);

                m_TokenSource = null;
                successCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called, we already set m_TokenSource to null and set loading to false,
                // `onLoadingCancelled` is also triggered already, the only thing left is to dispose the token source
            }
            catch (Exception e)
            {
                exceptionCallback?.Invoke(e);
            }
            finally
            {
                m_TokenSource?.Dispose();
                m_TokenSource = null;
            }
        }

        public void Cancel()
        {
            if (m_TokenSource == null)
                return;

            m_TokenSource.Cancel();
            m_TokenSource.Dispose();
            m_TokenSource = null;

            m_CancelledCallback?.Invoke();
            m_CancelledCallback = null;
        }
    }
}
