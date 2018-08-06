using System;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    // Handles network messages on client and server
    public delegate void NetworkMessageDelegate(NetworkMessage netMsg);

    // Handles requests to spawn objects on the client
    public delegate GameObject SpawnDelegate(Vector3 position, NetworkHash128 assetId);

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
        SyncList = 9,
        SpawnPrefab = 3,
        SpawnSceneObject = 10,
        SpawnFinished = 12,
        ObjectHide = 13,
        LocalClientAuthority = 15,
        LocalChildTransform = 16,

        // used for profiling
        UserMessage = 0,
        HLAPIMsg = 28,
        LLAPIMsg = 29,

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

        Highest = 47
    }

    public class NetworkMessage
    {
        public const int MaxMessageSize = (64 * 1024) - 1;

        public short msgType;
        public NetworkConnection conn;
        public NetworkReader reader;
        public int channelId;

        public static string Dump(byte[] payload, int sz)
        {
            string outStr = "[";
            for (int i = 0; i < sz; i++)
            {
                outStr += (payload[i] + " ");
            }
            outStr += "]";
            return outStr;
        }

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

        // moved IsSequenced etc. functions here because it's better than in NetworkConnection
        public static bool IsSequencedQoS(QosType qos)
        {
            return (qos == QosType.ReliableSequenced || qos == QosType.UnreliableSequenced);
        }

        public static bool IsReliableQoS(QosType qos)
        {
            return (qos == QosType.Reliable || qos == QosType.ReliableFragmented || qos == QosType.ReliableSequenced || qos == QosType.ReliableStateUpdate);
        }

        public static bool IsUnreliableQoS(QosType qos)
        {
            return (qos == QosType.Unreliable || qos == QosType.UnreliableFragmented || qos == QosType.UnreliableSequenced || qos == QosType.StateUpdate);
        }
    }

    // network protocol all in one place, instead of constructing headers in all kinds of different places
    //
    //   MsgType     (2 bytes)
    //   ContentSize (2 bytes)
    //   Content     (ContentSize bytes)
    public static class Protocol
    {
        // pack message before sending
        public static byte[] PackMessage(ushort msgType, byte[] content)
        {
            // original HLAPI's 'content' part is never null, so we don't have to handle that case.
            // just create an empty array if null.
            if (content == null) content = new byte[0];

            NetworkWriter writer = new NetworkWriter();

            // message content size (short)
            if (content.Length > UInt16.MaxValue)
            {
                if (LogFilter.logError) { Debug.LogError("PackMessage: size is too large (" + content.Length + ") bytes. The maximum buffer size is " + UInt16.MaxValue + " bytes."); }
                return null;
            }
            writer.Write((UInt16)content.Length);

            // message type (short)
            writer.Write((UInt16)msgType);

            // message content (if any)
            writer.Write(content, 0, content.Length);

            return writer.ToArray();
        }

        // unpack message after receiving
        public static bool UnpackMessage(byte[] message, out ushort msgType, out byte[] content)
        {
            NetworkReader reader = new NetworkReader(message);

            // read content size (short)
            UInt16 size = reader.ReadUInt16();

            // read message type
            msgType = reader.ReadUInt16();

            // read content (if any)
            content = reader.ReadBytes(size);

            return content.Length == size;
        }
    }
}


#endif //ENABLE_UNET
