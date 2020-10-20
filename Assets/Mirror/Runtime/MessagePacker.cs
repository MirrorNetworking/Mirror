using System;
using System.Collections.Generic;
using System.ComponentModel;

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
        /// <summary>
        /// Map of Message Id => Type
        /// When we receive a message, we can lookup here to find out what type
        /// it was.  This is populated by the weaver.
        /// </summary>
        private static readonly Dictionary<int, Type> messageTypes = new Dictionary<int, Type>();

        public static Type GetMessageType(int id) => messageTypes[id];

        public static void RegisterMessage<T>()
        {
            int id = GetId<T>();

            if (messageTypes.TryGetValue(id, out Type type) && type != typeof(T)) {
                throw new ArgumentException($"Message {typeof(T)} and {messageTypes[id]} have the same ID. Change the name of one of those messages");
            }
            messageTypes[id] = typeof(T);
        }

        public static int GetId<T>()
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
        public static void Pack<T>(T message, NetworkWriter writer)
        {
            // if it is a value type,  just use typeof(T) to avoid boxing
            // this works because value types cannot be derived
            // if it is a reference type (for example IMessageBase),
            // ask the message for the real type
            Type mstType = default(T) == null && message != null ? message.GetType() : typeof(T);

            int msgType = GetId(mstType);
            writer.WriteUInt16((ushort)msgType);

            writer.Write(message);
        }

        // helper function to pack message into a simple byte[] (which allocates)
        // => useful for tests
        // => useful for local client message enqueue
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static byte[] Pack<T>(T message)
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                Pack(message, writer);
                byte[] data = writer.ToArray();

                return data;
            }
        }

        // unpack a message we received
        public static T Unpack<T>(byte[] data)
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
        public static int UnpackId(NetworkReader messageReader)
        {
            return messageReader.ReadUInt16();
        }
    }
}
