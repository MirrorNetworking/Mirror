using System;
using UnityEngine;

namespace Mirror
{
    // Deprecated 10/06/2020
    [Obsolete("Implement NetworkMessage instead. Use extension methods instead of Serialize/Deserialize, see https://github.com/vis2k/Mirror/pull/2317", true)]
    public interface IMessageBase {}

    // Deprecated 10/06/2020
    [Obsolete("Implement NetworkMessage instead. Use extension methods instead of Serialize/Deserialize, see https://github.com/vis2k/Mirror/pull/2317", true)]
    public class MessageBase : IMessageBase {}

    #region Public System Messages
    public struct ReadyMessage : NetworkMessage {}

    public struct NotReadyMessage : NetworkMessage {}

    public struct AddPlayerMessage : NetworkMessage {}

    public struct SceneMessage : NetworkMessage
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

    #region System Messages required for code gen path
    public struct CommandMessage : NetworkMessage
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct RpcMessage : NetworkMessage
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
    public struct SpawnMessage : NetworkMessage
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

    public struct ObjectSpawnStartedMessage : NetworkMessage {}

    public struct ObjectSpawnFinishedMessage : NetworkMessage {}

    public struct ObjectDestroyMessage : NetworkMessage
    {
        public uint netId;
    }

    public struct ObjectHideMessage : NetworkMessage
    {
        public uint netId;
    }

    public struct UpdateVarsMessage : NetworkMessage
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    public struct NetworkPingMessage : NetworkMessage
    {
        public double clientTime;

        public NetworkPingMessage(double value)
        {
            clientTime = value;
        }
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    public struct NetworkPongMessage : NetworkMessage
    {
        public double clientTime;
        public double serverTime;
    }
    #endregion
}
