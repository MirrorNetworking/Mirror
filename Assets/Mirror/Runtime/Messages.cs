using System;
using UnityEngine;

namespace Mirror
{

    #region Public System Messages

    public struct ReadyMessage { }

    public struct NotReadyMessage { }

    public struct AddPlayerMessage { }

    public struct SceneMessage 
    {
        public string scenePath;
        // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
        public SceneOperation sceneOperation;
        public string[] additiveScenes;
    }

    public struct SceneReadyMessage { }

    public enum SceneOperation : byte
    {
        Normal,
        LoadAdditive,
        UnloadAdditive
    }

    #endregion

    #region System Messages requried for code gen path
    public struct ServerRpcMessage
    {
        public uint netId;
        public int componentIndex;
        public int functionHash;

        // if the server Rpc can return values
        // this then a ServerRpcReply will be sent with this id
        public int replyId;
        // the parameters for the Cmd function
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    public struct ServerRpcReply
    {
        public int replyId;
        public ArraySegment<byte> payload;
    }

    public struct RpcMessage
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
    public struct SpawnMessage
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

    public struct ObjectDestroyMessage
    {
        public uint netId;
    }

    public struct ObjectHideMessage
    {
        public uint netId;
    }

    public struct UpdateVarsMessage
    {
        public uint netId;
        // the serialized component data
        // -> ArraySegment to avoid unnecessary allocations
        public ArraySegment<byte> payload;
    }

    // A client sends this message to the server
    // to calculate RTT and synchronize time
    public struct NetworkPingMessage
    {
        public double clientTime;
    }

    // The server responds with this message
    // The client can use this to calculate RTT and sync time
    public struct NetworkPongMessage 
    {
        public double clientTime;
        public double serverTime;
    }
    #endregion
}
