using System;
using UnityEngine;

namespace Mirror
{
    // message packing all in one place, instead of constructing headers in all
    // kinds of different places
    //
    //   MsgType     (2 bytes)
    //   Content     (ContentSize bytes)
    public static class MessagePacking
    {
        // message header size
        public const int HeaderSize = sizeof(ushort);

        // max message content size (without header) calculation for convenience
        // -> Transport.GetMaxPacketSize is the raw maximum
        // -> Every message gets serialized into <<id, content>>
        // -> Every serialized message get put into a batch with a header
        public static int MaxContentSize =>
            Transport.activeTransport.GetMaxPacketSize()
            - HeaderSize
            - Batcher.HeaderSize;

        public static ushort GetId<T>() where T : struct, NetworkMessage
        {
            // paul: 16 bits is enough to avoid collisions
            //  - keeps the message size small
            //  - in case of collisions,  Mirror will display an error
            return (ushort)(typeof(T).FullName.GetStableHashCode() & 0xFFFF);
        }

        // pack message before sending
        // -> NetworkWriter passed as arg so that we can use .ToArraySegment
        //    and do an allocation free send before recycling it.
        public static void Pack<T>(T message, NetworkWriter writer)
            where T : struct, NetworkMessage
        {
            ushort msgType = GetId<T>();
            writer.WriteUShort(msgType);

            // serialize message into writer
            writer.Write(message);
        }

        // unpack message after receiving
        // -> pass NetworkReader so it's less strange if we create it in here
        //    and pass it upwards.
        // -> NetworkReader will point at content afterwards!
        public static bool Unpack(NetworkReader messageReader, out ushort msgType)
        {
            // read message type
            try
            {
                msgType = messageReader.ReadUShort();
                return true;
            }
            catch (System.IO.EndOfStreamException)
            {
                msgType = 0;
                return false;
            }
        }

        internal static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T> handler, bool requireAuthentication)
            where T : struct, NetworkMessage
            where C : NetworkConnection
            => (conn, reader, channelId) =>
        {
            // protect against DOS attacks if attackers try to send invalid
            // data packets to crash the server/client. there are a thousand
            // ways to cause an exception in data handling:
            // - invalid headers
            // - invalid message ids
            // - invalid data causing exceptions
            // - negative ReadBytesAndSize prefixes
            // - invalid utf8 strings
            // - etc.
            //
            // let's catch them all and then disconnect that connection to avoid
            // further attacks.
            T message = default;
            // record start position for NetworkDiagnostics because reader might contain multiple messages if using batching
            int startPos = reader.Position;
            try
            {
                if (requireAuthentication && !conn.isAuthenticated)
                {
                    // message requires authentication, but the connection was not authenticated
                    Debug.LogWarning($"Closing connection: {conn}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                    conn.Disconnect();
                    return;
                }

                //Debug.Log($"ConnectionRecv {conn} msgType:{typeof(T)} content:{BitConverter.ToString(reader.buffer.Array, reader.buffer.Offset, reader.buffer.Count)}");

                // if it is a value type, just use default(T)
                // otherwise allocate a new instance
                message = reader.Read<T>();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Closed connection: {conn}. This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: {exception}");
                conn.Disconnect();
                return;
            }
            finally
            {
                int endPos = reader.Position;
                // TODO: Figure out the correct channel
                NetworkDiagnostics.OnReceive(message, channelId, endPos - startPos);
            }

            // user handler exception should not stop the whole server
            try
            {
                // user implemented handler
                handler((C)conn, message);
            }
            catch (Exception e)
            {
                Debug.LogError($"Disconnecting connId={conn.connectionId} to prevent exploits from an Exception in MessageHandler: {e.GetType().Name} {e.Message}\n{e.StackTrace}");
                conn.Disconnect();
            }
        };
    }
}
