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

    public class NetworkMessage
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

    public static class Utils
    {
        // ScaleFloatToByte( -1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 0
        // ScaleFloatToByte(  0f, -1f, 1f, byte.MinValue, byte.MaxValue) => 127
        // ScaleFloatToByte(0.5f, -1f, 1f, byte.MinValue, byte.MaxValue) => 191
        // ScaleFloatToByte(  1f, -1f, 1f, byte.MinValue, byte.MaxValue) => 255
        public static byte ScaleFloatToByte(float value, float minValue, float maxValue, byte minTarget, byte maxTarget)
        {
            // note: C# byte - byte => int, hence so many casts
            int targetRange = maxTarget - minTarget; // max byte - min byte only fits into something bigger
            float valueRange = maxValue - minValue;
            float valueRelative = value - minValue;
            return (byte)(minTarget + (byte)(valueRelative/valueRange * (float)targetRange));
        }

        // ScaleByteToFloat(  0, byte.MinValue, byte.MaxValue, -1, 1) => -1
        // ScaleByteToFloat(127, byte.MinValue, byte.MaxValue, -1, 1) => -0.003921569
        // ScaleByteToFloat(191, byte.MinValue, byte.MaxValue, -1, 1) => 0.4980392
        // ScaleByteToFloat(255, byte.MinValue, byte.MaxValue, -1, 1) => 1
        public static float ScaleByteToFloat(byte value, byte minValue, byte maxValue, float minTarget, float maxTarget)
        {
            // note: C# byte - byte => int, hence so many casts
            float targetRange = maxTarget - minTarget;
            byte valueRange = (byte)(maxValue - minValue);
            byte valueRelative = (byte)(value - minValue);
            return minTarget + ((float)valueRelative/(float)valueRange * targetRange);
        }

        // eulerAngles have 3 floats, putting them into 2 bytes of [x,y],[z,0]
        // would be a waste. instead we compress into 5 bits each => 15 bits.
        // so a ushort.
        public static ushort PackThreeFloatsIntoUShort(float u, float v, float w, float minValue, float maxValue)
        {
            // 5 bits max value = 1+2+4+8+16 = 31 = 0x1F
            byte lower = ScaleFloatToByte(u, minValue, maxValue, 0x00, 0x1F);
            byte middle = ScaleFloatToByte(v, minValue, maxValue, 0x00, 0x1F);
            byte upper = ScaleFloatToByte(w, minValue, maxValue, 0x00, 0x1F);
            ushort combined = (ushort)(upper << 10 | middle << 5 | lower);
            return combined;
        }

        // see PackThreeFloatsIntoUShort for explanation
        public static float[] UnpackUShortIntoThreeFloats(ushort combined, float minTarget, float maxTarget)
        {
            byte lower = (byte)(combined & 0x1F);
            byte middle = (byte)((combined >> 5) & 0x1F);
            byte upper = (byte)(combined >> 10); // nothing on the left, no & needed

            // note: we have to use 4 bits per float, so between 0x00 and 0x0F
            float u = ScaleByteToFloat(lower, 0x00, 0x1F, minTarget, maxTarget);
            float v = ScaleByteToFloat(middle, 0x00, 0x1F, minTarget, maxTarget);
            float w = ScaleByteToFloat(upper, 0x00, 0x1F, minTarget, maxTarget);
            return new float[]{u, v, w};
        }
    }
}
