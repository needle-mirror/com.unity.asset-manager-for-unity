using System;
using System.IO;
using System.Threading;

namespace Unity.Cloud.CommonEmbedded.Runtime
{
    /// <summary>
    /// A memory stream wrapper that allows simultaneous reading and writing.
    /// </summary>
    internal class TwoWayMemoryStream : MemoryStream
    {
        static AutoResetEvent m_AutoResetEvent = new (false);

        readonly MemoryStream m_InnerStream;
        readonly object m_Lock = new ();

        long m_ReadPosition;
        long m_WritePosition;
        bool m_Completed;

        public TwoWayMemoryStream()
        {
            m_InnerStream = new MemoryStream();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            while (true)
            {
                Monitor.Enter(m_Lock);
                if (m_ReadPosition >= m_WritePosition && !m_Completed)
                {
                    Monitor.Exit(m_Lock);
                    m_AutoResetEvent.WaitOne();
                }
                else
                    break;
            }
            
            try
            {
                m_InnerStream.Position = m_ReadPosition;
                int bytesRead = m_InnerStream.Read(buffer, offset, count);
                m_ReadPosition = m_InnerStream.Position;

                return bytesRead;
            }
            finally
            {
                Monitor.Exit(m_Lock);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_InnerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (m_Lock)
            {
                m_InnerStream.Position = Volatile.Read(ref m_WritePosition);
                m_InnerStream.Write(buffer, offset, count);
                Volatile.Write(ref m_WritePosition, m_InnerStream.Position);
            }

            m_AutoResetEvent.Set();
        }

        public void SetCompleted()
        {
            lock(m_Lock)
            {
                m_Completed = true;
                m_AutoResetEvent.Set();
            }
        }
    }
}
