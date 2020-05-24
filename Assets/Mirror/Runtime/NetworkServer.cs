using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{

    /// <summary>
    /// The NetworkServer.
    /// </summary>
    /// <remarks>
    /// <para>NetworkServer handles remote connections from remote clients via a NetworkServerSimple instance, and also has a local connection for a local client.</para>
    /// <para>The NetworkManager uses the NetworkServer, but it can be used without the NetworkManager.</para>
    /// <para>The set of networked objects that have been spawned is managed by NetworkServer. Objects are spawned with NetworkServer.Spawn() which adds them to this set, and makes them be created on clients. Spawned objects are removed automatically when they are destroyed, or than they can be removed from the spawned set by calling NetworkServer.UnSpawn() - this does not destroy the object.</para>
    /// <para>There are a number of internal messages used by NetworkServer, these are setup when NetworkServer.Listen() is called.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetworkServer : MonoBehaviour, INetworkServer
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkServer));

        bool initialized;

        [Serializable] public class NetworkConnectionEvent : UnityEvent<INetworkConnection> { }
        [Serializable] public class NetworkSceneEvent : UnityEvent<string> { }

        /// <summary>
        /// The maximum number of concurrent network connections to support.
        /// <para>This effects the memory usage of the network layer.</para>
        /// </summary>
        [Tooltip("Maximum number of concurrent connections.")]
        [Min(1)]
        public int MaxConnections = 4;

        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// </summary>
        public UnityEvent Started = new UnityEvent();

        /// <summary>
        /// Event fires once a new Client has connect to the Server.
        /// </summary>
        public NetworkConnectionEvent Connected = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires once a new Client has passed Authentication to the Server.
        /// </summary>
        public NetworkConnectionEvent Authenticated = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires before Server changes scene.
        /// </summary>
        public NetworkSceneEvent ServerChangeScene = new NetworkSceneEvent();

        /// <summary>
        /// Event fires after Server has completed scene change.
        /// </summary>
        public NetworkSceneEvent ServerSceneChanged = new NetworkSceneEvent();

        /// <summary>
        /// Event fires once a Client has Disconnected from the Server.
        /// </summary>
        public NetworkConnectionEvent Disconnected = new NetworkConnectionEvent();

        public UnityEvent Stopped = new UnityEvent();

        [Header("Authentication")]
        [Tooltip("Authentication component attached to this object")]
        public NetworkAuthenticator authenticator;

        /// <summary>
        /// The connection to the host mode client (if any).
        /// </summary>
        // original HLAPI has .localConnections list with only m_LocalConnection in it
        // (for backwards compatibility because they removed the real localConnections list a while ago)
        // => removed it for easier code. use .localConnection now!
        public INetworkConnection LocalConnection { get; private set; }

        // The host client for this server 
        public NetworkClient LocalClient { get; private set; }

        /// <summary>
        /// True if there is a local client connected to this server (host mode)
        /// </summary>
        public bool LocalClientActive => LocalClient != null && LocalClient.Active;

        /// <summary>
        /// Number of active player objects across all connections on the server.
        /// <para>This is only valid on the host / server.</para>
        /// </summary>
        public int NumPlayers => connections.Count(kv => kv.Identity != null);

        /// <summary>
        /// A list of local connections on the server.
        /// </summary>
        public readonly HashSet<INetworkConnection> connections = new HashSet<INetworkConnection>();

        /// <summary>
        /// <para>If you enable this, the server will not listen for incoming connections on the regular network port.</para>
        /// <para>This can be used if the game is running in host mode and does not want external players to be able to connect - making it like a single-player game. Also this can be useful when using AddExternalConnection().</para>
        /// </summary>
        public bool Listening = true;

        /// <summary>
        /// <para>Checks if the server has been started.</para>
        /// <para>This will be true after NetworkServer.Listen() has been called.</para>
        /// </summary>
        public bool Active { get; private set; }

        public readonly Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();

        // just a cached memory area where we can collect connections
        // for broadcasting messages
        private static readonly List<INetworkConnection> connectionsCache = new List<INetworkConnection>();

        // Time kept in this server
        public readonly NetworkTime Time = new NetworkTime();

        // transport to use to accept connections
        public AsyncTransport transport;

        /// <summary>
        /// This shuts down the server and disconnects all clients.
        /// </summary>
        public void Disconnect()
        {
            foreach (INetworkConnection conn in connections)
            {
                conn.Disconnect();
            }
            if (transport != null)
                transport.Disconnect();
        }

        void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            if (logger.LogEnabled()) logger.Log("NetworkServer Created version " + Version.Current);

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();

            if (transport == null)
                transport = GetComponent<AsyncTransport>();

            if (authenticator != null)
            {
                authenticator.OnServerAuthenticated += OnAuthenticated;

                Connected.AddListener(authenticator.OnServerAuthenticateInternal);
            }
            else
            {
                // if no authenticator, consider every connection as authenticated
                Connected.AddListener(OnAuthenticated);
            }
        }

        internal void RegisterMessageHandlers(INetworkConnection connection)
        {
            connection.RegisterHandler<ReadyMessage>(OnClientReadyMessage);
            connection.RegisterHandler<CommandMessage>(OnCommandMessage);
            connection.RegisterHandler<RemovePlayerMessage>(OnRemovePlayerMessage);
        }

        /// <summary>
        /// Start the server, setting the maximum number of connections.
        /// </summary>
        /// <param name="maxConns">Maximum number of allowed connections</param>
        /// <returns></returns>
        public async Task ListenAsync()
        {
            Initialize();

            // only start server if we want to listen
            if (Listening)
            {
                await transport.ListenAsync();
                logger.Log("Server started listening");
            }

            Active = true;

            // call OnStartServer AFTER Listen, so that NetworkServer.active is
            // true and we can call NetworkServer.Spawn in OnStartServer
            // overrides.
            // (useful for loading & spawning stuff from database etc.)
            //
            // note: there is no risk of someone connecting after Listen() and
            //       before OnStartServer() because this all runs in one thread
            //       and we don't start processing connects until Update.
            Started.Invoke();

            _ = AcceptAsync();
        }

        // accept connections from clients
        private async Task AcceptAsync()
        {
            try
            {
                IConnection connection;

                while ((connection = await transport.AcceptAsync()) != null)
                {
                    var networkConnectionToClient = new NetworkConnection(connection);

                    _ = ConnectionAcceptedAsync(networkConnectionToClient);
                }

                Cleanup();
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
        }

        // cleanup resources so that we can start again
        private void Cleanup()
        {

            if (authenticator != null)
            {
                authenticator.OnServerAuthenticated -= OnAuthenticated;
                Connected.RemoveListener(authenticator.OnServerAuthenticateInternal);
            }
            else
            {
                // if no authenticator, consider every connection as authenticated
                Connected.RemoveListener(OnAuthenticated);
            }

            Stopped.Invoke();
            initialized = false;
            Active = false;
        }

        /// <summary>
        /// <para>This accepts a network connection and adds it to the server.</para>
        /// <para>This connection will use the callbacks registered with the server.</para>
        /// </summary>
        /// <param name="conn">Network connection to add.</param>
        public void AddConnection(INetworkConnection conn)
        {
            if (!connections.Contains(conn))
            {
                // connection cannot be null here or conn.connectionId
                // would throw NRE
                connections.Add(conn);
                conn.RegisterHandler<NetworkPingMessage>(Time.OnServerPing);
            }
        }

        /// <summary>
        /// This removes an external connection added with AddExternalConnection().
        /// </summary>
        /// <param name="connectionId">The id of the connection to remove.</param>
        public void RemoveConnection(INetworkConnection conn)
        {
            connections.Remove(conn);
        }

        // called by LocalClient to add itself. dont call directly.
        internal void SetLocalConnection(NetworkClient client, IConnection tconn)
        {
            if (LocalConnection != null)
            {
                throw new InvalidOperationException("Local Connection already exists");
            }

            var conn = new NetworkConnection(tconn);
            LocalConnection = conn;
            LocalClient = client;

            _ = ConnectionAcceptedAsync(conn);

        }

        internal void ActivateHostScene()
        {
            foreach (NetworkIdentity identity in spawned.Values)
            {
                if (!identity.IsClient)
                {
                    if (logger.LogEnabled()) logger.Log("ActivateHostScene " + identity.NetId + " " + identity);

                    identity.StartClient();
                }
            }
        }

        /// <summary>
        /// Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        internal void OnServerChangeScene(string newSceneName)
        {
            ServerChangeScene.Invoke(newSceneName);
        }

        /// <summary>
        /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
        /// </summary>
        /// <param name="sceneName">The name of the new scene.</param>
        internal void OnServerSceneChanged(string sceneName)
        {
            ServerSceneChanged.Invoke(sceneName);
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        void SendToObservers<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (logger.LogEnabled()) logger.Log("Server.SendToObservers id:" + typeof(T));

            if (identity.observers.Count > 0)
                NetworkConnection.Send(identity.observers, msg, channelId);
        }

        /// <summary>
        /// Send a message to all connected clients, both ready and not-ready.
        /// <para>See <see cref="NetworkConnection.IsReady"/></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="msg">Message</param>
        /// <param name="channelId">Transport channel to use</param>
        /// <param name="sendToReadyOnly">Indicates if only ready clients should receive the message</param>
        public void SendToAll<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (logger.LogEnabled()) logger.Log("Server.SendToAll id:" + typeof(T));
            NetworkConnection.Send(connections, msg, channelId);
        }

        /// <summary>
        /// Send a message to only clients which are ready with option to include the owner of the object identity.
        /// <para>See <see cref="NetworkConnection.IsReady"/></para>
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="identity">Identity of the owner</param>
        /// <param name="msg">Message</param>
        /// <param name="includeOwner">Should the owner of the object be included</param>
        /// <param name="channelId">Transport channel to use</param>
        public void SendToReady<T>(NetworkIdentity identity, T msg, bool includeOwner = true, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (logger.LogEnabled()) logger.Log("Server.SendToReady msgType:" + typeof(T));

            connectionsCache.Clear();

            foreach (INetworkConnection connection in identity.observers)
            {

                bool isOwner = connection == identity.ConnectionToClient;
                if ((!isOwner || includeOwner) && connection.IsReady)
                {
                    connectionsCache.Add(connection);
                }
            }

            NetworkConnection.Send(connectionsCache, msg, channelId);
        }

        /// <summary>
        /// Send a message to only clients which are ready including the owner of the object identity.
        /// <para>See <see cref="NetworkConnection.IsReady"/></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="identity">identity of the object</param>
        /// <param name="msg">Message</param>
        /// <param name="channelId">Transport channel to use</param>
        public void SendToReady<T>(NetworkIdentity identity, T msg, int channelId) where T : IMessageBase
        {
            SendToReady(identity, msg, true, channelId);
        }

        // The user should never need to pump the update loop manually
        internal void Update()
        {
            if (!Active)
                return;

            // update all server objects
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in spawned)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    kvp.Value.ServerUpdate();
                }
                else
                {
                    // spawned list should have no null entries because we
                    // always call Remove in OnObjectDestroy everywhere.
                    logger.LogWarning("Found 'null' entry in spawned list for netId=" + kvp.Key + ". Please call NetworkServer.Destroy to destroy networked objects. Don't use GameObject.Destroy.");
                }
            }
        }

        async Task ConnectionAcceptedAsync(INetworkConnection conn)
        {
            if (logger.LogEnabled()) logger.Log("Server accepted client:" + conn);

            // are more connections allowed? if not, kick
            // (it's easier to handle this in Mirror, so Transports can have
            //  less code and third party transport might not do that anyway)
            // (this way we could also send a custom 'tooFull' message later,
            //  Transport can't do that)
            if (connections.Count >= MaxConnections)
            {
                conn.Disconnect();
                if (logger.LogEnabled()) logger.Log("Server full, kicked client:" + conn);
                return;
            }

            // add connection
            AddConnection(conn);

            // let everyone know we just accepted a connection
            Connected.Invoke(conn);

            // now process messages until the connection closes

            try
            {
                await conn.ProcessMessagesAsync();
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
            finally
            {
                OnDisconnected(conn);
            }
        }

        //called once a client disconnects from the server
        void OnDisconnected(INetworkConnection connection)
        {
            if (logger.LogEnabled()) logger.Log("Server disconnect client:" + connection);

            RemoveConnection(connection);

            Disconnected.Invoke(connection);

            connection.DestroyOwnedObjects();
            connection.Identity = null;

            if (connection == LocalConnection)
                LocalConnection = null;
        }

        internal void OnAuthenticated(INetworkConnection conn)
        {
            if (logger.LogEnabled()) logger.Log("Server authenticate client:" + conn);

            // connection has been authenticated,  now we can handle other messages
            RegisterMessageHandlers(conn);

            Authenticated?.Invoke(conn);
        }

        /// <summary>
        /// server that received the message
        /// </summary>
        /// <remarks>This is a hack, but it is needed to deserialize
        /// gameobjects when processing the message</remarks>
        /// 
        internal static NetworkServer Current;

        /// <summary>
        /// send this message to the player only
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="identity"></param>
        /// <param name="msg"></param>
        public void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (identity != null)
            {
                identity.ConnectionToClient.Send(msg, channelId);
            }
            else
            {
                throw new InvalidOperationException("SendToClientOfPlayer: player has no NetworkIdentity: " + identity);
            }
        }

        /// <summary>
        /// This replaces the player object for a connection with a different player object. The old player object is not destroyed.
        /// <para>If a connection already has a player object, this can be used to replace that object with a different player object. This does NOT change the ready state of the connection, so it can safely be used while changing scenes.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="client">Client associated to the player.</param> 
        /// <param name="player">Player object spawned for the player.</param>
        /// <param name="assetId"></param>
        /// <param name="keepAuthority">Does the previous player remain attached to this connection?</param>
        /// <returns></returns>
        public bool ReplacePlayerForConnection(INetworkConnection conn, NetworkClient client, GameObject player, Guid assetId, bool keepAuthority = false)
        {
            NetworkIdentity identity = GetNetworkIdentity(player);
            identity.AssetId = assetId;
            return InternalReplacePlayerForConnection(conn, client, player, keepAuthority);
        }

        /// <summary>
        /// This replaces the player object for a connection with a different player object. The old player object is not destroyed.
        /// <para>If a connection already has a player object, this can be used to replace that object with a different player object. This does NOT change the ready state of the connection, so it can safely be used while changing scenes.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="client">Client associated to the player.</param> 
        /// <param name="player">Player object spawned for the player.</param>
        /// <param name="keepAuthority">Does the previous player remain attached to this connection?</param>
        /// <returns></returns>
        public bool ReplacePlayerForConnection(INetworkConnection conn, NetworkClient client, GameObject player, bool keepAuthority = false)
        {
            return InternalReplacePlayerForConnection(conn, client, player, keepAuthority);
        }

        void SpawnObserversForConnection(INetworkConnection conn)
        {
            if (logger.LogEnabled()) logger.Log("Spawning " + spawned.Count + " objects for conn " + conn);

            if (!conn.IsReady)
            {
                // client needs to finish initializing before we can spawn objects
                // otherwise it would not find them.
                return;
            }

            // let connection know that we are about to start spawning...
            conn.Send(new ObjectSpawnStartedMessage());

            // add connection to each nearby NetworkIdentity's observers, which
            // internally sends a spawn message for each one to the connection.
            foreach (NetworkIdentity identity in spawned.Values)
            {
                // try with far away ones in ummorpg!
                //TODO this is different
                if (identity.gameObject.activeSelf)
                {
                    if (logger.LogEnabled()) logger.Log("Sending spawn message for current server objects name='" + identity.name + "' netId=" + identity.NetId + " sceneId=" + identity.sceneId);

                    bool visible = identity.OnCheckObserver(conn);
                    if (visible)
                    {
                        identity.AddObserver(conn);
                    }
                }
            }

            // let connection know that we finished spawning, so it can call
            // OnStartClient on each one (only after all were spawned, which
            // is how Unity's Start() function works too)
            conn.Send(new ObjectSpawnFinishedMessage());
        }

        /// <summary>
        /// <para>When an AddPlayer message handler has received a request from a player, the server calls this to associate the player object with the connection.</para>
        /// <para>When a player is added for a connection, the client for that connection is made ready automatically. The player object is automatically spawned, so you do not need to call NetworkServer.Spawn for that object. This function is used for "adding" a player, not for "replacing" the player on a connection. If there is already a player on this playerControllerId for this connection, this will fail.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="client">Client associated to the player.</param> 
        /// <param name="player">Player object spawned for the player.</param>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public bool AddPlayerForConnection(INetworkConnection conn, GameObject player, Guid assetId)
        {
            NetworkIdentity identity = GetNetworkIdentity(player);
            identity.AssetId = assetId;
            return AddPlayerForConnection(conn, player);
        }

        /// <summary>
        /// <para>When an AddPlayer message handler has received a request from a player, the server calls this to associate the player object with the connection.</para>
        /// <para>When a player is added for a connection, the client for that connection is made ready automatically. The player object is automatically spawned, so you do not need to call NetworkServer.Spawn for that object. This function is used for "adding" a player, not for "replacing" the player on a connection. If there is already a player on this playerControllerId for this connection, this will fail.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="client">Client associated to the player.</param>
        /// <param name="player">Player object spawned for the player.</param>
        /// <returns></returns>
        public bool AddPlayerForConnection(INetworkConnection conn, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.Log("AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }

            // cannot have a player object in "Add" version
            if (conn.Identity != null)
            {
                logger.Log("AddPlayer: player object already exists");
                return false;
            }

            // make sure we have a controller before we call SetClientReady
            // because the observers will be rebuilt only if we have a controller
            conn.Identity = identity;

            // set server to the NetworkIdentity
            identity.Server = this;

            identity.Client = this.LocalClient;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.SetClientOwner(conn);

            // special case,  we are in host mode,  set hasAuthority to true so that all overrides see it
            if (conn == LocalConnection)
            {
                identity.HasAuthority = true;
                LocalClient.Connection.Identity = identity;
            }

            // set ready if not set yet
            SetClientReady(conn);

            if (logger.LogEnabled()) logger.Log("Adding new playerGameObject object netId: " + identity.NetId + " asset ID " + identity.AssetId);

            Respawn(identity);
            return true;
        }

        void Respawn(NetworkIdentity identity)
        {
            if (identity.NetId == 0)
            {
                // If the object has not been spawned, then do a full spawn and update observers
                Spawn(identity.gameObject, identity.ConnectionToClient);
            }
            else
            {
                // otherwise just replace his data
                SendSpawnMessage(identity, identity.ConnectionToClient);
            }
        }

        internal bool InternalReplacePlayerForConnection(INetworkConnection conn, NetworkClient client, GameObject player, bool keepAuthority)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError("ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }

            if (identity.ConnectionToClient != null && identity.ConnectionToClient != conn)
            {
                logger.LogError("Cannot replace player for connection. New player is already owned by a different connection" + player);
                return false;
            }

            //NOTE: there can be an existing player
            logger.Log("NetworkServer ReplacePlayer");

            NetworkIdentity previousPlayer = conn.Identity;

            conn.Identity = identity;
            identity.Client = client;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.SetClientOwner(conn);

            //NOTE: DONT set connection ready.

            // special case,  we are in host mode,  set hasAuthority to true so that all overrides see it
            if (conn == LocalConnection)
            {
                identity.HasAuthority = true;
                LocalClient.Connection.Identity = identity;
            }

            // add connection to observers AFTER the playerController was set.
            // by definition, there is nothing to observe if there is no player
            // controller.
            //
            // IMPORTANT: do this in AddPlayerForConnection & ReplacePlayerForConnection!
            SpawnObserversForConnection(conn);

            if (logger.LogEnabled()) logger.Log("Replacing playerGameObject object netId: " + player.GetComponent<NetworkIdentity>().NetId + " asset ID " + player.GetComponent<NetworkIdentity>().AssetId);

            Respawn(identity);

            if (!keepAuthority)
                previousPlayer.RemoveClientAuthority();

            return true;
        }

        internal NetworkIdentity GetNetworkIdentity(GameObject go)
        {
            NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                throw new InvalidOperationException("Gameobject does not have NetworkIdentity " + go);
            }
            return identity;
        }

        /// <summary>
        /// Sets the client to be ready.
        /// <para>When a client has signaled that it is ready, this method tells the server that the client is ready to receive spawned objects and state synchronization updates. This is usually called in a handler for the SYSTEM_READY message. If there is not specific action a game needs to take for this message, relying on the default ready handler function is probably fine, so this call wont be needed.</para>
        /// </summary>
        /// <param name="conn">The connection of the client to make ready.</param>
        public void SetClientReady(INetworkConnection conn)
        {
            if (logger.LogEnabled()) logger.Log("SetClientReadyInternal for conn:" + conn);

            // set ready
            conn.IsReady = true;

            // client is ready to start spawning objects
            if (conn.Identity != null)
                SpawnObserversForConnection(conn);
        }

        internal void ShowForConnection(NetworkIdentity identity, INetworkConnection conn)
        {
            if (conn.IsReady)
                SendSpawnMessage(identity, conn);
        }

        internal void HideForConnection(NetworkIdentity identity, INetworkConnection conn)
        {
            var msg = new ObjectHideMessage
            {
                netId = identity.NetId
            };
            conn.Send(msg);
        }

        /// <summary>
        /// Marks all connected clients as no longer ready.
        /// <para>All clients will no longer be sent state synchronization updates. The player's clients can call ClientManager.Ready() again to re-enter the ready state. This is useful when switching scenes.</para>
        /// </summary>
        public void SetAllClientsNotReady()
        {
            foreach (INetworkConnection conn in connections)
            {
                SetClientNotReady(conn);
            }
        }

        /// <summary>
        /// Sets the client of the connection to be not-ready.
        /// <para>Clients that are not ready do not receive spawned objects or state synchronization updates. They client can be made ready again by calling SetClientReady().</para>
        /// </summary>
        /// <param name="conn">The connection of the client to make not ready.</param>
        public void SetClientNotReady(INetworkConnection conn)
        {
            if (conn.IsReady)
            {
                if (logger.LogEnabled()) logger.Log("PlayerNotReady " + conn);
                conn.IsReady = false;
                conn.RemoveObservers();

                conn.Send(new NotReadyMessage());
            }
        }

        // default ready handler.
        void OnClientReadyMessage(INetworkConnection conn, ReadyMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("Default handler for ready message from " + conn);
            SetClientReady(conn);
        }

        // default remove player handler
        void OnRemovePlayerMessage(INetworkConnection conn, RemovePlayerMessage msg)
        {
            if (conn.Identity != null)
            {
                Destroy(conn.Identity.gameObject);
                conn.Identity = null;
            }
            else
            {
                throw new InvalidOperationException("Received remove player message but connection has no player");
            }
        }

        // Handle command from specific player, this could be one of multiple players on a single client
        void OnCommandMessage(INetworkConnection conn, CommandMessage msg)
        {
            if (!spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                logger.LogWarning("Spawned object not found when handling Command message [netId=" + msg.netId + "]");
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            if (identity.ConnectionToClient != conn)
            {
                logger.LogWarning("Command for object without authority [netId=" + msg.netId + "]");
                return;
            }

            if (logger.LogEnabled()) logger.Log("OnCommandMessage for netId=" + msg.netId + " conn=" + conn);

            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                identity.HandleCommand(msg.componentIndex, msg.functionHash, networkReader);
        }

        internal void SpawnObject(GameObject obj, INetworkConnection ownerConnection)
        {
            if (!Active)
            {
                throw new InvalidOperationException("SpawnObject for " + obj + ", NetworkServer is not active. Cannot spawn objects without an active server.");
            }

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                throw new InvalidOperationException("SpawnObject " + obj + " has no NetworkIdentity. Please add a NetworkIdentity to " + obj);
            }

            identity.ConnectionToClient = ownerConnection;
            identity.Server = this;
            identity.Client = LocalClient;

            // special case to make sure hasAuthority is set
            // on start server in host mode
            if (ownerConnection == LocalConnection)
                identity.HasAuthority = true;

            identity.StartServer();

            if (logger.LogEnabled()) logger.Log("SpawnObject instance ID " + identity.NetId + " asset ID " + identity.AssetId);

            identity.RebuildObservers(true);
        }

        internal void SendSpawnMessage(NetworkIdentity identity, INetworkConnection conn)
        {
            if (identity.serverOnly)
                return;

            // for easier debugging
            if (logger.LogEnabled()) logger.Log("Server SendSpawnMessage: name=" + identity.name + " sceneId=" + identity.sceneId.ToString("X") + " netid=" + identity.NetId);

            // one writer for owner, one for observers
            using (PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter(), observersWriter = NetworkWriterPool.GetWriter())
            {
                bool isOwner = identity.ConnectionToClient == conn;

                ArraySegment<byte> payload = CreateSpawnMessagePayload(isOwner, identity, ownerWriter, observersWriter);

                var msg = new SpawnMessage
                {
                    netId = identity.NetId,
                    isLocalPlayer = conn.Identity == identity,
                    isOwner = isOwner,
                    sceneId = identity.sceneId,
                    assetId = identity.AssetId,
                    // use local values for VR support
                    position = identity.transform.localPosition,
                    rotation = identity.transform.localRotation,
                    scale = identity.transform.localScale,

                    payload = payload,
                };


                conn.Send(msg);
            }
        }

        static ArraySegment<byte> CreateSpawnMessagePayload(bool isOwner, NetworkIdentity identity, PooledNetworkWriter ownerWriter, PooledNetworkWriter observersWriter)
        {
            // Only call OnSerializeAllSafely if there are NetworkBehaviours
            if (identity.NetworkBehaviours.Length == 0)
            {
                return default;
            }

            // serialize all components with initialState = true
            // (can be null if has none)
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // use owner segment if 'conn' owns this identity, otherwise
            // use observers segment
            ArraySegment<byte> payload = isOwner ?
                ownerWriter.ToArraySegment() :
                observersWriter.ToArraySegment();

            return payload;
        }

        bool CheckForPrefab(GameObject obj)
        {
#if UNITY_EDITOR
#if UNITY_2018_3_OR_NEWER
            return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj);
