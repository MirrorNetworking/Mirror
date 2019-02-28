using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkMessage netMsg);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, NetworkGuid assetId);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

    // invoke type for Cmd/Rpc/SyncEvents
    public enum UNetInvokeType
    {
        Command,
        ClientRpc,
        SyncEvent
    }

    // built-in system network messages
    // original HLAPI uses short, so let's keep short to not break packet header etc.
    // => use .ToString() to get the field name from the field value
    // => we specify the short values so it's easier to look up opcodes when debugging packets
    public enum MsgType : short
    {
        // internal system messages - cannot be replaced by user code
        ObjectDestroy = 1,
        Rpc = 2,
        Owner = 4,
        Command = 5,
        SyncEvent = 7,
        UpdateVars = 8,
        SpawnPrefab = 3,
        SpawnSceneObject = 10,
        SpawnStarted = 11,
        SpawnFinished = 12,
        ObjectHide = 13,
        LocalClientAuthority = 15,

        // public system messages - can be replaced by user code
        Connect = 32,
        Disconnect = 33,
        Error = 34,
        Ready = 35,
        NotReady = 36,
        AddPlayer = 37,
        RemovePlayer = 38,
        Scene = 39,

        // time synchronization
        Ping = 43,
        Pong = 44,

        Highest = 47
    }

    public struct NetworkMessage
    {
        public short msgType;
        public NetworkConnection conn;
        public NetworkReader reader;

        public TMsg ReadMessage<TMsg>() where TMsg : MessageBase, new()
        {
            TMsg msg = new TMsg();
            msg.Deserialize(reader);
            return msg;
        }

        public void ReadMessage<TMsg>(TMsg msg) where TMsg : MessageBase
        {
            msg.Deserialize(reader);
        }
    }

    public enum Version
    {
        Current = 1
    }

    public static class Channels
    {
        public const int DefaultReliable = 0;
        public const int DefaultUnreliable = 1;
    }

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
            msgType = (UInt16)messageReader.ReadPackedUInt32();
            return true;
        }
    }
}
