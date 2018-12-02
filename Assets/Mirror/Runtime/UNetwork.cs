using System;
using UnityEngine;

namespace Mirror
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkMessage netMsg);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, Guid assetId);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

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
        LocalPlayerTransform = 6,
        SyncEvent = 7,
        UpdateVars = 8,
        SpawnPrefab = 3,
        SpawnSceneObject = 10,
        SpawnFinished = 12,
        ObjectHide = 13,
        LocalClientAuthority = 15,
        LocalChildTransform = 16,

        // public system messages - can be replaced by user code
        Connect = 32,
        Disconnect = 33,
        Error = 34,
        Ready = 35,
        NotReady = 36,
        AddPlayer = 37,
        RemovePlayer = 38,
        Scene = 39,
        Animation = 40,
        AnimationParameters = 41,
        AnimationTrigger = 42,

        // time synchronization
        Ping = 43,
        Pong = 44,

        Highest = 47
    }

    public class NetworkMessage
    {
        public short msgType;
        public NetworkConnection conn;
        public NetworkReader reader;

        public TMsg ReadMessage<TMsg>() where TMsg : MessageBase, new()
        {
            var msg = new TMsg();
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

    public class Channels
    {
        public const int DefaultReliable = 0;
        public const int DefaultUnreliable = 1;
    }

    // network protocol all in one place, instead of constructing headers in all kinds of different places
    //
    //   MsgType     (1-n bytes)
    //   Content     (ContentSize bytes)
    //
    // -> we use varint for headers because most messages will result in 1 byte type/size headers then instead of always
    //    using 2 bytes for shorts.
    // -> this reduces bandwidth by 10% if average message size is 20 bytes (probably even shorter)
    public static class Protocol
    {
        // pack message before sending
        public static byte[] PackMessage(ushort msgType, byte[] content)
        {
            // original HLAPI's 'content' part is never null, so we don't have to handle that case.
            // just create an empty array if null.
            if (content == null) content = new byte[0];

            NetworkWriter writer = new NetworkWriter();

            // message type (varint)
            writer.WritePackedUInt32(msgType);

            // message content (if any)
            writer.Write(content, 0, content.Length);

            return writer.ToArray();
        }

        // unpack message after receiving
        public static bool UnpackMessage(byte[] message, out ushort msgType, out byte[] content)
        {
            NetworkReader reader = new NetworkReader(message);

            // read message type (varint)
            msgType = (UInt16)reader.ReadPackedUInt32();

            // read content (remaining data in message)
            content = reader.ReadBytes(reader.Length - reader.Position);

            return true;
        }
    }
}
