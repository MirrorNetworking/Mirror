using System;
using System.Collections.Generic;
using System.Linq;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    /// <summary>NetworkServer handles remote connections and has a local connection for a local client.</summary>
    public static class NetworkServer
    {
        static bool initialized;
        public static int maxConnections;

        /// <summary>Connection to host mode client (if any)</summary>
        public static NetworkConnectionToClient localConnection { get; private set; }

        /// <summary>True is a local client is currently active on the server</summary>
        public static bool localClientActive => localConnection != null;

        /// <summary>Dictionary of all server connections, with connectionId as key</summary>
        public static Dictionary<int, NetworkConnectionToClient> connections =
            new Dictionary<int, NetworkConnectionToClient>();

        /// <summary>Message Handlers dictionary, with mesageId as key</summary>
        static Dictionary<int, NetworkMessageDelegate> handlers =
            new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>Single player mode can use dontListen to not accept incoming connections</summary>
        // see also: https://github.com/vis2k/Mirror/pull/2595
        public static bool dontListen;

        /// <summary>active checks if the server has been started</summary>
        public static bool active { get; internal set; }

        /// <summary>batch messages and send them out in LateUpdate (or after batchInterval)</summary>
        // (this is pretty much always a good idea)
        public static bool batching = true;

        /// <summary>interval in seconds used for batching. 0 means send in every LateUpdate.</summary>
        public static float batchInterval = 0;

        // interest management component (optional)
        // by default, everyone observes everyone
        public static InterestManagement aoi;

        /// <summary>Automatically disconnect inactive connections after a timeout. Can be changed at runtime.</summary>
        public static bool disconnectInactiveConnections;

        /// <summary>Timeout in seconds to disconnect inactive connections. Can be changed at runtime.</summary>
        // By default, clients send at least a Ping message every 2 seconds.
        // The Host client is immune from idle timeout disconnection.
        public static float disconnectInactiveTimeout = 60;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        internal static Action<NetworkConnection> OnConnectedEvent;
        internal static Action<NetworkConnection> OnDisconnectedEvent;

        // initialization / shutdown ///////////////////////////////////////////
        static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            // Debug.Log("NetworkServer Created version " + Version.Current);

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();

            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkServer.Listen, If you are calling Listen manually then make sure to set 'Transport.activeTransport' first");
            AddTransportHandlers();
        }

        static void AddTransportHandlers()
        {
            Transport.activeTransport.OnServerConnected = OnConnected;
            Transport.activeTransport.OnServerDataReceived = OnDataReceived;
            Transport.activeTransport.OnServerDisconnected = OnDisconnected;
            Transport.activeTransport.OnServerError = OnError;
        }

        public static void ActivateHostScene()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (!identity.isClient)
                {
                    // Debug.Log("ActivateHostScene " + identity.netId + " " + identity);
                    identity.OnStartClient();
                }
            }
        }

        internal static void RegisterMessageHandlers()
        {
            RegisterHandler<ReadyMessage>(OnClientReadyMessage);
            RegisterHandler<CommandMessage>(OnCommandMessage);
            RegisterHandler<NetworkPingMessage>(NetworkTime.OnServerPing, false);
        }

        /// <summary>Starts server and listens to incoming connections with max connections limit.</summary>
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

        // Note: NetworkClient.DestroyAllClientObjects does the same on client.
        static void CleanupNetworkIdentities()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (identity != null)
                {
                    // scene objects are reset and disabled.
                    // they always stay in the scene, we don't destroy them.
                    if (identity.sceneId != 0)
                    {
                        identity.Reset();
                        identity.gameObject.SetActive(false);
                    }
                    // spawned objects are destroyed
                    else
                    {
                        GameObject.Destroy(identity.gameObject);
                    }
                }
            }

            NetworkIdentity.spawned.Clear();
        }

        /// <summary>Shuts down the server and disconnects all clients</summary>
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

                initialized = false;
            }
            dontListen = false;
            active = false;
            handlers.Clear();

            CleanupNetworkIdentities();
            NetworkIdentity.ResetNextNetworkId();
        }

        // connections /////////////////////////////////////////////////////////
        /// <summary>Add a connection and setup callbacks. Returns true if not added yet.</summary>
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

        /// <summary>Removes a connection by connectionId. Returns true if removed.</summary>
        public static bool RemoveConnection(int connectionId)
        {
            return connections.Remove(connectionId);
        }

        // called by LocalClient to add itself. don't call directly.
        internal static void SetLocalConnection(LocalConnectionToClient conn)
        {
            if (localConnection != null)
            {
                Debug.LogError("Local Connection already exists");
                return;
            }

            localConnection = conn;
        }

        internal static void RemoveLocalConnection()
        {
            if (localConnection != null)
            {
                localConnection.Disconnect();
                localConnection = null;
            }
            RemoveConnection(0);
        }

        /// <summary>True if we have no external connections (host is allowed)</summary>
        public static bool NoExternalConnections()
        {
            return connections.Count == 0 ||
                   (connections.Count == 1 && localConnection != null);
        }
        [Obsolete("NoConnections was renamed to NoExternalConnections because that's what it checks for.")]
        public static bool NoConnections() => NoExternalConnections();

        // send ////////////////////////////////////////////////////////////////
        /// <summary>Send a message to all clients, even those that haven't joined the world yet (non ready)</summary>
        public static void SendToAll<T>(T message, int channelId = Channels.Reliable, bool sendToReadyOnly = false)
            where T : struct, NetworkMessage
        {
            if (!active)
            {
                Debug.LogWarning("Can not send using NetworkServer.SendToAll<T>(T msg) because NetworkServer is not active");
                return;
            }

            // Debug.Log("Server.SendToAll id:" + typeof(T));
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message only once
                MessagePacking.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // filter and then send to all internet connections at once
                // -> makes code more complicated, but is HIGHLY worth it to
                //    avoid allocations, allow for multicast, etc.
                int count = 0;
                foreach (NetworkConnectionToClient conn in connections.Values)
                {
                    if (sendToReadyOnly && !conn.isReady)
                        continue;

                    count++;
                    conn.Send(segment, channelId);
                }

                NetworkDiagnostics.OnSend(message, channelId, segment.Count, count);
            }
        }

        /// <summary>Send a message to all clients which have joined the world (are ready).</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReady<T>(T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            if (!active)
            {
                Debug.LogWarning("Can not send using NetworkServer.SendToReady<T>(T msg) because NetworkServer is not active");
                return;
            }

            SendToAll(message, channelId, true);
        }

        /// <summary>Send a message to only clients which are ready with option to include the owner of the object identity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReady<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log("Server.SendToReady msgType:" + typeof(T));
            if (identity == null || identity.observers == null || identity.observers.Count == 0)
                return;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message only once
                MessagePacking.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                int count = 0;
                foreach (NetworkConnection conn in identity.observers.Values)
                {
                    bool isOwner = conn == identity.connectionToClient;
                    if ((!isOwner || includeOwner) && conn.isReady)
                    {
                        count++;
                        conn.Send(segment, channelId);
                    }
                }

                NetworkDiagnostics.OnSend(message, channelId, segment.Count, count);
            }
        }

        /// <summary>Send a message to only clients which are ready including the owner of the NetworkIdentity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReady<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage
        {
            SendToReady(identity, message, true, channelId);
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        static void SendToObservers<T>(NetworkIdentity identity, T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log("Server.SendToObservers id:" + typeof(T));
            if (identity == null || identity.observers == null || identity.observers.Count == 0)
                return;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                // pack message into byte[] once
                MessagePacking.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                foreach (NetworkConnection conn in identity.observers.Values)
                {
                    conn.Send(segment, channelId);
                }

                NetworkDiagnostics.OnSend(message, channelId, segment.Count, identity.observers.Count);
            }
        }

        /// <summary>Send this message to the player only</summary>
        [Obsolete("Use identity.connectionToClient.Send() instead! Previously Mirror needed this function internally, but not anymore.")]
        public static void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channels.Reliable)
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

        // transport events ////////////////////////////////////////////////////
        static void OnConnected(int connectionId)
        {
            // Debug.Log("Server accepted client:" + connectionId);

            // connectionId needs to be != 0 because 0 is reserved for local player
            // note that some transports like kcp generate connectionId by
            // hashing which can be < 0 as well, so we need to allow < 0!
            if (connectionId == 0)
            {
                Debug.LogError("Server.HandleConnect: invalid connectionId: " + connectionId + " . Needs to be != 0, because 0 is reserved for local player.");
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
                NetworkConnectionToClient conn = new NetworkConnectionToClient(connectionId, batching, batchInterval);
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
            OnConnectedEvent?.Invoke(conn);
        }

        static void OnDataReceived(int connectionId, ArraySegment<byte> data, int channelId)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                conn.TransportReceive(data, channelId);
            }
            else
            {
                Debug.LogError("HandleData Unknown connectionId:" + connectionId);
            }
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
            }
        }

        static void OnDisconnected(NetworkConnection conn)
        {
            OnDisconnectedEvent?.Invoke(conn);
            //Debug.Log("Server lost client:" + conn);
        }

        static void OnError(int connectionId, Exception exception)
        {
            // TODO Let's discuss how we will handle errors
            Debug.LogException(exception);
        }

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        [Obsolete("Use RegisterHandler(Action<NetworkConnection, T), requireAuthentication instead.")]
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            RegisterHandler<T>((_, value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler<T>((_, value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Unregister a handler for a message type T.</summary>
        public static void UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers.Remove(msgType);
        }

        /// <summary>Clears all registered message handlers.</summary>
        public static void ClearHandlers() => handlers.Clear();

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

        // disconnect //////////////////////////////////////////////////////////
        /// <summary>Disconnect all connections, including the local connection.</summary>
        public static void DisconnectAll()
        {
            DisconnectAllExternalConnections();
            localConnection = null;
            active = false;
        }

        /// <summary>Disconnect all currently connected clients except the local connection.</summary>
        public static void DisconnectAllExternalConnections()
        {
            // disconnect and remove all connections.
            // we can not use foreach here because if
            //   conn.Disconnect -> Transport.ServerDisconnect calls
            //   OnDisconnect -> NetworkServer.OnDisconnect(connectionId)
            // immediately then OnDisconnect would remove the connection while
            // we are iterating here.
            //   see also: https://github.com/vis2k/Mirror/issues/2357
            // this whole process should be simplified some day.
            // until then, let's copy .Values to avoid InvalidOperatinException.
            // note that this is only called when stopping the server, so the
            // copy is no performance problem.
            foreach (NetworkConnectionToClient conn in connections.Values.ToList())
            {
                conn.Disconnect();
                // call OnDisconnected unless local player in host mode
                if (conn.connectionId != NetworkConnection.LocalConnectionId)
                    OnDisconnected(conn);
            }
            connections.Clear();
        }

        [Obsolete("DisconnectAllConnections was renamed to DisconnectAllExternalConnections because that's what it does.")]
        public static void DisconnectAllConnections() => DisconnectAllExternalConnections();

        // add/remove/replace player ///////////////////////////////////////////
        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        // When a player is added for a connection, the client for that
        // connection is made ready automatically. The player object is
        // automatically spawned, so you do not need to call NetworkServer.Spawn
        // for that object. This function is used for "adding" a player, not for
        // "replacing" the player on a connection. If there is already a player
        // on this playerControllerId for this connection, this will fail.
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

            // special case,  we are in host mode,  set hasAuthority to true so that all overrides see it
            if (conn is LocalConnectionToClient)
            {
                identity.hasAuthority = true;
                NetworkClient.InternalAddPlayer(identity);
            }

            // set ready if not set yet
            SetClientReady(conn);

            // Debug.Log("Adding new playerGameObject object netId: " + identity.netId + " asset ID " + identity.assetId);

            Respawn(identity);
            return true;
        }

        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        // When a player is added for a connection, the client for that
        // connection is made ready automatically. The player object is
        // automatically spawned, so you do not need to call NetworkServer.Spawn
        // for that object. This function is used for "adding" a player, not for
        // "replacing" the player on a connection. If there is already a player
        // on this playerControllerId for this connection, this will fail.
        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return AddPlayerForConnection(conn, player);
        }

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        // This does NOT change the ready state of the connection, so it can
        // safely be used while changing scenes.
        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, bool keepAuthority = false)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }

            if (identity.connectionToClient != null && identity.connectionToClient != conn)
            {
                Debug.LogError("Cannot replace player for connection. New player is already owned by a different connection" + player);
                return false;
            }

            //NOTE: there can be an existing player
            //Debug.Log("NetworkServer ReplacePlayer");

            NetworkIdentity previousPlayer = conn.identity;

            conn.identity = identity;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.SetClientOwner(conn);

            // special case,  we are in host mode,  set hasAuthority to true so that all overrides see it
            if (conn is LocalConnectionToClient)
            {
                identity.hasAuthority = true;
                NetworkClient.InternalAddPlayer(identity);
            }

            // add connection to observers AFTER the playerController was set.
            // by definition, there is nothing to observe if there is no player
            // controller.
            //
            // IMPORTANT: do this in AddPlayerForConnection & ReplacePlayerForConnection!
            SpawnObserversForConnection(conn);

            // Debug.Log("Replacing playerGameObject object netId: " + player.GetComponent<NetworkIdentity>().netId + " asset ID " + player.GetComponent<NetworkIdentity>().assetId);

            Respawn(identity);

            if (!keepAuthority)
                previousPlayer.RemoveClientAuthority();

            return true;
        }

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        // This does NOT change the ready state of the connection, so it can
        // safely be used while changing scenes.
        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId, bool keepAuthority = false)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return ReplacePlayerForConnection(conn, player, keepAuthority);
        }

        // ready ///////////////////////////////////////////////////////////////
        /// <summary>Flags client connection as ready (=joined world).</summary>
        // When a client has signaled that it is ready, this method tells the
        // server that the client is ready to receive spawned objects and state
        // synchronization updates. This is usually called in a handler for the
        // SYSTEM_READY message. If there is not specific action a game needs to
        // take for this message, relying on the default ready handler function
        // is probably fine, so this call wont be needed.
        public static void SetClientReady(NetworkConnection conn)
        {
            // Debug.Log("SetClientReadyInternal for conn:" + conn);

            // set ready
            conn.isReady = true;

            // client is ready to start spawning objects
            if (conn.identity != null)
                SpawnObserversForConnection(conn);
        }

        /// <summary>Marks the client of the connection to be not-ready.</summary>
        // Clients that are not ready do not receive spawned objects or state
        // synchronization updates. They client can be made ready again by
        // calling SetClientReady().
        public static void SetClientNotReady(NetworkConnection conn)
        {
            if (conn.isReady)
            {
                // Debug.Log("PlayerNotReady " + conn);
                conn.isReady = false;
                conn.RemoveObservers();

                conn.Send(new NotReadyMessage());
            }
        }

        /// <summary>Marks all connected clients as no longer ready.</summary>
        // All clients will no longer be sent state synchronization updates. The
        // player's clients can call ClientManager.Ready() again to re-enter the
        // ready state. This is useful when switching scenes.
        public static void SetAllClientsNotReady()
        {
            foreach (NetworkConnectionToClient conn in connections.Values)
            {
                SetClientNotReady(conn);
            }
        }

        // default ready handler.
        static void OnClientReadyMessage(NetworkConnection conn, ReadyMessage msg)
        {
            // Debug.Log("Default handler for ready message from " + conn);
            SetClientReady(conn);
        }

        // show / hide for connection //////////////////////////////////////////
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

        /// <summary>Removes the player object from the connection</summary>
        // destroyServerObject: Indicates whether the server object should be destroyed
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
            //else Debug.Log($"Connection {conn} has no identity");
        }

        // remote calls ////////////////////////////////////////////////////////
        // Handle command from specific player, this could be one of multiple
        // players on a single client
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
            bool requiresAuthority = commandInfo.requiresAuthority;
            if (requiresAuthority && identity.connectionToClient != conn)
            {
                Debug.LogWarning("Command for object without authority [netId=" + msg.netId + "]");
                return;
            }

            // Debug.Log("OnCommandMessage for netId=" + msg.netId + " conn=" + conn);

            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, MirrorInvokeType.Command, networkReader, conn as NetworkConnectionToClient);
        }

        // spawning ////////////////////////////////////////////////////////////
        static ArraySegment<byte> CreateSpawnMessagePayload(bool isOwner, NetworkIdentity identity, PooledNetworkWriter ownerWriter, PooledNetworkWriter observersWriter)
        {
            // Only call OnSerializeAllSafely if there are NetworkBehaviours
            if (identity.NetworkBehaviours.Length == 0)
            {
                return default;
            }

            // serialize all components with initialState = true
            // (can be null if has none)
            identity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // convert to ArraySegment to avoid reader allocations
            // (need to handle null case too)
            ArraySegment<byte> ownerSegment = ownerWritten > 0 ? ownerWriter.ToArraySegment() : default;
            ArraySegment<byte> observersSegment = observersWritten > 0 ? observersWriter.ToArraySegment() : default;

            // use owner segment if 'conn' owns this identity, otherwise
            // use observers segment
            ArraySegment<byte> payload = isOwner ? ownerSegment : observersSegment;

            return payload;
        }

        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly) return;

            // Debug.Log("Server SendSpawnMessage: name=" + identity.name + " sceneId=" + identity.sceneId.ToString("X") + " netid=" + identity.netId);

            // one writer for owner, one for observers
            using (PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter(), observersWriter = NetworkWriterPool.GetWriter())
            {
                bool isOwner = identity.connectionToClient == conn;
                ArraySegment<byte> payload = CreateSpawnMessagePayload(isOwner, identity, ownerWriter, observersWriter);
                SpawnMessage message = new SpawnMessage
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
                conn.Send(message);
            }
        }

        static void SpawnObject(GameObject obj, NetworkConnection ownerConnection)
        {
            // verify if we an spawn this
            if (Utils.IsPrefab(obj))
            {
                Debug.LogError($"GameObject {obj.name} is a prefab, it can't be spawned. This will cause errors in builds.");
                return;
            }

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

            // special case to make sure hasAuthority is set
            // on start server in host mode
            if (ownerConnection is LocalConnectionToClient)
                identity.hasAuthority = true;

            identity.OnStartServer();

            // Debug.Log("SpawnObject instance ID " + identity.netId + " asset ID " + identity.assetId);

            RebuildObservers(identity, true);
        }

        /// <summary>Spawn the given game object on all clients which are ready.</summary>
        // This will cause a new object to be instantiated from the registered
        // prefab, or from a custom spawn function.
        public static void Spawn(GameObject obj, NetworkConnection ownerConnection = null)
        {
            SpawnObject(obj, ownerConnection);
        }

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        // This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.
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

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        // This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.
        public static void Spawn(GameObject obj, Guid assetId, NetworkConnection ownerConnection = null)
        {
            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            SpawnObject(obj, ownerConnection);
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

        /// <summary>Spawns NetworkIdentities in the scene on the server.</summary>
        // NetworkIdentity objects in a scene are disabled by default. Calling
        // SpawnObjects() causes these scene objects to be enabled and spawned.
        // It is like calling NetworkServer.Spawn() for each of them.
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

        static void SpawnObserversForConnection(NetworkConnection conn)
        {
            // Debug.Log("Spawning " + NetworkIdentity.spawned.Count + " objects for conn " + conn);

            if (!conn.isReady)
            {
                // client needs to finish initializing before we can spawn objects
                // otherwise it would not find them.
                return;
            }

            // let connection know that we are about to start spawning...
            conn.Send(new ObjectSpawnStartedMessage());

            // add connection to each nearby NetworkIdentity's observers, which
            // internally sends a spawn message for each one to the connection.
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // try with far away ones in ummorpg!
                if (identity.gameObject.activeSelf) //TODO this is different
                {
                    // Debug.Log("Sending spawn message for current server objects name='" + identity.name + "' netId=" + identity.netId + " sceneId=" + identity.sceneId.ToString("X"));

                    // we need to support three cases:
                    // - legacy system (identity has .visibility)
                    // - new system (networkserver has .aoi)
                    // - default case: no .visibility and no .aoi means add all
                    //   connections by default)
                    //
                    // ForceHidden/ForceShown overwrite all systems so check it
                    // first!

                    // ForceShown: add no matter what
                    if (identity.visible == Visibility.ForceShown)
                    {
                        identity.AddObserver(conn);
                    }
                    // ForceHidden: don't show no matter what
                    else if (identity.visible == Visibility.ForceHidden)
                    {
                        // do nothing
                    }
                    // default: legacy system / new system / no system support
                    else if (identity.visible == Visibility.Default)
                    {
                        // legacy system
#pragma warning disable 618
                        if (identity.visibility != null)
                        {
                            // call OnCheckObserver
                            if (identity.visibility.OnCheckObserver(conn))
                                identity.AddObserver(conn);
                        }
#pragma warning restore 618
                        // new system
                        else if (aoi != null)
                        {
                            // call OnCheckObserver
                            if (aoi.OnCheckObserver(identity, conn))
                                identity.AddObserver(conn);
                        }
                        // no system: add all observers by default
                        else
                        {
                            identity.AddObserver(conn);
                        }
                    }
                }
            }

            // let connection know that we finished spawning, so it can call
            // OnStartClient on each one (only after all were spawned, which
            // is how Unity's Start() function works too)
            conn.Send(new ObjectSpawnFinishedMessage());
        }

        /// <summary>This takes an object that has been spawned and un-spawns it.</summary>
        // The object will be removed from clients that it was spawned on, or
        // the custom spawn handler function on the client will be called for
        // the object.
        // Unlike when calling NetworkServer.Destroy(), on the server the object
        // will NOT be destroyed. This allows the server to re-use the object,
        // even spawn it again later.
        public static void UnSpawn(GameObject obj) => DestroyObject(obj, false);

        // destroy /////////////////////////////////////////////////////////////
        /// <summary>Destroys all of the connection's owned objects on the server.</summary>
        // This is used when a client disconnects, to remove the players for
        // that client. This also destroys non-player objects that have client
        // authority set for this connection.
        public static void DestroyPlayerForConnection(NetworkConnection conn)
        {
            // destroy all objects owned by this connection, including the player object
            conn.DestroyOwnedObjects();
            conn.identity = null;
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

            identity.ClearObservers();
            if (NetworkClient.active && localClientActive)
            {
                identity.OnStopClient();
            }

            identity.OnStopServer();

            // when unspawning, don't destroy the server's object
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

        static void DestroyObject(GameObject obj, bool destroyServerObject)
        {
            if (obj == null)
            {
                Debug.Log("NetworkServer DestroyObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, destroyServerObject);
            }
        }

        /// <summary>Destroys this object and corresponding objects on all clients.</summary>
        // In some cases it is useful to remove an object but not delete it on
        // the server. For that, use NetworkServer.UnSpawn() instead of
        // NetworkServer.Destroy().
        public static void Destroy(GameObject obj) => DestroyObject(obj, true);

        // interest management /////////////////////////////////////////////////
        // Helper function to add all server connections as observers.
        // This is used if none of the components provides their own
        // OnRebuildObservers function.
        internal static void AddAllReadyServerConnectionsToObservers(NetworkIdentity identity)
        {
            // add all server connections
            foreach (NetworkConnectionToClient conn in connections.Values)
            {
                // only if authenticated (don't send to people during logins)
                if (conn.isReady)
                    identity.AddObserver(conn);
            }

            // add local host connection (if any)
            if (localConnection != null && localConnection.isReady)
            {
                identity.AddObserver(localConnection);
            }
        }

        // allocate newObservers helper HashSet only once
        static readonly HashSet<NetworkConnection> newObservers = new HashSet<NetworkConnection>();

        // rebuild observers default method (no AOI) - adds all connections
        static void RebuildObserversDefault(NetworkIdentity identity, bool initialize)
        {
            // only add all connections when rebuilding the first time.
            // second time we just keep them without rebuilding anything.
            if (initialize)
            {
                // not force hidden?
                if (identity.visible != Visibility.ForceHidden)
                {
                    AddAllReadyServerConnectionsToObservers(identity);
                }
            }
        }

        // rebuild observers via interest management system
        static void RebuildObserversCustom(NetworkIdentity identity, bool initialize)
        {
            // clear newObservers hashset before using it
            newObservers.Clear();

            // not force hidden?
            if (identity.visible != Visibility.ForceHidden)
            {
                // obsolete legacy system support (for now)
#pragma warning disable 618
                if (identity.visibility != null)
                    identity.visibility.OnRebuildObservers(newObservers, initialize);
#pragma warning restore 618
                else
                    aoi.OnRebuildObservers(identity, newObservers, initialize);
            }

            // IMPORTANT: AFTER rebuilding add own player connection in any case
            // to ensure player always sees himself no matter what.
            // -> OnRebuildObservers might clear observers, so we need to add
            //    the player's own connection AFTER. 100% fail safe.
            // -> fixes https://github.com/vis2k/Mirror/issues/692 where a
            //    player might teleport out of the ProximityChecker's cast,
            //    losing the own connection as observer.
            if (identity.connectionToClient != null)
            {
                newObservers.Add(identity.connectionToClient);
            }

            bool changed = false;

            // add all newObservers that aren't in .observers yet
            foreach (NetworkConnection conn in newObservers)
            {
                // only add ready connections.
                // otherwise the player might not be in the world yet or anymore
                if (conn != null && conn.isReady)
                {
                    if (initialize || !identity.observers.ContainsKey(conn.connectionId))
                    {
                        // new observer
                        conn.AddToObserving(identity);
                        // Debug.Log("New Observer for " + gameObject + " " + conn);
                        changed = true;
                    }
                }
            }

            // remove all old .observers that aren't in newObservers anymore
            foreach (NetworkConnection conn in identity.observers.Values)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromObserving(identity, false);
                    // Debug.Log("Removed Observer for " + gameObject + " " + conn);
                    changed = true;
                }
            }

            // copy new observers to observers
            if (changed)
            {
                identity.observers.Clear();
                foreach (NetworkConnection conn in newObservers)
                {
                    if (conn != null && conn.isReady)
                        identity.observers.Add(conn.connectionId, conn);
                }
            }

            // special case for host mode: we use SetHostVisibility to hide
            // NetworkIdentities that aren't in observer range from host.
            // this is what games like Dota/Counter-Strike do too, where a host
            // does NOT see all players by default. they are in memory, but
            // hidden to the host player.
            //
            // this code is from UNET, it's a bit strange but it works:
            // * it hides newly connected identities in host mode
            //   => that part was the intended behaviour
            // * it hides ALL NetworkIdentities in host mode when the host
            //   connects but hasn't selected a character yet
            //   => this only works because we have no .localConnection != null
            //      check. at this stage, localConnection is null because
            //      StartHost starts the server first, then calls this code,
            //      then starts the client and sets .localConnection. so we can
            //      NOT add a null check without breaking host visibility here.
            // * it hides ALL NetworkIdentities in server-only mode because
            //   observers never contain the 'null' .localConnection
            //   => that was not intended, but let's keep it as it is so we
            //      don't break anything in host mode. it's way easier than
            //      iterating all identities in a special function in StartHost.
            if (initialize)
            {
                if (!newObservers.Contains(localConnection))
                {
                    // obsolete legacy system support (for now)
#pragma warning disable 618
                    if (identity.visibility != null)
                        identity.visibility.OnSetHostVisibility(false);
#pragma warning restore 618
                    else
                        identity.OnSetHostVisibility(false);
                }
            }
        }

        // RebuildObservers does a local rebuild for the NetworkIdentity.
        // This causes the set of players that can see this object to be rebuild.
        //
        // IMPORTANT:
        // => global rebuild would be more simple, BUT
        // => local rebuild is way faster for spawn/despawn because we can
        //    simply rebuild a select NetworkIdentity only
        // => having both .observers and .observing is necessary for local
        //    rebuilds
        //
        // in other words, this is the perfect solution even though it's not
        // completely simple (due to .observers & .observing)
        //
        // Mirror maintains .observing automatically in the background. best of
        // both worlds without any worrying now!
        public static void RebuildObservers(NetworkIdentity identity, bool initialize)
        {
            // observers are null until OnStartServer creates them
            if (identity.observers == null)
                return;

            // legacy proximitychecker support:
            // make sure user doesn't use both new and old system.
#pragma warning disable 618
            if (aoi != null & identity.visibility != null)
            {
                Debug.LogError($"RebuildObservers: {identity.name} has {identity.visibility.GetType()} component but there is also a global {aoi.GetType()} component. Can't use both systems at the same time!");
                return;
            }
#pragma warning restore 618

            // if there is no interest management system,
            // or if 'force shown' then add all connections
#pragma warning disable 618
            if ((aoi == null && identity.visibility == null) ||
                identity.visible == Visibility.ForceShown)
#pragma warning restore 618
            {
                RebuildObserversDefault(identity, initialize);
            }
            // otherwise let interest management system rebuild
            else
            {
                RebuildObserversCustom(identity, initialize);
            }
        }

        // update //////////////////////////////////////////////////////////////
        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate()
        {
            // process all incoming messages first before updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ServerEarlyUpdate();
        }

        // cache NetworkIdentity serializations
        // Update() shouldn't serialize multiple times for multiple connections
        struct Serialization
        {
            public PooledNetworkWriter ownerWriter;
            public PooledNetworkWriter observersWriter;
            // TODO there is probably a more simple way later
            public int ownerWritten;
            public int observersWritten;
        }
        static Dictionary<NetworkIdentity, Serialization> serializations =
            new Dictionary<NetworkIdentity, Serialization>();

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkLateUpdate()
        {
            // only process spawned & connections if active
            if (active)
            {
                // go through all connections
                foreach (NetworkConnectionToClient connection in connections.Values)
                {
                    // check for inactivity
                    if (disconnectInactiveConnections &&
                        !connection.IsAlive(disconnectInactiveTimeout))
                    {
                        Debug.LogWarning($"Disconnecting {connection} for inactivity!");
                        connection.Disconnect();
                        continue;
                    }

                    // has this connection joined the world yet?
                    // for each READY connection:
                    //   pull in UpdateVarsMessage for each entity it observes
                    if (connection.isReady)
                    {
                        // for each entity that this connection is seeing
                        foreach (NetworkIdentity identity in connection.observing)
                        {
                            // make sure it's not null or destroyed.
                            // (which can happen if someone uses
                            //  GameObject.Destroy instead of
                            //  NetworkServer.Destroy)
                            if (identity != null)
                            {
                                // multiple connections might be observed by the
                                // same NetworkIdentity, but we don't want to
                                // serialize them multiple times. look it up first.
                                //
                                // IMPORTANT: don't forget to return them to pool!
                                // TODO make this easier later. for now aim for
                                //      feature parity to not break projects.
                                if (!serializations.ContainsKey(identity))
                                {
                                    // serialize all the dirty components.
                                    // one version for owner, one for observers.
                                    PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter();
                                    PooledNetworkWriter observersWriter = NetworkWriterPool.GetWriter();
                                    identity.OnSerializeAllSafely(false, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);
                                    serializations[identity] = new Serialization
                                    {
                                        ownerWriter = ownerWriter,
                                        observersWriter = observersWriter,
                                        ownerWritten = ownerWritten,
                                        observersWritten = observersWritten
                                    };

                                    // clear dirty bits only for the components that we serialized
                                    // DO NOT clean ALL component's dirty bits, because
                                    // components can have different syncIntervals and we don't
                                    // want to reset dirty bits for the ones that were not
                                    // synced yet.
                                    // (we serialized only the IsDirty() components, or all of
                                    //  them if initialState. clearing the dirty ones is enough.)
                                    //
                                    // NOTE: this is what we did before push->pull
                                    //       broadcasting. let's keep doing this for
                                    //       feature parity to not break anyone's project.
                                    //       TODO make this more simple / unnecessary later.
                                    identity.ClearDirtyComponentsDirtyBits();
                                }

                                // get serialization
                                Serialization serialization = serializations[identity];

                                // is this entity owned by this connection?
                                bool owned = identity.connectionToClient == connection;

                                // send serialized data
                                // owner writer if owned
                                if (owned)
                                {
                                    // was it dirty / did we actually serialize anything?
                                    if (serialization.ownerWritten > 0)
                                    {
                                        UpdateVarsMessage message = new UpdateVarsMessage
                                        {
                                            netId = identity.netId,
                                            payload = serialization.ownerWriter.ToArraySegment()
                                        };
                                        connection.Send(message);
                                    }
                                }
                                // observers writer if not owned
                                else
                                {
                                    // was it dirty / did we actually serialize anything?
                                    if (serialization.observersWritten > 0)
                                    {
                                        UpdateVarsMessage message = new UpdateVarsMessage
                                        {
                                            netId = identity.netId,
                                            payload = serialization.observersWriter.ToArraySegment()
                                        };
                                        connection.Send(message);
                                    }
                                }
                            }
                            // spawned list should have no null entries because we
                            // always call Remove in OnObjectDestroy everywhere.
                            // if it does have null then someone used
                            // GameObject.Destroy instead of NetworkServer.Destroy.
                            else Debug.LogWarning("Found 'null' entry in observing list for connectionId=" + connection.connectionId + ". Please call NetworkServer.Destroy to destroy networked objects. Don't use GameObject.Destroy.");
                        }
                    }

                    // update connection to flush out batched messages
                    connection.Update();
                }

                // return serialized writers to pool, clear set
                // TODO this is for feature parity before push->pull change.
                //      make this more simple / unnecessary later.
                foreach (Serialization entry in serializations.Values)
                {
                    NetworkWriterPool.Recycle(entry.ownerWriter);
                    NetworkWriterPool.Recycle(entry.observersWriter);
                }
                serializations.Clear();

                // TODO this unfortunately means we still need to iterate ALL
                //      spawned and not just the ones with observers. figure
                //      out a way to get rid of this.
                //
                // TODO clear dirty bits when removing the last observer instead!
                //      no need to do it for ALL entities ALL the time.
                //
                // for each spawned:
                //   clear dirty bits if it has no observers.
                //   we did this before push->pull broadcasting so let's keep
                //   doing this for now.
                foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
                {
                    if (identity.observers == null || identity.observers.Count == 0)
                    {
                        // clear all component's dirty bits.
                        // it would be spawned on new observers anyway.
                        identity.ClearAllComponentsDirtyBits();
                    }
                }
            }

            // process all incoming messages after updating the world
            // (even if not active. still want to process disconnects etc.)
            if (Transport.activeTransport != null)
                Transport.activeTransport.ServerLateUpdate();
        }

        // obsolete to not break people's projects. Update was public.
        [Obsolete("NetworkServer.Update is now called internally from our custom update loop. No need to call Update manually anymore.")]
        public static void Update() => NetworkLateUpdate();
    }
}
