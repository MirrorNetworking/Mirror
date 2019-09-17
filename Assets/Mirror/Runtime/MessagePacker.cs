using System;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    // message packing all in one place, instead of constructing headers in all
    // kinds of different places
    //
    //   MsgType     (1-n bytes)
    //   Content     (ContentSize bytes)
    //
    // -> we use varint for headers because most messages will result in 1 byte
    //    type/size headers then instead of always
    //    using 2 bytes for shorts.
    // -> this reduces bandwidth by 10% if average message size is 20 bytes
    //    (probably even shorter)
    public static class MessagePacker
    {
        public static int GetId<T>() where T : IMessageBase
        {
            // paul: 16 bits is enough to avoid collisions
            //  - keeps the message size small because it gets varinted
            //  - in case of collisions,  Mirror will display an error
            return typeof(T).FullName.GetStableHashCode() & 0xFFFF;
        }

        // pack message before sending
        // -> pass writer instead of byte[] so we can reuse it
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use Pack<T> instead")]
        public static byte[] PackMessage(int msgType, MessageBase msg)
        {
            NetworkWriter writer = NetworkWriterPool.GetWriter();
            try
            {
                // write message type
                writer.WriteInt16((short)msgType);

                // serialize message into writer
                msg.Serialize(writer);

                // return byte[]
                return writer.ToArray();
            }
            finally
            {
                NetworkWriterPool.Recycle(writer);
            }
        }

        // pack message before sending
        public static byte[] Pack<T>(T message) where T : IMessageBase
        {
            NetworkWriter writer = NetworkWriterPool.GetWriter();
            try
            {
                // write message type
                int msgType = GetId<T>();
                writer.WriteUInt16((ushort)msgType);

                // serialize message into writer
                message.Serialize(writer);

                // return byte[]
                return writer.ToArray();
            }
            finally
            {
                NetworkWriterPool.Recycle(writer);
            }
        }

        // unpack a message we received
        public static T Unpack<T>(byte[] data) where T : IMessageBase, new()
        {
            NetworkReader reader = new NetworkReader(data);

            int msgType = GetId<T>();

            int id = reader.ReadUInt16();
            if (id != msgType)
                throw new FormatException("Invalid message,  could not unpack " + typeof(T).FullName);

            T message = new T();
            message.Deserialize(reader);
            return message;
        }

        // unpack message after receiving
        // -> pass NetworkReader so it's less strange if we create it in here
        //    and pass it upwards.
        // -> NetworkReader will point at content afterwards!
        public static bool UnpackMessage(NetworkReader messageReader, out int msgType)
        {
            // read message type (varint)
            try
            {
                msgType = messageReader.ReadUInt16();
                return true;
            }
            catch (System.IO.EndOfStreamException)
            {
                msgType = 0;
                return false;
            }
        }

        internal static NetworkMessageDelegate MessageHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthenication) where T : IMessageBase, new() => networkMessage =>
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
            try
            {
                if (requireAuthenication && !networkMessage.conn.isAuthenticated)
                {
                    // message requires authentication, but the connection was no authnticated
                    Debug.LogWarning($"Closing connection: {networkMessage.conn.connectionId}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                    networkMessage.conn.Disconnect();
                    return;
                }

                message = networkMessage.ReadMessage<T>();
            }
            catch (Exception exception)
            {
                Debug.LogError("Closed connection: " + networkMessage.conn.connectionId + ". This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: " + exception);
                networkMessage.conn.Disconnect();
                return;
            }
            finally
            {
                // TODO: Figure out the correct channel
                NetworkDiagnostics.OnReceive(message, -1, networkMessage.reader.Length);
            }

            handler(networkMessage.conn, message);
        };
    }
}
