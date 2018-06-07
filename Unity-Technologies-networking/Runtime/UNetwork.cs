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
    public class MsgType
    {
        // internal system messages - cannot be replaced by user code
        public const short ObjectDestroy = 1;
        public const short Rpc = 2;
        public const short ObjectSpawn = 3;
        public const short Owner = 4;
        public const short Command = 5;
        public const short LocalPlayerTransform = 6;
        public const short SyncEvent = 7;
        public const short UpdateVars = 8;
        public const short SyncList = 9;
        public const short ObjectSpawnScene = 10;
        public const short NetworkInfo = 11;
        public const short SpawnFinished = 12;
        public const short ObjectHide = 13;
        public const short CRC = 14;
        public const short LocalClientAuthority = 15;
        public const short LocalChildTransform = 16;
        public const short Fragment = 17;
        public const short PeerClientAuthority = 18;

        // used for profiling
        internal const short UserMessage = 0;
        internal const short HLAPIMsg = 28;
        internal const short LLAPIMsg = 29;
        internal const short HLAPIResend = 30;
        internal const short HLAPIPending = 31;

        public const short InternalHighest = 31;

        // public system messages - can be replaced by user code
        public const short Connect = 32;
        public const short Disconnect = 33;
        public const short Error = 34;
        public const short Ready = 35;
        public const short NotReady = 36;
        public const short AddPlayer = 37;
        public const short RemovePlayer = 38;
        public const short Scene = 39;
        public const short Animation = 40;
        public const short AnimationParameters = 41;
        public const short AnimationTrigger = 42;
        public const short LobbyReadyToBegin = 43;
        public const short LobbySceneLoaded = 44;
        public const short LobbyAddPlayerFailed = 45;
        public const short LobbyReturnToLobby = 46;
#if ENABLE_UNET_HOST_MIGRATION
        public const short ReconnectPlayer = 47;
#endif

        //NOTE: update msgLabels below if this is changed.
        public const short Highest = 47;

        static internal string[] msgLabels =
        {
            "none",
            "ObjectDestroy",
            "Rpc",
            "ObjectSpawn",
            "Owner",
            "Command",
            "LocalPlayerTransform",
            "SyncEvent",
            "UpdateVars",
            "SyncList",
            "ObjectSpawnScene", // 10
            "NetworkInfo",
            "SpawnFinished",
            "ObjectHide",
            "CRC",
            "LocalClientAuthority",
            "LocalChildTransform",
            "Fragment",
            "PeerClientAuthority",
            "",
            "", // 20
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "", // 30
            "", // - SystemInternalHighest
            "Connect", // 32,
            "Disconnect",
            "Error",
            "Ready",
            "NotReady",
            "AddPlayer",
            "RemovePlayer",
            "Scene",
            "Animation", // 40
            "AnimationParams",
            "AnimationTrigger",
            "LobbyReadyToBegin",
            "LobbySceneLoaded",
            "LobbyAddPlayerFailed", // 45
            "LobbyReturnToLobby", // 46
#if ENABLE_UNET_HOST_MIGRATION
            "ReconnectPlayer", // 47
#endif
        };

        static public string MsgTypeToString(short value)
        {
            if (value < 0 || value > Highest)
            {
                return String.Empty;
            }
            string result =  msgLabels[value];
            if (string.IsNullOrEmpty(result))
            {
                result = "[" + value + "]";
            }
            return result;
        }
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
    }

    public enum ChannelOption
    {
        MaxPendingBuffers = 1,
        AllowFragmentation = 2,
        MaxPacketSize = 3
            // maybe add an InitialCapacity for Pending Buffers list if needed in the future
    }
}


#endif //ENABLE_UNET