#elif UNITY_2018_2_OR_NEWER
            return (UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(obj) == null) && (UnityEditor.PrefabUtility.GetPrefabObject(obj) != null);
#else
            return (UnityEditor.PrefabUtility.GetPrefabParent(obj) == null) && (UnityEditor.PrefabUtility.GetPrefabObject(obj) != null);
#endif
#else
            return false;
#endif
        }

        bool VerifyCanSpawn(GameObject obj)
        {
            if (CheckForPrefab(obj))
            {
                logger.LogFormat(LogType.Error, "GameObject {0} is a prefab, it can't be spawned. This will cause errors in builds.", obj.name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// This spawns an object like NetworkServer.Spawn() but also assigns Client Authority to the specified client.
        /// <para>This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.</para>
        /// </summary>
        /// <param name="obj">The object to spawn.</param>
        /// <param name="player">The player object to set Client Authority to.</param>
        public void Spawn(GameObject obj, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                throw new InvalidOperationException("Player object has no NetworkIdentity");
            }

            if (identity.ConnectionToClient == null)
            {
                throw new InvalidOperationException("Player object is not a " + nameof(player) + ".");
            }

            Spawn(obj, identity.ConnectionToClient);
        }

        /// <summary>
        /// This spawns an object like NetworkServer.Spawn() but also assigns Client Authority to the specified client.
        /// <para>This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.</para>
        /// </summary>
        /// <param name="obj">The object to spawn.</param>
        /// <param name="assetId">The assetId of the object to spawn. Used for custom spawn handlers.</param>
        /// <param name="client">The client associated to the object.</param>
        /// <param name="ownerConnection">The connection that has authority over the object</param>
        public void Spawn(GameObject obj, Guid assetId, INetworkConnection ownerConnection = null)
        {
            if (VerifyCanSpawn(obj))
            {
                NetworkIdentity identity = GetNetworkIdentity(obj);
                identity.AssetId = assetId;
                SpawnObject(obj, ownerConnection);
            }
        }

        /// <summary>
        /// Spawn the given game object on all clients which are ready.
        /// <para>This will cause a new object to be instantiated from the registered prefab, or from a custom spawn function.</para>
        /// </summary>
        /// <param name="obj">Game object with NetworkIdentity to spawn.</param>
        /// <param name="client">Client associated to the object.</param>
        /// <param name="ownerConnection">The connection that has authority over the object</param>
        public void Spawn(GameObject obj, INetworkConnection ownerConnection = null)
        {
            if (VerifyCanSpawn(obj))
            {
                SpawnObject(obj, ownerConnection);
            }
        }

        void DestroyObject(NetworkIdentity identity, bool destroyServerObject)
        {
            if (logger.LogEnabled()) logger.Log("DestroyObject instance:" + identity.NetId);
            spawned.Remove(identity.NetId);

            identity.ConnectionToClient?.RemoveOwnedObject(identity);

            var msg = new ObjectDestroyMessage
            {
                netId = identity.NetId
            };
            SendToObservers(identity, msg);

            identity.ClearObservers();
            if (LocalClientActive)
            {
                identity.StopClient();
            }

            identity.StopServer();

            identity.Reset();
            // when unspawning, dont destroy the server's object
            if (destroyServerObject)
            {
                UnityEngine.Object.Destroy(identity.gameObject);
            }
        }

        /// <summary>
        /// Destroys this object and corresponding objects on all clients.
        /// <para>In some cases it is useful to remove an object but not delete it on the server. For that, use NetworkServer.UnSpawn() instead of NetworkServer.Destroy().</para>
        /// </summary>
        /// <param name="obj">Game object to destroy.</param>
        public void Destroy(GameObject obj)
        {
            if (obj == null)
            {
                logger.Log("NetworkServer DestroyObject is null");
                return;
            }

            NetworkIdentity identity = GetNetworkIdentity(obj);
            DestroyObject(identity, true);
        }

        /// <summary>
        /// This takes an object that has been spawned and un-spawns it.
        /// <para>The object will be removed from clients that it was spawned on, or the custom spawn handler function on the client will be called for the object.</para>
        /// <para>Unlike when calling NetworkServer.Destroy(), on the server the object will NOT be destroyed. This allows the server to re-use the object, even spawn it again later.</para>
        /// </summary>
        /// <param name="obj">The spawned object to be unspawned.</param>
        public void UnSpawn(GameObject obj)
        {
            if (obj == null)
            {
                logger.Log("NetworkServer UnspawnObject is null");
                return;
            }

            NetworkIdentity identity = GetNetworkIdentity(obj);
            DestroyObject(identity, false);
        }

        internal bool ValidateSceneObject(NetworkIdentity identity)
        {
            if (identity.gameObject.hideFlags == HideFlags.NotEditable ||
                identity.gameObject.hideFlags == HideFlags.HideAndDontSave)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(identity.gameObject))
                return false;
#endif

            // If not a scene object
            return identity.sceneId != 0;
        }

        /// <summary>
        /// This causes NetworkIdentity objects in a scene to be spawned on a server.
        /// <para>NetworkIdentity objects in a scene are disabled by default. Calling SpawnObjects() causes these scene objects to be enabled and spawned. It is like calling NetworkServer.Spawn() for each of them.</para>
        /// </summary>
        /// <param name="client">The client associated to the objects.</param>
        /// <returns>Success if objects where spawned.</returns>
        public bool SpawnObjects()
        {
            // only if server active
            if (!Active)
                return false;

            NetworkIdentity[] identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                {
                    if (logger.LogEnabled()) logger.Log("SpawnObjects sceneId:" + identity.sceneId.ToString("X") + " name:" + identity.gameObject.name);

                    identity.gameObject.SetActive(true);

                    Spawn(identity.gameObject);
                }
            }

            return true;
        }
    }
}
