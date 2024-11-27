using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AssetManager.Core.Editor
{
    // Nothing in this class actually needs to be serialized, but if we don't serialize it, we will encounter null exception errors when we update code
    [Serializable]
    class AsyncLoadOperation
    {
        Action m_CancelledCallback;

        CancellationTokenSource m_TokenSource;
        bool m_IsLoading;

        public bool IsLoading => m_IsLoading;

        public async Task Start<T>(Func<CancellationToken, IAsyncEnumerable<T>> createTaskFromToken,
            Action loadingStartCallback = null,
            Action<IEnumerable<T>> successCallback = null,
            Action<T> onItemCallback = null,
            Action cancelledCallback = null,
            Action<Exception> exceptionCallback = null,
            Action finallyCallback = null)
        {
            if (createTaskFromToken == null)
                return;

            m_CancelledCallback = cancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            m_IsLoading = true;
            loadingStartCallback?.Invoke();

            try
            {
                var result = new List<T>();

                await foreach (var item in createTaskFromToken(m_TokenSource.Token))
                {
                    if (item != null)
                    {
                        onItemCallback?.Invoke(item);
                        result.Add(item);
                    }
                }

                m_TokenSource.Token.ThrowIfCancellationRequested();

                m_IsLoading = false;
                successCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called,
                // `onLoadingCancelled` is also triggered already, the only thing left is to dispose the token source
            }
            catch (Exception e)
            {
                exceptionCallback?.Invoke(e);
            }
            finally
            {
                m_IsLoading = false;
                m_TokenSource?.Dispose();
                m_TokenSource = null;
                finallyCallback?.Invoke();
            }
        }

        public async Task Start<T>(Func<CancellationToken, Task<T>> createTaskFromToken,
            Action loadingStartCallback = null,
            Action<T> successCallback = null,
            Action cancelledCallback = null,
            Action<Exception> exceptionCallback = null,
            Action finallyCallback = null)
        {
            if (IsLoading || createTaskFromToken == null)
                return;

            m_CancelledCallback = cancelledCallback;

            m_TokenSource = new CancellationTokenSource();

            loadingStartCallback?.Invoke();

            try
            {
                var result = await createTaskFromToken(m_TokenSource.Token);

                m_TokenSource.Token.ThrowIfCancellationRequested();

                successCallback?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // We do nothing here because when `CancelLoading` is called,
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
                finallyCallback?.Invoke();
            }
        }

        public void Cancel()
        {
            if (m_TokenSource == null)
                return;

            m_TokenSource.Cancel();

            m_CancelledCallback?.Invoke();
            m_CancelledCallback = null;
        }
    }
}
