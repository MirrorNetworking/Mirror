#if ENABLE_UNET
using System;

namespace UnityEngine.Networking
{
    // This is used by the ChannelBuffer when buffering traffic.
    // Unreliable channels have a single ChannelPacket, Reliable channels have single "current" packet and a list of buffered ChannelPackets
    struct ChannelPacket
    {
        int m_Position;
        byte[] m_Buffer;
        bool m_IsReliable;

        public ChannelPacket(int packetSize, bool isReliable)
        {
            m_Position = 0;
            m_Buffer = new byte[packetSize];
            m_IsReliable = isReliable;
        }

        public void Reset()
        {
            m_Position = 0;
        }

        public bool IsEmpty()
        {
            return m_Position == 0;
        }

        public void Write(byte[] bytes, int numBytes)
        {
            Array.Copy(bytes, 0, m_Buffer, m_Position, numBytes);
            m_Position += numBytes;
        }

        public bool HasSpace(int numBytes)
        {
            return m_Position + numBytes <= m_Buffer.Length;
        }

        public bool SendToTransport(NetworkConnection conn, int channelId)
        {
            byte error;
            if (conn.TransportSend(m_Buffer, (ushort)m_Position, channelId, out error))
            {
                m_Position = 0;
                return true;
            }
            else
            {
                // NoResources and reliable? Then it will be resent, so don't reset position, just return false.
                if (error == (int)NetworkError.NoResources && m_IsReliable)
                {
#if UNITY_EDITOR
                    UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                        MsgType.HLAPIResend, "msg", 1);
#endif
                    return false;
                }

                // otherwise something unexpected happened. log the error, reset position and return.
                if (LogFilter.logError) { Debug.LogError("SendToTransport failed. error:" + (NetworkError)error + " channel:" + channelId + " bytesToSend:" + m_Position); }
                m_Position = 0;
                return false;
            }
        }
    }
}
#endif //ENABLE_UNET
