namespace Mirror
{
    // remote calls like Rpc/Cmd/SyncEvent all use the same message type
    class RemoteCallMessage : MessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        public byte[] payload; // the parameters for the Cmd function

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadPackedUInt32();
            componentIndex = (int)reader.ReadPackedUInt32();
            functionHash = reader.ReadInt32(); // hash is always 4 full bytes, WritePackedInt would send 1 extra byte here
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32(netId);
            writer.WritePackedUInt32((uint)componentIndex);
            writer.Write(functionHash);
            writer.WriteBytesAndSize(payload);
        }
    }

    class CommandMessage : RemoteCallMessage {}

    class RpcMessage : RemoteCallMessage {}

    class SyncEventMessage : RemoteCallMessage {}
}
