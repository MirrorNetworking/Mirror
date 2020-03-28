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

        public static int GetId(Type type)
        {
            return type.FullName.GetStableHashCode() & 0xFFFF;
        }

        // pack message before sending
        // -> NetworkWriter passed as arg so that we can use .ToArraySegment
        //    and do an allocation free send before recycling it.
        public static void Pack<T>(T message, NetworkWriter writer) where T : IMessageBase
        {
            // if it is a value type,  just use typeof(T) to avoid boxing
            // this works because value types cannot be derived
            // if it is a reference type (for example IMessageBase),
            // ask the message for the real type
            int msgType = GetId(default(T) != null ? typeof(T) : message.GetType());
            writer.WriteUInt16((ushort)msgType);

            // serialize message into writer
            message.Serialize(writer);
        }

        // helper function to pack message into a simple byte[] (which allocates)
        // => useful for tests
        // => useful for local client message enqueue
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static byte[] Pack<T>(T message) where T : IMessageBase
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                Pack(message, writer);
                byte[] data = writer.ToArray();

                return data;
            }
        }

        // unpack a message we received
        public static T Unpack<T>(byte[] data) where T : IMessageBase, new()
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(data))
            {
                int msgType = GetId<T>();

                int id = networkReader.ReadUInt16();
                if (id != msgType)
                    throw new FormatException("Invalid message,  could not unpack " + typeof(T).FullName);

                T message = new T();
                message.Deserialize(networkReader);

                return message;
            }
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

        internal static NetworkMessageDelegate MessageHandler<T, C>(Action<C, T> handler, bool requireAuthenication)
            where T : IMessageBase, new()
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
            try
            {
                if (requireAuthenication && !conn.isAuthenticated)
                {
                    // message requires authentication, but the connection was not authenticated
                    Debug.LogWarning($"Closing connection: {conn}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                    conn.Disconnect();
                    return;
                }

                // if it is a value type, just use defult(T)
                // otherwise allocate a new instance
                message = default(T) != null ? default(T) : new T();
                message.Deserialize(reader);
            }
            catch (Exception exception)
            {
                Debug.LogError("Closed connection: " + conn + ". This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: " + exception);
                conn.Disconnect();
                return;
            }
            finally
            {
                // TODO: Figure out the correct channel
                NetworkDiagnostics.OnReceive(message, channelId, reader.Length);
            }

            handler((C)conn, message);
        };
    }
}
