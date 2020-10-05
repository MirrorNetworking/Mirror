using System;
using System.Collections.Generic;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// The NetworkServer.
    /// </summary>
    /// <remarks>
    /// <para>NetworkServer handles remote connections from remote clients via a NetworkServerSimple instance, and also has a local connection for a local client.</para>
    /// <para>The NetworkServer is a singleton. It has static convenience functions such as NetworkServer.SendToAll() and NetworkServer.Spawn() which automatically use the singleton instance.</para>
    /// <para>The NetworkManager uses the NetworkServer, but it can be used without the NetworkManager.</para>
    /// <para>The set of networked objects that have been spawned is managed by NetworkServer. Objects are spawned with NetworkServer.Spawn() which adds them to this set, and makes them be created on clients. Spawned objects are removed automatically when they are destroyed, or than they can be removed from the spawned set by calling NetworkServer.UnSpawn() - this does not destroy the object.</para>
    /// <para>There are a number of internal messages used by NetworkServer, these are setup when NetworkServer.Listen() is called.</para>
    /// </remarks>
    public static class NetworkServer
    {
        static bool initialized;
        static int maxConnections;

        // interest management should be configured before starting.
        public static InterestManagement interestManagement;

        /// <summary>
        /// A list of local connections on the server.
        /// </summary>
        public static Dictionary<int, NetworkConnectionToClient> connections = new Dictionary<int, NetworkConnectionToClient>();

        /// <summary>
        /// <para>Dictionary of the message handlers registered with the server.</para>
        /// <para>The key to the dictionary is the message Id.</para>
        /// </summary>
        static Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>
        /// <para>If you enable this, the server will not listen for incoming connections on the regular network port.</para>
        /// <para>This can be used if the game is running in host mode and does not want external players to be able to connect - making it like a single-player game. Also this can be useful when using AddExternalConnection().</para>
        /// </summary>
        public static bool dontListen;

        /// <summary>
        /// <para>Checks if the server has been started.</para>
        /// <para>This will be true after NetworkServer.Listen() has been called.</para>
        /// </summary>
        public static bool active { get; internal set; }

        /// <summary>
        /// This shuts down the server and disconnects all clients.
        /// </summary>
        public static void Shutdown()
        {
            if (initialized)
            {
                DisconnectAll();

                if (!dontListen)
                {
                    // stop the server.
                    // we do NOT call Transport.Shutdown, because someone only
                    // called NetworkServer.Shutdown. we can't assume that the
                    // client is supposed to be shut down too!
                    Transport.activeTransport.ServerStop();
                }

                Transport.activeTransport.OnServerDisconnected.RemoveListener(OnDisconnected);
                Transport.activeTransport.OnServerConnected.RemoveListener(OnConnected);
                Transport.activeTransport.OnServerDataReceived.RemoveListener(OnDataReceived);
                Transport.activeTransport.OnServerError.RemoveListener(OnError);

                initialized = false;
            }
            dontListen = false;
            active = false;
            handlers.Clear();

            CleanupNetworkIdentities();
            NetworkIdentity.ResetNextNetworkId();
        }

        static void CleanupNetworkIdentities()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (identity != null)
                {
                    if (identity.sceneId != 0)
                    {
                        identity.Reset();
                        identity.gameObject.SetActive(false);
                    }
                    else
                    {
                        GameObject.Destroy(identity.gameObject);
                    }
                }
            }

            NetworkIdentity.spawned.Clear();
        }

        static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            // Debug.Log("NetworkServer Created version " + Version.Current);

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();

            Transport.activeTransport.OnServerDisconnected.AddListener(OnDisconnected);
            Transport.activeTransport.OnServerConnected.AddListener(OnConnected);
            Transport.activeTransport.OnServerDataReceived.AddListener(OnDataReceived);
            Transport.activeTransport.OnServerError.AddListener(OnError);
        }

        internal static void RegisterMessageHandlers()
        {
            RegisterHandler<ReadyMessage>(OnClientReadyMessage);
            RegisterHandler<CommandMessage>(OnCommandMessage);
            RegisterHandler<NetworkPingMessage>(NetworkTime.OnServerPing, false);
        }

        /// <summary>
        /// Start the server, setting the maximum number of connections.
        /// </summary>
        /// <param name="maxConns">Maximum number of allowed connections</param>
        public static void Listen(int maxConns)
        {
            Initialize();
            maxConnections = maxConns;

            // only start server if we want to listen
            if (!dontListen)
            {
                Transport.activeTransport.ServerStart();
                Debug.Log("Server started listening");
            }

            active = true;
            RegisterMessageHandlers();
        }

        /// <summary>
        /// <para>This accepts a network connection and adds it to the server.</para>
        /// <para>This connection will use the callbacks registered with the server.</para>
        /// </summary>
        /// <param name="conn">Network connection to add.</param>
        /// <returns>True if added.</returns>
        public static bool AddConnection(NetworkConnectionToClient conn)
        {
            if (!connections.ContainsKey(conn.connectionId))
            {
                // connection cannot be null here or conn.connectionId
                // would throw NRE
                connections[conn.connectionId] = conn;
                conn.SetHandlers(handlers);
                return true;
            }
            // already a connection with this id
            return false;
        }

        /// <summary>
        /// This removes an external connection added with AddExternalConnection().
        /// </summary>
        /// <param name="connectionId">The id of the connection to remove.</param>
        /// <returns>True if the removal succeeded</returns>
        public static bool RemoveConnection(int connectionId)
        {
            return connections.Remove(connectionId);
        }

        /// <summary>
        /// this is like SendToReady - but it doesn't check the ready flag on the connection.
        /// this is used for ObjectDestroy messages.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identity"></param>
        /// <param name="msg"></param>
        /// <param name="channelId"></param>
        static void SendToObservers<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log("Server.SendToObservers id:" + typeof(T));

            if (identity == null || identity.observers.Count == 0)
                return;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message into byte[] once
                MessagePacker.Pack(msg, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // send
                foreach (NetworkConnectionToClient conn in identity.observers)
                {
                    conn.Send(segment, channelId);
                }
            }
        }

        /// <summary>
        /// Send a message to all connected clients, both ready and not-ready.
        /// <para>See <see cref="NetworkConnection.isReady">NetworkConnection.isReady</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="msg">Message</param>
        /// <param name="channelId">Transport channel to use</param>
        /// <param name="sendToReadyOnly">Indicates if only ready clients should receive the message</param>
        /// <returns></returns>
        public static bool SendToAll<T>(T msg, int channelId = Channels.DefaultReliable, bool sendToReadyOnly = false)
            where T : struct, NetworkMessage
        {
            if (!active)
            {
                Debug.LogWarning("Can not send using NetworkServer.SendToAll<T>(T msg) because NetworkServer is not active");
                return false;
            }

            // Debug.Log("Server.SendToAll id:" + typeof(T));

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message only once
                MessagePacker.Pack(msg, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // send
                bool result = true;
                foreach (NetworkConnectionToClient conn in connections.Values)
                {
                    if (sendToReadyOnly && !conn.isReady)
                        continue;

                    result &= conn.Send(segment, channelId);
                }
                return result;
            }
        }

        /// <summary>
        /// Send a message to only clients which are ready.
        /// <para>See <see cref="NetworkConnection.isReady">NetworkConnection.isReady</see></para>
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="msg">Message</param>
        /// <param name="channelId">Transport channel to use</param>
        /// <returns></returns>
        public static bool SendToReady<T>(T msg, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            if (!active)
            {
                Debug.LogWarning("Can not send using NetworkServer.SendToReady<T>(T msg) because NetworkServer is not active");
                return false;
            }

            return SendToAll(msg, channelId, true);
        }

        /// <summary>
        /// Send a message to only clients which are ready with option to include the owner of the object identity.
        /// <para>See <see cref="NetworkConnection.isReady">NetworkConnection.isReady</see></para>
        /// </summary>
        /// <typeparam name="T">Message type.</typeparam>
        /// <param name="identity">Identity of the owner</param>
        /// <param name="msg">Message</param>
        /// <param name="includeOwner">Should the owner of the object be included</param>
        /// <param name="channelId">Transport channel to use</param>
        /// <returns></returns>
        public static bool SendToReady<T>(NetworkIdentity identity, T msg, bool includeOwner = true, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log("Server.SendToReady msgType:" + typeof(T));

            if (identity == null || identity.observers.Count == 0)
                return false;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message only once
                MessagePacker.Pack(msg, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // send
                bool result = true;
                foreach (NetworkConnectionToClient conn in identity.observers)
                {
                    bool isOwner = conn == identity.connectionToClient;
                    if ((!isOwner || includeOwner) && conn.isReady)
                    {
                        result &= conn.Send(segment, channelId);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Send a message to only clients which are ready including the owner of the object identity.
        /// <para>See <see cref="NetworkConnection.isReady">NetworkConnection.isReady</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="identity">identity of the object</param>
        /// <param name="msg">Message</param>
        /// <param name="channelId">Transport channel to use</param>
        /// <returns></returns>
        public static bool SendToReady<T>(NetworkIdentity identity, T msg, int channelId)
            where T : struct, NetworkMessage
        {
            return SendToReady(identity, msg, true, channelId);
        }

        /// <summary>
        /// Disconnect all currently connected clients, including the local connection.
        /// <para>This can only be called on the server. Clients will receive the Disconnect message.</para>
        /// </summary>
        public static void DisconnectAll()
        {
            DisconnectAllConnections();
            active = false;
        }

        /// <summary>
        /// Disconnect all currently connected clients except the local connection.
        /// <para>This can only be called on the server. Clients will receive the Disconnect message.</para>
        /// </summary>
        public static void DisconnectAllConnections()
        {
            foreach (NetworkConnection conn in connections.Values)
            {
                conn.Disconnect();
                OnDisconnected(conn);
                conn.Dispose();
            }
            connections.Clear();
        }

        /// <summary>
        /// If connections is empty or if only has host
        /// </summary>
        /// <returns></returns>
        public static bool NoConnections()
        {
            return connections.Count == 0;
        }

        /// <summary>
        /// Called from NetworkManager in LateUpdate
        /// <para>The user should never need to pump the update loop manually</para>
        /// </summary>
        public static void Update()
        {
            // dont need to update server if not active
            if (!active)
                return;

            // update all server objects
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in NetworkIdentity.spawned)
            {
                NetworkIdentity identity = kvp.Value;
                if (identity != null)
                {
                    identity.ServerUpdate();
                }
                else
                {
                    // spawned list should have no null entries because we
                    // always call Remove in OnObjectDestroy everywhere.
                    Debug.LogWarning("Found 'null' entry in spawned list for netId=" + kvp.Key + ". Please call NetworkServer.Destroy to destroy networked objects. Don't use GameObject.Destroy.");
                }
            }
        }

        static void OnConnected(int connectionId)
        {
            // Debug.Log("Server accepted client:" + connectionId);

            // connectionId needs to be > 0 because 0 is reserved for local player
            if (connectionId <= 0)
            {
                Debug.LogError("Server.HandleConnect: invalid connectionId: " + connectionId + " . Needs to be >0, because 0 is reserved for local player.");
                Transport.activeTransport.ServerDisconnect(connectionId);
                return;
            }

            // connectionId not in use yet?
            if (connections.ContainsKey(connectionId))
            {
                Transport.activeTransport.ServerDisconnect(connectionId);
                // Debug.Log("Server connectionId " + connectionId + " already in use. kicked client:" + connectionId);
                return;
            }

            // are more connections allowed? if not, kick
            // (it's easier to handle this in Mirror, so Transports can have
            //  less code and third party transport might not do that anyway)
            // (this way we could also send a custom 'tooFull' message later,
            //  Transport can't do that)
            if (connections.Count < maxConnections)
            {
                // add connection
                NetworkConnectionToClient conn = new NetworkConnectionToClient(connectionId);
                OnConnected(conn);
            }
            else
            {
                // kick
                Transport.activeTransport.ServerDisconnect(connectionId);
                // Debug.Log("Server full, kicked client:" + connectionId);
            }
        }

        internal static void OnConnected(NetworkConnectionToClient conn)
        {
            // Debug.Log("Server accepted client:" + conn);

            // add connection and invoke connected event
            AddConnection(conn);
            conn.InvokeHandler(new ConnectMessage());
        }

        internal static void OnDisconnected(int connectionId)
        {
            // Debug.Log("Server disconnect client:" + connectionId);

            if (connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                conn.Disconnect();
                RemoveConnection(connectionId);
                // Debug.Log("Server lost client:" + connectionId);

                OnDisconnected(conn);

                // interest management rebuild to remove the connection from all
                // entity's observers. otherwise broadcast systems will still try to
                // send to this connection, which isn't a big problem, but it does
                // give an "invalid connectionId" warning message.
                // and if we have 10k monsters, we get 10k warning messages, which
                // would actually slow down/freeze the editor for a short time.
                // => AFTER removing the connection, because RebuildAll will attempt
                //    to send unspawn messages, which have a connection-still-valid
                //    check.
                // NOTE: we could also use a simple removeFromAllObservers function,
                //       which would be faster, but it's also extra code and extra
                //       complexity.
                //       AND it wouldn't even work properly, because by just
                //       removing the connection, the observers would never get
                //       the unspawn messages.
                //       In other words, always do a full rebuild because IT WORKS
                //       perfectly.
                if (interestManagement != null)
                {
                    interestManagement.RebuildAll();
                }
                else Debug.LogError($"InterestManagement not found. Please add an InterestManagement component like {typeof(BruteForceInterestManagement)} to the NetworkManager!");
            }
        }

        static void OnDisconnected(NetworkConnection conn)
        {
            conn.InvokeHandler(new DisconnectMessage());
            // Debug.Log("Server lost client:" + conn);
        }

        static void OnDataReceived(int connectionId, ArraySegment<byte> data)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                conn.TransportReceive(data);
            }
            else
            {
                Debug.LogError("HandleData Unknown connectionId:" + connectionId);
            }
        }

        static void OnError(int connectionId, Exception exception)
        {
            // TODO Let's discuss how we will handle errors
            Debug.LogException(exception);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacker.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            RegisterHandler<T>((_, value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>
        /// Replaces a handler for a particular message type.
        /// <para>See also <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)">RegisterHandler(T)(Action(NetworkConnection, T), bool)</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacker.GetId<T>();
            handlers[msgType] = MessagePacker.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>
        /// Replaces a handler for a particular message type.
        /// <para>See also <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)">RegisterHandler(T)(Action(NetworkConnection, T), bool)</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler<T>((_, value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>
        /// Unregisters a handler for a particular message type.
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        public static void UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        /// <summary>
        /// Clear all registered callback handlers.
        /// </summary>
        public static void ClearHandlers()
        {
            handlers.Clear();
        }

        /// <summary>
        /// send this message to the player only
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="identity"></param>
        /// <param name="msg"></param>
        public static void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            if (identity != null)
            {
                identity.connectionToClient.Send(msg, channelId);
            }
            else
            {
                Debug.LogError("SendToClientOfPlayer: player has no NetworkIdentity: " + identity);
            }
        }

        /// <summary>
        /// <para>When an AddPlayer message handler has received a request from a player, the server calls this to associate the player object with the connection.</para>
        /// <para>When a player is added for a connection, the client for that connection is made ready automatically. The player object is automatically spawned, so you do not need to call NetworkServer.Spawn for that object. This function is used for "adding" a player, not for "replacing" the player on a connection. If there is already a player on this playerControllerId for this connection, this will fail.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="player">Player object spawned for the player.</param>
        /// <param name="assetId"></param>
        /// <returns>True if connection was sucessfully added for a connection.</returns>
        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return AddPlayerForConnection(conn, player);
        }

        /// <summary>
        /// <para>When an AddPlayer message handler has received a request from a player, the server calls this to associate the player object with the connection.</para>
        /// <para>When a player is added for a connection, the client for that connection is made ready automatically. The player object is automatically spawned, so you do not need to call NetworkServer.Spawn for that object. This function is used for "adding" a player, not for "replacing" the player on a connection. If there is already a player on this playerControllerId for this connection, this will fail.</para>
        /// </summary>
        /// <param name="conn">Connection which is adding the player.</param>
        /// <param name="player">Player object spawned for the player.</param>
        /// <returns>True if connection was successfully added for a connection.</returns>
        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogWarning("AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }

            // cannot have a player object in "Add" version
            if (conn.identity != null)
            {
                Debug.Log("AddPlayer: player object already exists");
                return false;
            }

            // make sure we have a controller before we call SetClientReady
            // because the observers will be rebuilt only if we have a controller
            conn.identity = identity;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.SetClientOwner(conn);

            // set ready if not set yet
            SetClientReady(conn);

            // Debug.Log("Adding new playerGameObject object netId: " + identity.netId + " asset ID " + identity.assetId);

            Respawn(identity);
            return true;
        }

        static void Respawn(NetworkIdentity identity)
        {
            if (identity.netId == 0)
            {
                // If the object has not been spawned, then do a full spawn and update observers
                Spawn(identity.gameObject, identity.connectionToClient);
            }
            else
            {
                // otherwise just replace his data
                SendSpawnMessage(identity, identity.connectionToClient);
            }
        }

        internal static bool GetNetworkIdentity(GameObject go, out NetworkIdentity identity)
        {
            identity = go.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"GameObject {go.name} doesn't have NetworkIdentity.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sets the client to be ready.
        /// <para>When a client has signaled that it is ready, this method tells the server that the client is ready to receive spawned objects and state synchronization updates. This is usually called in a handler for the SYSTEM_READY message. If there is not specific action a game needs to take for this message, relying on the default ready handler function is probably fine, so this call wont be needed.</para>
        /// </summary>
        /// <param name="conn">The connection of the client to make ready.</param>
        public static void SetClientReady(NetworkConnection conn)
        {
            // Debug.Log("SetClientReadyInternal for conn:" + conn);

            // set ready
            conn.isReady = true;
        }

        internal static void ShowForConnection(NetworkIdentity identity, NetworkConnection conn)
        {
            if (conn.isReady)
                SendSpawnMessage(identity, conn);
        }

        internal static void HideForConnection(NetworkIdentity identity, NetworkConnection conn)
        {
            ObjectHideMessage msg = new ObjectHideMessage
            {
                netId = identity.netId
            };
            conn.Send(msg);
        }

        /// <summary>
        /// Marks all connected clients as no longer ready.
        /// <para>All clients will no longer be sent state synchronization updates. The player's clients can call ClientManager.Ready() again to re-enter the ready state. This is useful when switching scenes.</para>
        /// </summary>
        public static void SetAllClientsNotReady()
        {
            foreach (NetworkConnection conn in connections.Values)
            {
                SetClientNotReady(conn);
            }
        }

        /// <summary>
        /// Sets the client of the connection to be not-ready.
        /// <para>Clients that are not ready do not receive spawned objects or state synchronization updates. They client can be made ready again by calling SetClientReady().</para>
        /// </summary>
        /// <param name="conn">The connection of the client to make not ready.</param>
        public static void SetClientNotReady(NetworkConnection conn)
        {
            if (conn.isReady)
            {
                // Debug.Log("PlayerNotReady " + conn);
                conn.isReady = false;
                conn.Send(new NotReadyMessage());
            }
        }

        /// <summary>
        /// default ready handler.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        static void OnClientReadyMessage(NetworkConnection conn, ReadyMessage msg)
        {
            // Debug.Log("Default handler for ready message from " + conn);
            SetClientReady(conn);
        }

        /// <summary>
        /// Removes the player object from the connection
        /// </summary>
        /// <param name="conn">The connection of the client to remove from</param>
        /// <param name="destroyServerObject">Indicates whether the server object should be destroyed</param>
        public static void RemovePlayerForConnection(NetworkConnection conn, bool destroyServerObject)
        {
            if (conn.identity != null)
            {
                if (destroyServerObject)
                    Destroy(conn.identity.gameObject);
                else
                    UnSpawn(conn.identity.gameObject);

                conn.identity = null;
            }
            else
            {
                // Debug.Log($"Connection {conn} has no identity");
            }
        }

        /// <summary>
        /// Handle command from specific player, this could be one of multiple players on a single client
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        static void OnCommandMessage(NetworkConnection conn, CommandMessage msg)
        {
            if (!NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                Debug.LogWarning("Spawned object not found when handling Command message [netId=" + msg.netId + "]");
                return;
            }

            CommandInfo commandInfo = identity.GetCommandInfo(msg.componentIndex, msg.functionHash);

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            bool needAuthority = !commandInfo.ignoreAuthority;
            if (needAuthority && identity.connectionToClient != conn)
            {
                Debug.LogWarning("Command for object without authority [netId=" + msg.netId + "]");
                return;
            }

            // Debug.Log("OnCommandMessage for netId=" + msg.netId + " conn=" + conn);

            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, MirrorInvokeType.Command, networkReader, conn as NetworkConnectionToClient);
        }

        internal static void SpawnObject(GameObject obj, NetworkConnection ownerConnection)
        {
            if (!active)
            {
                Debug.LogError("SpawnObject for " + obj + ", NetworkServer is not active. Cannot spawn objects without an active server.");
                return;
            }

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("SpawnObject " + obj + " has no NetworkIdentity. Please add a NetworkIdentity to " + obj);
                return;
            }

            if (identity.SpawnedFromInstantiate)
            {
                // Using Instantiate on SceneObject is not allowed, so stop spawning here
                // NetworkIdentity.Awake already logs error, no need to log a second error here
                return;
            }

            identity.connectionToClient = (NetworkConnectionToClient)ownerConnection;

            identity.OnStartServer();

            // Debug.Log("SpawnObject instance ID " + identity.netId + " asset ID " + identity.assetId);
        }

        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly)
                return;

            // for easier debugging
            // Debug.Log("Server SendSpawnMessage: name=" + identity.name + " sceneId=" + identity.sceneId.ToString("X") + " netid=" + identity.netId);

            // one writer for owner, one for observers
            using (PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter(), observersWriter = NetworkWriterPool.GetWriter())
            {
                bool isOwner = identity.connectionToClient == conn;

                ArraySegment<byte> payload = CreateSpawnMessagePayload(isOwner, identity, ownerWriter, observersWriter);

                SpawnMessage msg = new SpawnMessage
                {
                    netId = identity.netId,
                    isLocalPlayer = conn.identity == identity,
                    isOwner = isOwner,
                    sceneId = identity.sceneId,
                    assetId = identity.assetId,
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
            ulong dirtyComponentsMask = identity.GetInitialComponentsMask();
            identity.OnSerializeAllSafely(true, dirtyComponentsMask, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // convert to ArraySegment to avoid reader allocations
            // (need to handle null case too)
            ArraySegment<byte> ownerSegment = ownerWritten > 0 ? ownerWriter.ToArraySegment() : default;
            ArraySegment<byte> observersSegment = observersWritten > 0 ? observersWriter.ToArraySegment() : default;

            // use owner segment if 'conn' owns this identity, otherwise
            // use observers segment
            ArraySegment<byte> payload = isOwner ? ownerSegment : observersSegment;

            return payload;
        }

        /// <summary>
        /// This destroys all the player objects associated with a NetworkConnections on a server.
        /// <para>This is used when a client disconnects, to remove the players for that client. This also destroys non-player objects that have client authority set for this connection.</para>
        /// </summary>
        /// <param name="conn">The connections object to clean up for.</param>
        public static void DestroyPlayerForConnection(NetworkConnection conn)
        {
            // destroy all objects owned by this connection, including the player object
            conn.DestroyOwnedObjects();
            conn.identity = null;
        }

        /// <summary>
        /// Spawn the given game object on all clients which are ready.
        /// <para>This will cause a new object to be instantiated from the registered prefab, or from a custom spawn function.</para>
        /// </summary>
        /// <param name="obj">Game object with NetworkIdentity to spawn.</param>
        /// <param name="ownerConnection">The connection that has authority over the object</param>
        public static void Spawn(GameObject obj, NetworkConnection ownerConnection = null)
        {
            if (VerifyCanSpawn(obj))
            {
                SpawnObject(obj, ownerConnection);
            }
        }

        /// <summary>
        /// This spawns an object like NetworkServer.Spawn() but also assigns Client Authority to the specified client.
        /// <para>This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.</para>
        /// </summary>
        /// <param name="obj">The object to spawn.</param>
        /// <param name="ownerPlayer">The player object to set Client Authority to.</param>
        public static void Spawn(GameObject obj, GameObject ownerPlayer)
        {
            NetworkIdentity identity = ownerPlayer.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Player object has no NetworkIdentity");
                return;
            }

            if (identity.connectionToClient == null)
            {
                Debug.LogError("Player object is not a player.");
                return;
            }

            Spawn(obj, identity.connectionToClient);
        }

        /// <summary>
        /// This spawns an object like NetworkServer.Spawn() but also assigns Client Authority to the specified client.
        /// <para>This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.</para>
        /// </summary>
        /// <param name="obj">The object to spawn.</param>
        /// <param name="assetId">The assetId of the object to spawn. Used for custom spawn handlers.</param>
        /// <param name="ownerConnection">The connection that has authority over the object</param>
        public static void Spawn(GameObject obj, Guid assetId, NetworkConnection ownerConnection = null)
        {
            if (VerifyCanSpawn(obj))
            {
                if (GetNetworkIdentity(obj, out NetworkIdentity identity))
                {
                    identity.assetId = assetId;
                }
                SpawnObject(obj, ownerConnection);
            }
        }

        static bool CheckForPrefab(GameObject obj)
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

        static bool VerifyCanSpawn(GameObject obj)
        {
            if (CheckForPrefab(obj))
            {
                Debug.LogError($"GameObject {obj.name} is a prefab, it can't be spawned. This will cause errors in builds.");
                return false;
            }

            return true;
        }

        static void DestroyObject(NetworkIdentity identity, bool destroyServerObject)
        {
            // Debug.Log("DestroyObject instance:" + identity.netId);
            NetworkIdentity.spawned.Remove(identity.netId);

            identity.connectionToClient?.RemoveOwnedObject(identity);

            ObjectDestroyMessage msg = new ObjectDestroyMessage
            {
                netId = identity.netId
            };
            SendToObservers(identity, msg);

            identity.OnStopServer();

            // when unspawning, dont destroy the server's object
            if (destroyServerObject)
            {
                identity.destroyCalled = true;
                UnityEngine.Object.Destroy(identity.gameObject);
            }
            // if we are destroying the server object we don't need to reset the identity
            // reseting it will cause isClient/isServer to be false in the OnDestroy call
            else
            {
                identity.Reset();
            }
        }

        /// <summary>
        /// Destroys this object and corresponding objects on all clients.
        /// <para>In some cases it is useful to remove an object but not delete it on the server. For that, use NetworkServer.UnSpawn() instead of NetworkServer.Destroy().</para>
        /// </summary>
        /// <param name="obj">Game object to destroy.</param>
        public static void Destroy(GameObject obj)
        {
            if (obj == null)
            {
                Debug.Log("NetworkServer DestroyObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, true);
            }
        }

        /// <summary>
        /// This takes an object that has been spawned and un-spawns it.
        /// <para>The object will be removed from clients that it was spawned on, or the custom spawn handler function on the client will be called for the object.</para>
        /// <para>Unlike when calling NetworkServer.Destroy(), on the server the object will NOT be destroyed. This allows the server to re-use the object, even spawn it again later.</para>
        /// </summary>
        /// <param name="obj">The spawned object to be unspawned.</param>
        public static void UnSpawn(GameObject obj)
        {
            if (obj == null)
            {
                Debug.Log("NetworkServer UnspawnObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, false);
            }
        }

        internal static bool ValidateSceneObject(NetworkIdentity identity)
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
        /// <returns>Success if objects where spawned.</returns>
        public static bool SpawnObjects()
        {
            // only if server active
            if (!active)
                return false;

            NetworkIdentity[] identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                {
                    // Debug.Log("SpawnObjects sceneId:" + identity.sceneId.ToString("X") + " name:" + identity.gameObject.name);
                    identity.gameObject.SetActive(true);
                }
            }

            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                    Spawn(identity.gameObject);
            }
            return true;
        }
    }
}
