// old static NetworkClient class.
// to be replaced by NetClient as component.
// points to NetClient.instance for easier migration.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>NetworkClient with connection to server.</summary>
    public static partial class NetworkClient
    {
        internal static Dictionary<ushort, NetworkMessageDelegate> handlers => NetClient.instance.handlers;

        /// <summary>All spawned NetworkIdentities by netId.</summary>
        public static Dictionary<uint, NetworkIdentity> spawned => NetClient.instance.spawned;

        /// <summary>Client's NetworkConnection to server.</summary>
        public static NetworkConnection connection
        {
            get => NetClient.instance.connection;
            internal set => NetClient.instance.connection = value;
        }

        /// <summary>True if client is ready (= joined world).</summary>
        public static bool ready
        {
            get => NetClient.instance.ready;
            set => NetClient.instance.ready = value;
        }

        /// <summary>NetworkIdentity of the localPlayer </summary>
        public static NetworkIdentity localPlayer
        {
            get => NetClient.instance.localPlayer;
            internal set => NetClient.instance.localPlayer = value;
        }

        // NetworkClient state
        internal static ConnectState connectState
        {
            get => NetClient.instance.connectState;
            set => NetClient.instance.connectState = value;
        }

        /// <summary>IP address of the connection to server.</summary>
        public static string serverIp => NetClient.instance.serverIp;

        /// <summary>active is true while a client is connecting/connected</summary>
        // (= while the network is active)
        public static bool active => NetClient.instance.active;

        /// <summary>Check if client is connecting (before connected).</summary>
        public static bool isConnecting => NetClient.instance.isConnecting;

        /// <summary>Check if client is connected (after connecting).</summary>
        public static bool isConnected => NetClient.instance.isConnected;

        /// <summary>True if client is running in host mode.</summary>
        public static bool isHostClient => NetClient.instance.isHostClient;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action                         OnConnectedEvent    => NetClient.instance.OnConnectedEvent;
        public static Action                         OnDisconnectedEvent => NetClient.instance.OnDisconnectedEvent;
        public static Action<TransportError, string> OnErrorEvent        => NetClient.instance.OnErrorEvent;

        /// <summary>Registered spawnable prefabs by assetId.</summary>
        public static Dictionary<uint, GameObject> prefabs => NetClient.instance.prefabs;

        // custom spawn / unspawn handlers by assetId.
        // useful to support prefab pooling etc.:
        // https://mirror-networking.gitbook.io/docs/guides/gameobjects/custom-spawnfunctions
        internal static Dictionary<uint, SpawnHandlerDelegate> spawnHandlers => NetClient.instance.spawnHandlers;
        internal static Dictionary<uint, UnSpawnDelegate> unspawnHandlers    => NetClient.instance.unspawnHandlers;

        // spawning
        internal static bool isSpawnFinished
        {
            get => NetClient.instance.isSpawnFinished;
            set => NetClient.instance.isSpawnFinished = value;
        }

        // Disabled scene objects that can be spawned again, by sceneId.
        internal static Dictionary<ulong, NetworkIdentity> spawnableObjects => NetClient.instance.spawnableObjects;

        // interest management component (optional)
        // only needed for SetHostVisibility
        public static InterestManagement aoi
        {
            get => NetClient.instance.aoi;
            set => NetClient.instance.aoi = value;
        }

        // scene loading
        public static bool isLoadingScene
        {
            get => NetClient.instance.isLoadingScene;
            set => NetClient.instance.isLoadingScene = value;
        }

        // initialization //////////////////////////////////////////////////////
        internal static void RegisterSystemHandlers(bool hostMode) => NetClient.instance.RegisterSystemHandlers(hostMode);

        // connect /////////////////////////////////////////////////////////////
        /// <summary>Connect client to a NetworkServer by address.</summary>
        public static void Connect(string address) => NetClient.instance.Connect(address);

        /// <summary>Connect client to a NetworkServer by Uri.</summary>
        public static void Connect(Uri uri) => NetClient.instance.Connect(uri);

        public static void ConnectHost() => NetClient.instance.ConnectHost();

        /// <summary>Connect host mode</summary>
        public static void ConnectLocalServer() => NetClient.instance.ConnectLocalServer();

        // disconnect //////////////////////////////////////////////////////////
        /// <summary>Disconnect from server.</summary>
        public static void Disconnect() => NetClient.instance.Disconnect();

        internal static void OnTransportData(ArraySegment<byte> data, int channelId) => NetClient.instance.OnTransportData(data, channelId);
        internal static void OnTransportDisconnected() => NetClient.instance.OnTransportDisconnected();

        // send ////////////////////////////////////////////////////////////////
        /// <summary>Send a NetworkMessage to the server over the given channel.</summary>
        public static void Send<T>(T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
            => NetClient.instance.Send(message, channelId);

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
            => NetClient.instance.RegisterHandler(handler, requireAuthentication);

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // RegisterHandler throws a warning (as it should) if a handler is assigned twice
        // Use of ReplaceHandler makes it clear the user intended to replace the handler
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
            => NetClient.instance.ReplaceHandler(handler, requireAuthentication);

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // RegisterHandler throws a warning (as it should) if a handler is assigned twice
        // Use of ReplaceHandler makes it clear the user intended to replace the handler
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
            => NetClient.instance.ReplaceHandler(handler, requireAuthentication);

        /// <summary>Unregister a message handler of type T.</summary>
        public static bool UnregisterHandler<T>()
            where T : struct, NetworkMessage
            => NetClient.instance.UnregisterHandler<T>();

        // spawnable prefabs ///////////////////////////////////////////////////
        /// <summary>Find the registered prefab for this asset id.</summary>
        public static bool GetPrefab(uint assetId, out GameObject prefab) => NetClient.instance.GetPrefab(assetId, out prefab);

        /// <summary>Register spawnable prefab with custom assetId.</summary>
        public static void RegisterPrefab(GameObject prefab, uint newAssetId) => NetClient.instance.RegisterPrefab(prefab, newAssetId);

        /// <summary>Register spawnable prefab.</summary>
        public static void RegisterPrefab(GameObject prefab) => NetClient.instance.RegisterPrefab(prefab);

        /// <summary>Register a spawnable prefab with custom assetId and custom spawn/unspawn handlers.</summary>
        public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        /// <summary>Register a spawnable prefab with custom spawn/unspawn handlers.</summary>
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        /// <summary>Register a spawnable prefab with custom assetId and custom spawn/unspawn handlers.</summary>
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        /// <summary>Register a spawnable prefab with custom spawn/unspawn handlers.</summary>
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        /// <summary>Removes a registered spawn prefab that was setup with NetworkClient.RegisterPrefab.</summary>
        public static void UnregisterPrefab(GameObject prefab) => NetClient.instance.UnregisterPrefab(prefab);

        // spawn handlers //////////////////////////////////////////////////////
        /// <summary>This is an advanced spawning function that registers a custom assetId with the spawning system.</summary>
        public static void RegisterSpawnHandler(uint assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        /// <summary>This is an advanced spawning function that registers a custom assetId with the spawning system.</summary>
        public static void RegisterSpawnHandler(uint assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) => NetClient.instance.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        /// <summary> Removes a registered spawn handler function that was registered with NetworkClient.RegisterHandler().</summary>
        public static void UnregisterSpawnHandler(uint assetId) => NetClient.instance.UnregisterSpawnHandler(assetId);

        /// <summary>This clears the registered spawn prefabs and spawn handler functions for this client.</summary>
        public static void ClearSpawners() => NetClient.instance.ClearSpawners();

        internal static bool InvokeUnSpawnHandler(uint assetId, GameObject obj) => NetClient.instance.InvokeUnSpawnHandler(assetId, obj);

        // ready ///////////////////////////////////////////////////////////////
        /// <summary>Sends Ready message to server, indicating that we loaded the scene, ready to enter the game.</summary>
        public static bool Ready() => NetClient.instance.Ready();

        // add player //////////////////////////////////////////////////////////
        // called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity identity) => NetClient.instance.InternalAddPlayer(identity);

        /// <summary>Sends AddPlayer message to the server, indicating that we want to join the world.</summary>
        public static bool AddPlayer() => NetClient.instance.AddPlayer();

        // spawning ////////////////////////////////////////////////////////////
        internal static void ApplySpawnPayload(NetworkIdentity identity, SpawnMessage message) => NetClient.instance.ApplySpawnPayload(identity, message);

        // Finds Existing Object with NetId or spawns a new one using AssetId or sceneId
        internal static bool FindOrSpawnObject(SpawnMessage message, out NetworkIdentity identity) => NetClient.instance.FindOrSpawnObject(message, out identity);

        /// <summary>Call this after loading/unloading a scene in the client after connection to register the spawnable objects</summary>
        public static void PrepareToSpawnSceneObjects() => NetClient.instance.PrepareToSpawnSceneObjects();

        internal static void OnObjectSpawnStarted(ObjectSpawnStartedMessage _)   => NetClient.instance.OnObjectSpawnStarted(_);
        internal static void OnObjectSpawnFinished(ObjectSpawnFinishedMessage _) => NetClient.instance.OnObjectSpawnFinished(_);

        // host mode callbacks /////////////////////////////////////////////////
        internal static void OnHostClientSpawn(SpawnMessage message) => NetClient.instance.OnHostClientSpawn(message);

        // client-only mode callbacks //////////////////////////////////////////
        internal static void OnObjectDestroy(ObjectDestroyMessage message) => NetClient.instance.OnObjectDestroy(message);
        internal static void OnSpawn(SpawnMessage message)                 => NetClient.instance.OnSpawn(message);
        internal static void OnChangeOwner(ChangeOwnerMessage message)                         => NetClient.instance.OnChangeOwner(message);
        internal static void ChangeOwner(NetworkIdentity identity, ChangeOwnerMessage message) => NetClient.instance.ChangeOwner(identity, message);

        internal static void CheckForLocalPlayer(NetworkIdentity identity) => NetClient.instance.CheckForLocalPlayer(identity);

        // update //////////////////////////////////////////////////////////////
        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate() => NetClient.instance?.NetworkEarlyUpdate();

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkLateUpdate() => NetClient.instance?.NetworkLateUpdate();

        // shutdown ////////////////////////////////////////////////////////////
        /// <summary>Destroys all networked objects on the client.</summary>
        // Note: NetworkServer.CleanupNetworkIdentities does the same on server.
        public static void DestroyAllClientObjects() => NetClient.instance.DestroyAllClientObjects();

        /// <summary>Shutdown the client.</summary>
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Shutdown() => NetClient.instance?.Shutdown();
    }
}
