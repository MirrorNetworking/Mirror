#if ENABLE_UNET
using System;

namespace UnityEngine.Networking
{
    // This is used by the ChannelBuffer when buffering traffic.
    // Unreliable channels have a single ChannelPacket, Reliable channels have single "current" packet and a list of buffered ChannelPackets
    struct ChannelPacket
    {
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

            bool result = true;
            if (!conn.TransportSend(m_Buffer, (ushort)m_Position, channelId, out error))
            {
                if (m_IsReliable && error == (int)NetworkError.NoResources)
                {
                    // handled below
                }
                else
                {
                    if (LogFilter.logError) { Debug.LogError("Failed to send internal buffer channel:" + channelId + " bytesToSend:" + m_Position); }
                    result = false;
                }
            }
            if (error != 0)
            {
                if (m_IsReliable && error == (int)NetworkError.NoResources)
                {
                    // this packet will be buffered by the containing ChannelBuffer, so this is not an error

#if UNITY_EDITOR
                    UnityEditor.NetworkDetailStats.IncrementStat(
                        UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                        MsgType.HLAPIResend, "msg", 1);
#endif
                    return false;
                }

                if (LogFilter.logError) { Debug.LogError("Send Error: " + (NetworkError)error + " channel:" + channelId + " bytesToSend:" + m_Position); }
                result = false;
            }
            m_Position = 0;
            return result;
        }

        int m_Position;
        byte[] m_Buffer;
        bool m_IsReliable;
    }
}
#endif //ENABLE_UNET
