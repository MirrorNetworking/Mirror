using System;
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
        static readonly ILogger logger = LogFactory.GetLogger(typeof(MessagePacker));

        public static int GetId<T>() where T : NetworkMessage
        {
            return GetId(typeof(T));
        }

        public static int GetId(Type type)
        {
            // paul: 16 bits is enough to avoid collisions
            //  - keeps the message size small because it gets varinted
            //  - in case of collisions,  Mirror will display an error
            return type.FullName.GetStableHashCode() & 0xFFFF;
        }

        // pack message before sending
        // -> NetworkWriter passed as arg so that we can use .ToArraySegment
        //    and do an allocation free send before recycling it.
        public static void Pack<T>(T message, NetworkWriter writer) where T : NetworkMessage
        {
            // if it is a value type,  just use typeof(T) to avoid boxing
            // this works because value types cannot be derived
            // if it is a reference type (for example NetworkMessage),
            // ask the message for the real type
            int msgType = GetId(default(T) != null ? typeof(T) : message.GetType());
            writer.WriteUInt16((ushort)msgType);

            // serialize message into writer
            writer.Write(message);
        }

        // unpack a message we received
        public static T Unpack<T>(byte[] data) where T : NetworkMessage
        {
            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(data))
            {
                int msgType = GetId<T>();

                int id = networkReader.ReadUInt16();
                if (id != msgType)
                    throw new FormatException("Invalid message,  could not unpack " + typeof(T).FullName);

                return networkReader.Read<T>();
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
            where T : NetworkMessage
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
                    logger.LogWarning($"Closing connection: {conn}. Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                    conn.Disconnect();
                    return;
                }

                // if it is a value type, just use defult(T)
                // otherwise allocate a new instance
                message = reader.Read<T>();
            }
            catch (Exception exception)
            {
                logger.LogError("Closed connection: " + conn + ". This can happen if the other side accidentally (or an attacker intentionally) sent invalid data. Reason: " + exception);
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
