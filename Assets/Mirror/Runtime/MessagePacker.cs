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

        // pack message before sending
        // -> pass writer instead of byte[] so we can reuse it
        public static byte[] PackMessage(ushort msgType, MessageBase msg)
        {
            // reset cached writer length and position
            packWriter.SetLength(0);

            // write message type
            packWriter.WritePackedUInt32(msgType);

            // serialize message into writer
            msg.Serialize(packWriter);

            // return byte[]
            return packWriter.ToArray();
        }

        // unpack message after receiving
        // -> pass NetworkReader so it's less strange if we create it in here
        //    and pass it upwards.
        // -> NetworkReader will point at content afterwards!
        public static bool UnpackMessage(NetworkReader messageReader, out ushort msgType)
        {
            // read message type (varint)
            msgType = (ushort)messageReader.ReadPackedUInt32();
            return true;
        }
    }
}
