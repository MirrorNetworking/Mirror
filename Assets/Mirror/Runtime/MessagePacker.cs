using System;

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
        // PackMessage is in hot path. caching the writer is really worth it to
        // avoid large amounts of allocations.
        static NetworkWriter packWriter = new NetworkWriter();

        public static int GetId<T>() where T : MessageBase
        {
            // paul: 16 bits is enough to avoid collisions
            //  - keeps the message size small because it gets varinted
            //  - in case of collisions,  Mirror will display an error
            return typeof(T).FullName.GetStableHashCode() & 0xFFFF;
        }

        // pack message before sending
        // -> pass writer instead of byte[] so we can reuse it
        [Obsolete("Use Pack<T> instead")]
        public static byte[] PackMessage(int msgType, MessageBase msg)
        {
            // reset cached writer length and position
            packWriter.SetLength(0);

            // write message type
            packWriter.Write((short)msgType);

            // serialize message into writer
            msg.Serialize(packWriter);

            // return byte[]
            return packWriter.ToArray();
        }

        // pack message before sending
        public static byte[] Pack<T>(T message) where T : MessageBase
        {
            // reset cached writer length and position
            packWriter.SetLength(0);

            // write message type
            int msgType = GetId<T>();
            packWriter.Write((ushort)msgType);

            // serialize message into writer
            message.Serialize(packWriter);

            // return byte[]
            return packWriter.ToArray();
        }

        // unpack a message we received
        public static T Unpack<T>(byte[] data) where T : MessageBase, new()
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
            msgType = (int)messageReader.ReadUInt16();
            return true;
        }
    }
}
