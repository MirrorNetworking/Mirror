// NetworkServer used to be a static class.
// keep it for now, and simply point it to the singleton NetworkServer component.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>NetworkServer handles remote connections and has a local connection for a local client.</summary>
    public static class NetworkServer
    {
        public static int maxConnections
        {
            get => NetworkServerComponent.singleton.maxConnections;
            set => NetworkServerComponent.singleton.maxConnections = value;
        }

        /// <summary>Connection to host mode client (if any)</summary>
        public static NetworkConnectionToClient localConnection
        {
            get => NetworkServerComponent.singleton.localConnection;
            private set => NetworkServerComponent.singleton.localConnection = value;
        }

        /// <summary>True is a local client is currently active on the server</summary>
        public static bool localClientActive => NetworkServerComponent.singleton.localClientActive;

        /// <summary>Dictionary of all server connections, with connectionId as key</summary>
        public static Dictionary<int, NetworkConnectionToClient> connections => NetworkServerComponent.singleton.connections;

        /// <summary>Message Handlers dictionary, with mesageId as key</summary>
        internal static Dictionary<ushort, NetworkMessageDelegate> handlers => NetworkServerComponent.singleton.handlers;

        /// <summary>All spawned NetworkIdentities by netId.</summary>
        // server sees ALL spawned ones.
        public static Dictionary<uint, NetworkIdentity> spawned => NetworkServerComponent.singleton.spawned;

        /// <summary>Single player mode can use dontListen to not accept incoming connections</summary>
        // see also: https://github.com/vis2k/Mirror/pull/2595
        public static bool dontListen
        {
            get => NetworkServerComponent.singleton.dontListen;
            set => NetworkServerComponent.singleton.dontListen = value;
        }

        /// <summary>active checks if the server has been started</summary>
        public static bool active
        {
            get => NetworkServerComponent.singleton.active;
            internal set => NetworkServerComponent.singleton.active = value;
        }

        // scene loading
        public static bool isLoadingScene
        {
            get => NetworkServerComponent.singleton.isLoadingScene;
            set => NetworkServerComponent.singleton.isLoadingScene = value;
        }

        // interest management component (optional)
        // by default, everyone observes everyone
        public static InterestManagement aoi
        {
            get => NetworkServerComponent.singleton.aoi;
            set => NetworkServerComponent.singleton.aoi = value;
        }

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action<NetworkConnection> OnConnectedEvent
        {
            get => NetworkServerComponent.singleton.OnConnectedEvent;
            set => NetworkServerComponent.singleton.OnConnectedEvent = value;
        }
        public static Action<NetworkConnection> OnDisconnectedEvent
        {
            get => NetworkServerComponent.singleton.OnDisconnectedEvent;
            set => NetworkServerComponent.singleton.OnDisconnectedEvent = value;
        }
        public static Action<NetworkConnection, Exception> OnErrorEvent
        {
            get => NetworkServerComponent.singleton.OnErrorEvent;
            set => NetworkServerComponent.singleton.OnErrorEvent = value;
        }

        // initialization / shutdown ///////////////////////////////////////////
        // calls OnStartClient for all SERVER objects in host mode once.
        // client doesn't get spawn messages for those, so need to call manually.
        public static void ActivateHostScene() => NetworkServerComponent.singleton.ActivateHostScene();

        internal static void RegisterMessageHandlers() => NetworkServerComponent.singleton.RegisterMessageHandlers();

        /// <summary>Starts server and listens to incoming connections with max connections limit.</summary>
        public static void Listen(int maxConns) => NetworkServerComponent.singleton.Listen(maxConns);

        /// <summary>Shuts down the server and disconnects all clients</summary>
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Shutdown()
        {
            if (NetworkServerComponent.singleton != null)
                NetworkServerComponent.singleton.Shutdown();
        }

        // connections /////////////////////////////////////////////////////////
        /// <summary>Add a connection and setup callbacks. Returns true if not added yet.</summary>
        public static bool AddConnection(NetworkConnectionToClient conn) => NetworkServerComponent.singleton.AddConnection(conn);

        /// <summary>Removes a connection by connectionId. Returns true if removed.</summary>
        public static bool RemoveConnection(int connectionId) => NetworkServerComponent.singleton.RemoveConnection(connectionId);

        // called by LocalClient to add itself. don't call directly.
        // TODO consider internal setter instead?
        internal static void SetLocalConnection(LocalConnectionToClient conn) => NetworkServerComponent.singleton.SetLocalConnection(conn);

        // removes local connection to client
        internal static void RemoveLocalConnection() => NetworkServerComponent.singleton.RemoveLocalConnection();

        /// <summary>True if we have no external connections (host is allowed)</summary>
        public static bool NoExternalConnections() => NetworkServerComponent.singleton.NoExternalConnections();

        // send ////////////////////////////////////////////////////////////////
        /// <summary>Send a message to all clients, even those that haven't joined the world yet (non ready)</summary>
        public static void SendToAll<T>(T message, int channelId = Channels.Reliable, bool sendToReadyOnly = false)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.SendToAll(message, channelId, sendToReadyOnly);

        /// <summary>Send a message to all clients which have joined the world (are ready).</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReady<T>(T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.SendToReady(message, channelId);

        /// <summary>Send a message to only clients which are ready with option to include the owner of the object identity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.SendToReadyObservers(identity, message, includeOwner, channelId);

        // Deprecated 2021-09-19
        [Obsolete("SendToReady(identity, message, ...) was renamed to SendToReadyObservers because that's what it does.")]
        public static void SendToReady<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage =>
                SendToReadyObservers(identity, message, includeOwner, channelId);

        /// <summary>Send a message to only clients which are ready including the owner of the NetworkIdentity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage =>
                SendToReadyObservers(identity, message, true, channelId);

        // Deprecated 2021-09-19
        [Obsolete("SendToReady(identity, message, ...) was renamed to SendToReadyObservers because that's what it does.")]
        public static void SendToReady<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage =>
                SendToReadyObservers(identity, message, channelId);

        // transport events ////////////////////////////////////////////////////
        internal static void OnConnected(NetworkConnectionToClient conn) => NetworkServerComponent.singleton.OnConnected(conn);
        internal static void OnTransportData(int connectionId, ArraySegment<byte> data, int channelId) => NetworkServerComponent.singleton.OnTransportData(connectionId, data, channelId);
        internal static void OnTransportDisconnected(int connectionId) => NetworkServerComponent.singleton.OnTransportDisconnected(connectionId);

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.RegisterHandler(handler, requireAuthentication);

        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<NetworkConnection, T, int> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.RegisterHandler(handler, requireAuthentication);

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.ReplaceHandler(handler, requireAuthentication);

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.ReplaceHandler(handler, requireAuthentication);

        /// <summary>Unregister a handler for a message type T.</summary>
        public static void UnregisterHandler<T>()
            where T : struct, NetworkMessage => NetworkServerComponent.singleton.UnregisterHandler<T>();

        /// <summary>Clears all registered message handlers.</summary>
        public static void ClearHandlers() => NetworkServerComponent.singleton.ClearHandlers();

        internal static bool GetNetworkIdentity(GameObject go, out NetworkIdentity identity) => NetworkServerComponent.singleton.GetNetworkIdentity(go, out identity);

        // disconnect //////////////////////////////////////////////////////////
        /// <summary>Disconnect all connections, including the local connection.</summary>
        public static void DisconnectAll() => NetworkServerComponent.singleton.DisconnectAll();

        // add/remove/replace player ///////////////////////////////////////////
        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player) => NetworkServerComponent.singleton.AddPlayerForConnection(conn, player);

        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId) => NetworkServerComponent.singleton.AddPlayerForConnection(conn, player, assetId);

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, bool keepAuthority = false) => NetworkServerComponent.singleton.ReplacePlayerForConnection(conn, player, keepAuthority);

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId, bool keepAuthority = false) => NetworkServerComponent.singleton.ReplacePlayerForConnection(conn, player, assetId, keepAuthority);

        // ready ///////////////////////////////////////////////////////////////
        /// <summary>Flags client connection as ready (=joined world).</summary>
        public static void SetClientReady(NetworkConnection conn) => NetworkServerComponent.singleton.SetClientReady(conn);

        /// <summary>Marks the client of the connection to be not-ready.</summary>
        public static void SetClientNotReady(NetworkConnection conn) => NetworkServerComponent.singleton.SetClientNotReady(conn);

        /// <summary>Marks all connected clients as no longer ready.</summary>
        public static void SetAllClientsNotReady() => NetworkServerComponent.singleton.SetAllClientsNotReady();

        // show / hide for connection //////////////////////////////////////////
        internal static void ShowForConnection(NetworkIdentity identity, NetworkConnection conn) => NetworkServerComponent.singleton.ShowForConnection(identity, conn);
        internal static void HideForConnection(NetworkIdentity identity, NetworkConnection conn) => NetworkServerComponent.singleton.HideForConnection(identity, conn);

        /// <summary>Removes the player object from the connection</summary>
        public static void RemovePlayerForConnection(NetworkConnection conn, bool destroyServerObject) => NetworkServerComponent.singleton.RemovePlayerForConnection(conn, destroyServerObject);

        // remote calls ////////////////////////////////////////////////////////
        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn) => NetworkServerComponent.singleton.SendSpawnMessage(identity, conn);
        internal static void SendChangeOwnerMessage(NetworkIdentity identity, NetworkConnection conn) => NetworkServerComponent.singleton.SendChangeOwnerMessage(identity, conn);

        /// <summary>Spawn the given game object on all clients which are ready.</summary>
        public static void Spawn(GameObject obj, NetworkConnection ownerConnection = null) => NetworkServerComponent.singleton.Spawn(obj, ownerConnection);

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        public static void Spawn(GameObject obj, GameObject ownerPlayer) => NetworkServerComponent.singleton.Spawn(obj, ownerPlayer);

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        public static void Spawn(GameObject obj, Guid assetId, NetworkConnection ownerConnection = null) => NetworkServerComponent.singleton.Spawn(obj, assetId, ownerConnection);

        internal static bool ValidateSceneObject(NetworkIdentity identity) => NetworkServerComponent.singleton.ValidateSceneObject(identity);

        /// <summary>Spawns NetworkIdentities in the scene on the server.</summary>
        public static bool SpawnObjects() => NetworkServerComponent.singleton.SpawnObjects();

        /// <summary>This takes an object that has been spawned and un-spawns it.</summary>
        public static void UnSpawn(GameObject obj) => NetworkServerComponent.singleton.UnSpawn(obj);

        // destroy /////////////////////////////////////////////////////////////
        /// <summary>Destroys all of the connection's owned objects on the server.</summary>
        public static void DestroyPlayerForConnection(NetworkConnection conn) => NetworkServerComponent.singleton.DestroyPlayerForConnection(conn);

        /// <summary>Destroys this object and corresponding objects on all clients.</summary>
        public static void Destroy(GameObject obj) => NetworkServerComponent.singleton.Destroy(obj);

        // interest management /////////////////////////////////////////////////
        internal static void AddAllReadyServerConnectionsToObservers(NetworkIdentity identity) => NetworkServerComponent.singleton.AddAllReadyServerConnectionsToObservers(identity);
        internal static HashSet<NetworkConnection> newObservers => NetworkServerComponent.singleton.newObservers;
        public static void RebuildObservers(NetworkIdentity identity, bool initialize) => NetworkServerComponent.singleton.RebuildObservers(identity, initialize);

        // update //////////////////////////////////////////////////////////////
        internal static void NetworkEarlyUpdate() => NetworkServerComponent.singleton.NetworkEarlyUpdate();
        internal static void NetworkLateUpdate() => NetworkServerComponent.singleton.NetworkLateUpdate();
    }
}
