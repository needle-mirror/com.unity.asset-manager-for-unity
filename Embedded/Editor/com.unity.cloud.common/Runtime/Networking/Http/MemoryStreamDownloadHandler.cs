using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.Cloud.CommonEmbedded.Runtime
{
    internal class MemoryStreamDownloadHandler : DownloadHandlerScript
    {
        public int ContentLength => m_BytesReceived > m_ContentLength ? m_BytesReceived : m_ContentLength;

        public event Action HeadersReceived;

        public TwoWayMemoryStream OutputStream { get; private set; }

        public bool DataReceived { get; private set; }

        int m_ContentLength;
        int m_BytesReceived;
        bool m_HeadersReceivedInvoked;

        public MemoryStreamDownloadHandler(int bufferSize = 4096) : base(new byte[bufferSize])
        {
            m_ContentLength = -1;
            m_BytesReceived = 0;
            m_HeadersReceivedInvoked = false;
            DataReceived = false;

            OutputStream = new TwoWayMemoryStream();
        }

        protected override float GetProgress()
        {
            return ContentLength <= 0 ? 0 : Mathf.Clamp01((float)m_BytesReceived / (float)ContentLength);
        }

        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            m_ContentLength = (int)contentLength;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || data.Length == 0)
                return false;

            TryInvokeHeadersReceived();

            m_BytesReceived += dataLength;
            OutputStream.Write(data, 0, dataLength);
            
            DataReceived = true;
            return true;
        }

        protected override void CompleteContent()
        {
            TryInvokeHeadersReceived();

            OutputStream.SetCompleted();
        }

        public override void Dispose()
        {
            CloseStream();
            base.Dispose();
        }

        /// <summary>
        /// Used in the event of an error that prevents the download handler from calling complete content.
        /// </summary>
        public void ForceCompleteContent()
        {
            CompleteContent();
        }

        void TryInvokeHeadersReceived()
        {
            if (!m_HeadersReceivedInvoked)
            {
                HeadersReceived?.Invoke();
                m_HeadersReceivedInvoked = true;
            }
        }

        void CloseStream()
        {
            if (OutputStream != null)
            {
                OutputStream.Dispose();
                OutputStream = null;
            }
        }
    }
}
