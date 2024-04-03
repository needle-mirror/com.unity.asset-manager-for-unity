using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    // Nothing in this class actually needs to be serialized, but if we don't serialize it, we will encounter null exception errors when we update code
    [Serializable]
    internal class AsyncLoadOperation
    {
        private Action m_OnCancelledCallback;

        public bool isLoading => m_TokenSource != null;

        private CancellationTokenSource m_TokenSource;

        public async Task Start<T>(Func<CancellationToken, IAsyncEnumerable<T>> createTaskFromToken, Action onLoadingStartCallback = null, Action<IEnumerable<T>> onSuccessCallback = null, Action onCancelledCallback = null, Action<Exception> onExceptionCallback = null)
        {
            if (isLoading || createTaskFromToken == null)
                return;

            m_OnCancelledCallback = onCancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            onLoadingStartCallback?.Invoke();

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

                m_TokenSource = null;
                onSuccessCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called, we already set m_TokenSource to null and set loading to false,
                // `onLoadingCancelled` is also triggered already, the only thing left is to dispose the token source
            }
            catch (Exception e)
            {
                onExceptionCallback?.Invoke(e);
            }
            finally
            {
                m_TokenSource?.Dispose();
                m_TokenSource = null;
            }
        }

        public async Task Start<T>(Func<CancellationToken, Task<T>> createTaskFromToken, Action onLoadingStartCallback = null, Action<T> onSuccessCallback = null, Action onCancelledCallback = null, Action<Exception> onExceptionCallback = null)
        {
            if (isLoading || createTaskFromToken == null)
                return;

            m_OnCancelledCallback = onCancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            onLoadingStartCallback?.Invoke();

            try
            {
                var result = await createTaskFromToken(m_TokenSource.Token);

                m_TokenSource = null;
                onSuccessCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called, we already set m_TokenSource to null and set loading to false,
                // `onLoadingCancelled` is also triggered already, the only thing left is to dispose the token source
            }
            catch (Exception e)
            {
                onExceptionCallback?.Invoke(e);
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
            m_TokenSource = null;

            m_OnCancelledCallback?.Invoke();
            m_OnCancelledCallback = null;
        }
    }
}