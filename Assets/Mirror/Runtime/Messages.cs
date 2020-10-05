using System;
using UnityEngine;

namespace Mirror
{
    public interface IMessageBase
    {
    }

    public abstract class MessageBase : IMessageBase
    {
    }

    #region Public System Messages
    public struct ErrorMessage : IMessageBase
    {
        public byte value;

        public ErrorMessage(byte v)
        {
            value = v;
        }
    }

    public struct ReadyMessage : IMessageBase
    {
    }

    public struct NotReadyMessage : IMessageBase
    {
    }

    public struct AddPlayerMessage : IMessageBase
    {
    }

    // Deprecated 5/2/2020
    /// <summary>
    /// Obsolete: Removed as a security risk. Use <see cref="NetworkServer.RemovePlayerForConnection(NetworkConnection, bool)">NetworkServer.RemovePlayerForConnection</see> instead.
    /// </summary>
    [Obsolete("Removed as a security risk. Use NetworkServer.RemovePlayerForConnection(NetworkConnection conn, bool keepAuthority = false) instead")]
    public struct RemovePlayerMessage : IMessageBase
    {
    }

    public struct DisconnectMessage : IMessageBase
    {
    }

    public struct ConnectMessage : IMessageBase
    {
    }

    public struct SceneMessage : IMessageBase
    {
        public string sceneName;
        // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
        public SceneOperation sceneOperation;
        public bool customHandling;
    }

    public enum SceneOperation : byte
    {
        Normal,
        LoadAdditive,
        UnloadAdditive
    }

    #endregion

    #region System Messages requried for code gen path
    public struct CommandMessage : IMessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct RpcMessage : IMessageBase
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }
    #endregion

    #region Internal System Messages
    public struct SpawnMessage : IMessageBase
    {
        /// <summary>
        /// netId of new or existing object
        /// </summary>
        public uint netId;
        /// <summary>
        /// Is the spawning object the local player. Sets ClientScene.localPlayer
        /// </summary>
        public bool isLocalPlayer;
        /// <summary>
        /// Sets hasAuthority on the spawned object
        /// </summary>
        public bool isOwner;
        /// <summary>
        /// The id of the scene object to spawn
        /// </summary>
        public ulong sceneId;
        /// <summary>
        /// The id of the prefab to spawn
        /// <para>If sceneId != 0 then it is used instead of assetId</para>
        /// </summary>
        public Guid assetId;
        /// <summary>
        /// Local position
        /// </summary>
        public Vector3 position;
        /// <summary>
        /// Local rotation
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// Local scale
        /// </summary>
        public Vector3 scale;
        /// <summary>
        /// The serialized component data
        /// <remark>ArraySegment to avoid unnecessary allocations</remark>
        /// </summary>
        public ArraySegment<byte> payload;
    }

    public struct ObjectSpawnStartedMessage : IMessageBase
    {
    }

    public struct ObjectSpawnFinishedMessage : IMessageBase
    {
    }

    public struct ObjectDestroyMessage : IMessageBase
    {
        public uint netId;
    }

    public struct ObjectHideMessage : IMessageBase
    {
        public uint netId;
    }

    public struct UpdateVarsMessage : IMessageBase
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    public struct NetworkPingMessage : IMessageBase
    {
        public double clientTime;

        public NetworkPingMessage(double value)
        {
            clientTime = value;
        }
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    public struct NetworkPongMessage : IMessageBase
    {
        public double clientTime;
        public double serverTime;
    }
    #endregion
}
