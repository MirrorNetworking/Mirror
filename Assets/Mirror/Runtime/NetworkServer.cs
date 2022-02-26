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
        internal static Dictionary<ushort, NetworkMessageDelegate> handlers =
            new Dictionary<ushort, NetworkMessageDelegate>();

        /// <summary>All spawned NetworkIdentities by netId.</summary>
        // server sees ALL spawned ones.
        public static readonly Dictionary<uint, NetworkIdentity> spawned =
            new Dictionary<uint, NetworkIdentity>();

        /// <summary>Single player mode can use dontListen to not accept incoming connections</summary>
        // see also: https://github.com/vis2k/Mirror/pull/2595
        public static bool dontListen;

        /// <summary>active checks if the server has been started</summary>
        public static bool active { get; internal set; }

        // scene loading
        public static bool isLoadingScene;

        // interest management component (optional)
        // by default, everyone observes everyone
        public static InterestManagement aoi;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action<NetworkConnectionToClient> OnConnectedEvent;
        public static Action<NetworkConnectionToClient> OnDisconnectedEvent;
        public static Action<NetworkConnectionToClient, Exception> OnErrorEvent;

        // initialization / shutdown ///////////////////////////////////////////
        static void Initialize()
        {
            if (initialized)
                return;

            // Debug.Log($"NetworkServer Created version {Version.Current}");

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();

            // reset Interest Management so that rebuild intervals
            // start at 0 when starting again.
            if (aoi != null) aoi.Reset();

            // reset NetworkTime
            NetworkTime.ResetStatics();

            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkServer.Listen, If you are calling Listen manually then make sure to set 'Transport.activeTransport' first");
            AddTransportHandlers();

            initialized = true;
        }

        static void AddTransportHandlers()
        {
            Transport.activeTransport.OnServerConnected = OnTransportConnected;
            Transport.activeTransport.OnServerDataReceived = OnTransportData;
            Transport.activeTransport.OnServerDisconnected = OnTransportDisconnected;
            Transport.activeTransport.OnServerError = OnError;
        }

        // calls OnStartClient for all SERVER objects in host mode once.
        // client doesn't get spawn messages for those, so need to call manually.
        public static void ActivateHostScene()
        {
            foreach (NetworkIdentity identity in spawned.Values)
            {
                if (!identity.isClient)
                {
                    // Debug.Log($"ActivateHostScene {identity.netId} {identity}");
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
                //Debug.Log("Server started listening");
            }

            active = true;
            RegisterMessageHandlers();
        }

        // Note: NetworkClient.DestroyAllClientObjects does the same on client.
        static void CleanupSpawned()
        {
            // iterate a COPY of spawned.
            // DestroyObject removes them from the original collection.
            // removing while iterating is not allowed.
            foreach (NetworkIdentity identity in spawned.Values.ToList())
            {
                if (identity != null)
                {
                    // scene object
                    if (identity.sceneId != 0)
                    {
                        // spawned scene objects are unspawned and reset.
                        // afterwards we disable them again.
                        // (they always stay in the scene, we don't destroy them)
                        DestroyObject(identity, DestroyMode.Reset);
                        identity.gameObject.SetActive(false);
                    }
                    // spawned prefabs
                    else
                    {
                        // spawned prefabs are unspawned and destroyed.
                        DestroyObject(identity, DestroyMode.Destroy);
                    }
                }
            }

            spawned.Clear();
        }

        /// <summary>Shuts down the server and disconnects all clients</summary>
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Shutdown()
        {
            if (initialized)
            {
                DisconnectAll();

                // stop the server.
                // we do NOT call Transport.Shutdown, because someone only
                // called NetworkServer.Shutdown. we can't assume that the
                // client is supposed to be shut down too!
                //
                // NOTE: stop no matter what, even if 'dontListen':
                //       someone might enabled dontListen at runtime.
                //       but we still need to stop the server.
                //       fixes https://github.com/vis2k/Mirror/issues/2536
                Transport.activeTransport.ServerStop();
                initialized = false;
            }

            // Reset all statics here....
            dontListen = false;
            active = false;
            isLoadingScene = false;

            localConnection = null;

            connections.Clear();
            connectionsCopy.Clear();
            handlers.Clear();
            newObservers.Clear();

            // this calls spawned.Clear()
            CleanupSpawned();

            // sets nextNetworkId to 1
            // sets clientAuthorityCallback to null
            // sets previousLocalPlayer to null
            NetworkIdentity.ResetStatics();

            // clear events. someone might have hooked into them before, but
            // we don't want to use those hooks after Shutdown anymore.
            OnConnectedEvent = null;
            OnDisconnectedEvent = null;
            OnErrorEvent = null;

            if (aoi != null) aoi.Reset();
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
                return true;
            }
            // already a connection with this id
            return false;
        }

        /// <summary>Removes a connection by connectionId. Returns true if removed.</summary>
        public static bool RemoveConnection(int connectionId) =>
            connections.Remove(connectionId);

        // called by LocalClient to add itself. don't call directly.
        // TODO consider internal setter instead?
        internal static void SetLocalConnection(LocalConnectionToClient conn)
        {
            if (localConnection != null)
            {
                Debug.LogError("Local Connection already exists");
                return;
            }

            localConnection = conn;
        }

        // removes local connection to client
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
        // DEPRECATED 2022-02-05
        [Obsolete("Use !HasExternalConnections() instead of NoExternalConnections() to avoid double negatives.")]
        public static bool NoExternalConnections() => !HasExternalConnections();

        /// <summary>True if we have external connections (that are not host)</summary>
        public static bool HasExternalConnections()
        {
            // any connections?
            if (connections.Count > 0)
            {
                // only host connection?
                if (connections.Count == 1 && localConnection != null)
                    return false;

                // otherwise we have real external connections
                return true;
            }
            return false;
        }

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

            // Debug.Log($"Server.SendToAll {typeof(T)}");
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

        // this is like SendToReadyObservers - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        static void SendToObservers<T>(NetworkIdentity identity, T message, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log($"Server.SendToObservers {typeof(T)}");
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

        /// <summary>Send a message to only clients which are ready with option to include the owner of the object identity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log($"Server.SendToReady {typeof(T)}");
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

        // Deprecated 2021-09-19
        [Obsolete("SendToReady(identity, message, ...) was renamed to SendToReadyObservers because that's what it does.")]
        public static void SendToReady<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage =>
                SendToReadyObservers(identity, message, includeOwner, channelId);

        /// <summary>Send a message to only clients which are ready including the owner of the NetworkIdentity</summary>
        // TODO put rpcs into NetworkServer.Update WorldState packet, then finally remove SendToReady!
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage
        {
            SendToReadyObservers(identity, message, true, channelId);
        }

        // Deprecated 2021-09-19
        [Obsolete("SendToReady(identity, message, ...) was renamed to SendToReadyObservers because that's what it does.")]
        public static void SendToReady<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage =>
                SendToReadyObservers(identity, message, channelId);

        // transport events ////////////////////////////////////////////////////
        // called by transport
        static void OnTransportConnected(int connectionId)
        {
            // Debug.Log($"Server accepted client:{connectionId}");

            // connectionId needs to be != 0 because 0 is reserved for local player
            // note that some transports like kcp generate connectionId by
            // hashing which can be < 0 as well, so we need to allow < 0!
            if (connectionId == 0)
            {
                Debug.LogError($"Server.HandleConnect: invalid connectionId: {connectionId} . Needs to be != 0, because 0 is reserved for local player.");
                Transport.activeTransport.ServerDisconnect(connectionId);
                return;
            }

            // connectionId not in use yet?
            if (connections.ContainsKey(connectionId))
            {
                Transport.activeTransport.ServerDisconnect(connectionId);
                // Debug.Log($"Server connectionId {connectionId} already in use...kicked client");
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
                // Debug.Log($"Server full, kicked client {connectionId}");
            }
        }

        internal static void OnConnected(NetworkConnectionToClient conn)
        {
            // Debug.Log($"Server accepted client:{conn}");

            // add connection and invoke connected event
            AddConnection(conn);
            OnConnectedEvent?.Invoke(conn);
        }

        static bool UnpackAndInvoke(NetworkConnectionToClient connection, NetworkReader reader, int channelId)
        {
            if (MessagePacking.Unpack(reader, out ushort msgType))
            {
                // try to invoke the handler for that message
                if (handlers.TryGetValue(msgType, out NetworkMessageDelegate handler))
                {
                    handler.Invoke(connection, reader, channelId);
                    connection.lastMessageTime = Time.time;
                    return true;
                }
                else
                {
                    // message in a batch are NOT length prefixed to save bandwidth.
                    // every message needs to be handled and read until the end.
                    // otherwise it would overlap into the next message.
                    // => need to warn and disconnect to avoid undefined behaviour.
                    // => WARNING, not error. can happen if attacker sends random data.
                    Debug.LogWarning($"Unknown message id: {msgType} for connection: {connection}. This can happen if no handler was registered for this message.");
                    // simply return false. caller is responsible for disconnecting.
                    //connection.Disconnect();
                    return false;
                }
            }
            else
            {
                // => WARNING, not error. can happen if attacker sends random data.
                Debug.LogWarning($"Invalid message header for connection: {connection}.");
                // simply return false. caller is responsible for disconnecting.
                //connection.Disconnect();
                return false;
            }
        }

        // called by transport
        internal static void OnTransportData(int connectionId, ArraySegment<byte> data, int channelId)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnectionToClient connection))
            {
                // client might batch multiple messages into one packet.
                // feed it to the Unbatcher.
                // NOTE: we don't need to associate a channelId because we
                //       always process all messages in the batch.
                if (!connection.unbatcher.AddBatch(data))
                {
                    Debug.LogWarning($"NetworkServer: received Message was too short (messages should start with message id)");
                    connection.Disconnect();
                    return;
                }

                // process all messages in the batch.
                // only while NOT loading a scene.
                // if we get a scene change message, then we need to stop
                // processing. otherwise we might apply them to the old scene.
                // => fixes https://github.com/vis2k/Mirror/issues/2651
                //
                // NOTE: if scene starts loading, then the rest of the batch
                //       would only be processed when OnTransportData is called
                //       the next time.
                //       => consider moving processing to NetworkEarlyUpdate.
                while (!isLoadingScene &&
                       connection.unbatcher.GetNextMessage(out NetworkReader reader, out double remoteTimestamp))
                {
                    // enough to read at least header size?
                    if (reader.Remaining >= MessagePacking.HeaderSize)
                    {
                        // make remoteTimeStamp available to the user
                        connection.remoteTimeStamp = remoteTimestamp;

                        // handle message
                        if (!UnpackAndInvoke(connection, reader, channelId))
                        {
                            // warn, disconnect and return if failed
                            // -> warning because attackers might send random data
                            // -> messages in a batch aren't length prefixed.
                            //    failing to read one would cause undefined
                            //    behaviour for every message afterwards.
                            //    so we need to disconnect.
                            // -> return to avoid the below unbatches.count error.
                            //    we already disconnected and handled it.
                            Debug.LogWarning($"NetworkServer: failed to unpack and invoke message. Disconnecting {connectionId}.");
                            connection.Disconnect();
                            return;
                        }
                    }
                    // otherwise disconnect
                    else
                    {
                        // WARNING, not error. can happen if attacker sends random data.
                        Debug.LogWarning($"NetworkServer: received Message was too short (messages should start with message id). Disconnecting {connectionId}");
                        connection.Disconnect();
                        return;
                    }
                }

                // if we weren't interrupted by a scene change,
                // then all batched messages should have been processed now.
                // otherwise batches would silently grow.
                // we need to log an error to avoid debugging hell.
                //
                // EXAMPLE: https://github.com/vis2k/Mirror/issues/2882
                // -> UnpackAndInvoke silently returned because no handler for id
                // -> Reader would never be read past the end
                // -> Batch would never be retired because end is never reached
                //
                // NOTE: prefixing every message in a batch with a length would
                //       avoid ever not reading to the end. for extra bandwidth.
                //
                // IMPORTANT: always keep this check to detect memory leaks.
                //            this took half a day to debug last time.
                if (!isLoadingScene && connection.unbatcher.BatchesCount > 0)
                {
                    Debug.LogError($"Still had {connection.unbatcher.BatchesCount} batches remaining after processing, even though processing was not interrupted by a scene change. This should never happen, as it would cause ever growing batches.\nPossible reasons:\n* A message didn't deserialize as much as it serialized\n*There was no message handler for a message id, so the reader wasn't read until the end.");
                }
            }
            else Debug.LogError($"HandleData Unknown connectionId:{connectionId}");
        }

        // called by transport
        // IMPORTANT: often times when disconnecting, we call this from Mirror
        //            too because we want to remove the connection and handle
        //            the disconnect immediately.
        //            => which is fine as long as we guarantee it only runs once
        //            => which we do by removing the connection!
        internal static void OnTransportDisconnected(int connectionId)
        {
            // Debug.Log($"Server disconnect client:{connectionId}");
            if (connections.TryGetValue(connectionId, out NetworkConnectionToClient conn))
            {
                RemoveConnection(connectionId);
                // Debug.Log($"Server lost client:{connectionId}");

                // NetworkManager hooks into OnDisconnectedEvent to make
                // DestroyPlayerForConnection(conn) optional, e.g. for PvP MMOs
                // where players shouldn't be able to escape combat instantly.
                if (OnDisconnectedEvent != null)
                {
                    OnDisconnectedEvent.Invoke(conn);
                }
                // if nobody hooked into it, then simply call DestroyPlayerForConnection
                else
                {
                    DestroyPlayerForConnection(conn);
                }
            }
        }

        static void OnError(int connectionId, Exception exception)
        {
            Debug.LogException(exception);
            // try get connection. passes null otherwise.
            connections.TryGetValue(connectionId, out NetworkConnectionToClient conn);
            OnErrorEvent?.Invoke(conn, exception);
        }

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        // TODO obsolete this some day to always use the channelId version.
        //      all handlers in this version are wrapped with 1 extra action.
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        // This version passes channelId to the handler.
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = MessagePacking.GetId<T>();
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
            ushort msgType = MessagePacking.GetId<T>();
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
        // synchronous: handles disconnect events and cleans up fully before returning!
        public static void DisconnectAll()
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
                // disconnect via connection->transport
                conn.Disconnect();

                // we want this function to be synchronous: handle disconnect
                // events and clean up fully before returning.
                // -> OnTransportDisconnected can safely be called without
                //    waiting for the Transport's callback.
                // -> it has checks to only run once.

                // call OnDisconnected unless local player in host mod
                // TODO unnecessary check?
                if (conn.connectionId != NetworkConnection.LocalConnectionId)
                    OnTransportDisconnected(conn.connectionId);
            }

            // cleanup
            connections.Clear();
            localConnection = null;
            active = false;
        }

        // add/remove/replace player ///////////////////////////////////////////
        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        // When a player is added for a connection, the client for that
        // connection is made ready automatically. The player object is
        // automatically spawned, so you do not need to call NetworkServer.Spawn
        // for that object. This function is used for "adding" a player, not for
        // "replacing" the player on a connection. If there is already a player
        // on this playerControllerId for this connection, this will fail.
        public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogWarning($"AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to {player}");
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

            // Debug.Log($"Adding new playerGameObject object netId: {identity.netId} asset ID: {identity.assetId}");

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
        public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player, Guid assetId)
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
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, bool keepAuthority = false)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to {player}");
                return false;
            }

            if (identity.connectionToClient != null && identity.connectionToClient != conn)
            {
                Debug.LogError($"Cannot replace player for connection. New player is already owned by a different connection{player}");
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

            //Debug.Log($"Replacing playerGameObject object netId:{player.GetComponent<NetworkIdentity>().netId} asset ID {player.GetComponent<NetworkIdentity>().assetId}");

            Respawn(identity);

            if (keepAuthority)
                // This needs to be sent to clear isLocalPlayer on
                // client while keeping hasAuthority true
                SendChangeOwnerMessage(previousPlayer, conn);
            else
                // This clears both isLocalPlayer and hasAuthority on client
                previousPlayer.RemoveClientAuthority();

            return true;
        }

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        // This does NOT change the ready state of the connection, so it can
        // safely be used while changing scenes.
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, Guid assetId, bool keepAuthority = false)
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
        public static void SetClientReady(NetworkConnectionToClient conn)
        {
            // Debug.Log($"SetClientReadyInternal for conn:{conn}");

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
        public static void SetClientNotReady(NetworkConnectionToClient conn)
        {
            conn.isReady = false;
            conn.RemoveFromObservingsObservers();
            conn.Send(new NotReadyMessage());
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
        static void OnClientReadyMessage(NetworkConnectionToClient conn, ReadyMessage msg)
        {
            // Debug.Log($"Default handler for ready message from {conn}");
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
        static void OnCommandMessage(NetworkConnectionToClient conn, CommandMessage msg, int channelId)
        {
            if (!conn.isReady)
            {
                // Clients may be set NotReady due to scene change or other game logic by user, e.g. respawning.
                // Ignore commands that may have been in flight before client received NotReadyMessage message.
                // Unreliable messages may be out of order, so don't spam warnings for those.
                if (channelId == Channels.Reliable)
                    Debug.LogWarning("Command received while client is not ready.\nThis may be ignored if client intentionally set NotReady.");
                return;
            }

            if (!spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                // over reliable channel, commands should always come after spawn.
                // over unreliable, they might come in before the object was spawned.
                // for example, NetworkTransform.
                // let's not spam the console for unreliable out of order messages.
                if (channelId == Channels.Reliable)
                    Debug.LogWarning($"Spawned object not found when handling Command message [netId={msg.netId}]");
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            bool requiresAuthority = RemoteProcedureCalls.CommandRequiresAuthority(msg.functionHash);
            if (requiresAuthority && identity.connectionToClient != conn)
            {
                Debug.LogWarning($"Command for object without authority [netId={msg.netId}]");
                return;
            }

            // Debug.Log($"OnCommandMessage for netId:{msg.netId} conn:{conn}");

            using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, RemoteCallType.Command, networkReader, conn as NetworkConnectionToClient);
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
            identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // convert to ArraySegment to avoid reader allocations
            // if nothing was written, .ToArraySegment returns an empty segment.
            ArraySegment<byte> ownerSegment = ownerWriter.ToArraySegment();
            ArraySegment<byte> observersSegment = observersWriter.ToArraySegment();

            // use owner segment if 'conn' owns this identity, otherwise
            // use observers segment
            ArraySegment<byte> payload = isOwner ? ownerSegment : observersSegment;

            return payload;
        }

        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly) return;

            //Debug.Log($"Server SendSpawnMessage: name:{identity.name} sceneId:{identity.sceneId:X} netid:{identity.netId}");

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
                    payload = payload
                };
                conn.Send(message);
            }
        }

        internal static void SendChangeOwnerMessage(NetworkIdentity identity, NetworkConnectionToClient conn)
        {
            // Don't send if identity isn't spawned or only exists on server
            if (identity.netId == 0 || identity.serverOnly) return;

            // Don't send if conn doesn't have the identity spawned yet
            // May be excluded from the client by interest management
            if (!conn.observing.Contains(identity)) return;

            //Debug.Log($"Server SendChangeOwnerMessage: name={identity.name} netid={identity.netId}");

            conn.Send(new ChangeOwnerMessage
            {
                netId = identity.netId,
                isOwner = identity.connectionToClient == conn,
                isLocalPlayer = conn.identity == identity
            });
        }

        static void SpawnObject(GameObject obj, NetworkConnection ownerConnection)
        {
            // verify if we can spawn this
            if (Utils.IsPrefab(obj))
            {
                Debug.LogError($"GameObject {obj.name} is a prefab, it can't be spawned. Instantiate it first.");
                return;
            }

            if (!active)
            {
                Debug.LogError($"SpawnObject for {obj}, NetworkServer is not active. Cannot spawn objects without an active server.");
                return;
            }

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"SpawnObject {obj} has no NetworkIdentity. Please add a NetworkIdentity to {obj}");
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

            // Debug.Log($"SpawnObject instance ID {identity.netId} asset ID {identity.assetId}");

            if (aoi)
            {
                // This calls user code which might throw exceptions
                // We don't want this to leave us in bad state
                try
                {
                    aoi.OnSpawned(identity);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

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

            // first pass: activate all scene objects
            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                {
                    // Debug.Log($"SpawnObjects sceneId:{identity.sceneId:X} name:{identity.gameObject.name}");
                    identity.gameObject.SetActive(true);

                    // fix https://github.com/vis2k/Mirror/issues/2778:
                    // -> SetActive(true) does NOT call Awake() if the parent
                    //    is inactive
                    // -> we need Awake() to initialize NetworkBehaviours[] etc.
                    //    because our second pass below spawns and works with it
                    // => detect this situation and manually call Awake for
                    //    proper initialization
                    if (!identity.gameObject.activeInHierarchy)
                        identity.Awake();
                }
            }

            // second pass: spawn all scene objects
            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                    // pass connection so that authority is not lost when server loads a scene
                    // https://github.com/vis2k/Mirror/pull/2987
                    Spawn(identity.gameObject, identity.connectionToClient);
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

        static void SpawnObserversForConnection(NetworkConnectionToClient conn)
        {
            //Debug.Log($"Spawning {spawned.Count} objects for conn {conn}");

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
            foreach (NetworkIdentity identity in spawned.Values)
            {
                // try with far away ones in ummorpg!
                if (identity.gameObject.activeSelf) //TODO this is different
                {
                    //Debug.Log($"Sending spawn message for current server objects name:{identity.name} netId:{identity.netId} sceneId:{identity.sceneId:X}");

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
                        // aoi system
                        if (aoi != null)
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
        public static void UnSpawn(GameObject obj) => DestroyObject(obj, DestroyMode.Reset);

        // destroy /////////////////////////////////////////////////////////////
        /// <summary>Destroys all of the connection's owned objects on the server.</summary>
        // This is used when a client disconnects, to remove the players for
        // that client. This also destroys non-player objects that have client
        // authority set for this connection.
        public static void DestroyPlayerForConnection(NetworkConnectionToClient conn)
        {
            // destroy all objects owned by this connection, including the player object
            conn.DestroyOwnedObjects();
            // remove connection from all of its observing entities observers
            // fixes https://github.com/vis2k/Mirror/issues/2737
            // -> cleaning those up in NetworkConnection.Disconnect is NOT enough
            //    because voluntary disconnects from the other end don't call
            //    NetworkConnectionn.Disconnect()
            conn.RemoveFromObservingsObservers();
            conn.identity = null;
        }

        // sometimes we want to GameObject.Destroy it.
        // sometimes we want to just unspawn on clients and .Reset() it on server.
        // => 'bool destroy' isn't obvious enough. it's really destroy OR reset!
        enum DestroyMode { Destroy, Reset }

        static void DestroyObject(NetworkIdentity identity, DestroyMode mode)
        {
            // Debug.Log($"DestroyObject instance:{identity.netId}");

            // only call OnRebuildObservers while active,
            // not while shutting down
            // (https://github.com/vis2k/Mirror/issues/2977)
            if (active && aoi)
            {
                // This calls user code which might throw exceptions
                // We don't want this to leave us in bad state
                try
                {
                    aoi.OnDestroyed(identity);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // remove from NetworkServer (this) dictionary
            spawned.Remove(identity.netId);

            identity.connectionToClient?.RemoveOwnedObject(identity);

            // send object destroy message to all observers, clear observers
            SendToObservers(identity, new ObjectDestroyMessage{netId = identity.netId});
            identity.ClearObservers();

            // in host mode, call OnStopClient/OnStopLocalPlayer manually
            if (NetworkClient.active && localClientActive)
            {
                if (identity.isLocalPlayer)
                    identity.OnStopLocalPlayer();

                identity.OnStopClient();
                // The object may have been spawned with host client ownership,
                // e.g. a pet so we need to clear hasAuthority and call
                // NotifyAuthority which invokes OnStopAuthority if hasAuthority.
                identity.hasAuthority = false;
                identity.NotifyAuthority();

                // remove from NetworkClient dictionary
                NetworkClient.spawned.Remove(identity.netId);
            }

            // we are on the server. call OnStopServer.
            identity.OnStopServer();

            // are we supposed to GameObject.Destroy() it completely?
            if (mode == DestroyMode.Destroy)
            {
                identity.destroyCalled = true;

                // Destroy if application is running
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(identity.gameObject);
                }
                // Destroy can't be used in Editor during tests. use DestroyImmediate.
                else
                {
                    GameObject.DestroyImmediate(identity.gameObject);
                }
            }
            // otherwise simply .Reset() and set inactive again
            else if (mode == DestroyMode.Reset)
            {
                identity.Reset();
            }
        }

        static void DestroyObject(GameObject obj, DestroyMode mode)
        {
            if (obj == null)
            {
                Debug.Log("NetworkServer DestroyObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, mode);
            }
        }

        /// <summary>Destroys this object and corresponding objects on all clients.</summary>
        // In some cases it is useful to remove an object but not delete it on
        // the server. For that, use NetworkServer.UnSpawn() instead of
        // NetworkServer.Destroy().
        public static void Destroy(GameObject obj) => DestroyObject(obj, DestroyMode.Destroy);

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
        // internal for tests
        internal static readonly HashSet<NetworkConnectionToClient> newObservers = new HashSet<NetworkConnectionToClient>();

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
                aoi.OnRebuildObservers(identity, newObservers);
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
            foreach (NetworkConnectionToClient conn in newObservers)
            {
                // only add ready connections.
                // otherwise the player might not be in the world yet or anymore
                if (conn != null && conn.isReady)
                {
                    if (initialize || !identity.observers.ContainsKey(conn.connectionId))
                    {
                        // new observer
                        conn.AddToObserving(identity);
                        // Debug.Log($"New Observer for {gameObject} {conn}");
                        changed = true;
                    }
                }
            }

            // remove all old .observers that aren't in newObservers anymore
            foreach (NetworkConnectionToClient conn in identity.observers.Values)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromObserving(identity, false);
                    // Debug.Log($"Removed Observer for {gameObjec} {conn}");
                    changed = true;
                }
            }

            // copy new observers to observers
            if (changed)
            {
                identity.observers.Clear();
                foreach (NetworkConnectionToClient conn in newObservers)
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
                    if (aoi != null)
                        aoi.SetHostVisibility(identity, false);
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

            // if there is no interest management system,
            // or if 'force shown' then add all connections
            if (aoi == null || identity.visible == Visibility.ForceShown)
            {
                RebuildObserversDefault(identity, initialize);
            }
            // otherwise let interest management system rebuild
            else
            {
                RebuildObserversCustom(identity, initialize);
            }
        }

        // broadcasting ////////////////////////////////////////////////////////
        // helper function to get the right serialization for a connection
        static NetworkWriter GetEntitySerializationForConnection(NetworkIdentity identity, NetworkConnectionToClient connection)
        {
            // get serialization for this entity (cached)
            // IMPORTANT: int tick avoids floating point inaccuracy over days/weeks
            NetworkIdentitySerialization serialization = identity.GetSerializationAtTick(Time.frameCount);

            // is this entity owned by this connection?
            bool owned = identity.connectionToClient == connection;

            // send serialized data
            // owner writer if owned
            if (owned)
            {
                // was it dirty / did we actually serialize anything?
                if (serialization.ownerWriter.Position > 0)
                    return serialization.ownerWriter;
            }
            // observers writer if not owned
            else
            {
                // was it dirty / did we actually serialize anything?
                if (serialization.observersWriter.Position > 0)
                    return serialization.observersWriter;
            }

            // nothing was serialized
            return null;
        }

        // helper function to broadcast the world to a connection
        static void BroadcastToConnection(NetworkConnectionToClient connection)
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
                    // get serialization for this entity viewed by this connection
                    // (if anything was serialized this time)
                    NetworkWriter serialization = GetEntitySerializationForConnection(identity, connection);
                    if (serialization != null)
                    {
                        EntityStateMessage message = new EntityStateMessage
                        {
                            netId = identity.netId,
                            payload = serialization.ToArraySegment()
                        };
                        connection.Send(message);
                    }
                }
                // spawned list should have no null entries because we
                // always call Remove in OnObjectDestroy everywhere.
                // if it does have null then someone used
                // GameObject.Destroy instead of NetworkServer.Destroy.
                else Debug.LogWarning($"Found 'null' entry in observing list for connectionId={connection.connectionId}. Please call NetworkServer.Destroy to destroy networked objects. Don't use GameObject.Destroy.");
            }
        }

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        // internal for tests
        internal static readonly List<NetworkConnectionToClient> connectionsCopy =
            new List<NetworkConnectionToClient>();

        static void Broadcast()
        {
            // copy all connections into a helper collection so that
            // OnTransportDisconnected can be called while iterating.
            // -> OnTransportDisconnected removes from the collection
            // -> which would throw 'can't modify while iterating' errors
            // => see also: https://github.com/vis2k/Mirror/issues/2739
            // (copy nonalloc)
            // TODO remove this when we move to 'lite' transports with only
            //      socket send/recv later.
            connectionsCopy.Clear();
            connections.Values.CopyTo(connectionsCopy);

            // go through all connections
            foreach (NetworkConnectionToClient connection in connectionsCopy)
            {
                // has this connection joined the world yet?
                // for each READY connection:
                //   pull in UpdateVarsMessage for each entity it observes
                if (connection.isReady)
                {
                    // broadcast world state to this connection
                    BroadcastToConnection(connection);
                }

                // update connection to flush out batched messages
                connection.Update();
            }

            // TODO this is way too slow because we iterate ALL spawned :/
            // TODO this is way too complicated :/
            // to understand what this tries to prevent, consider this example:
            //   monster has health=100
            //   we change health=200, dirty bit is set
            //   player comes in range, gets full serialization spawn packet.
            //   next Broadcast(), player gets the health=200 change because dirty bit was set.
            //
            // this code clears all dirty bits if no players are around to prevent it.
            // BUT there are two issues:
            //   1. what if a playerB was around the whole time?
            //   2. why don't we handle broadcast and spawn packets both HERE?
            //      handling spawn separately is why we need this complex magic
            //
            // see test: DirtyBitsAreClearedForSpawnedWithoutObservers()
            // see test: SyncObjectChanges_DontGrowWithoutObservers()
            //
            // PAUL: we also do this to avoid ever growing SyncList .changes
            //ClearSpawnedDirtyBits();
            //
            // this was moved to NetworkIdentity.AddObserver!
            // same result, but no more O(N) loop in here!
            // TODO remove this comment after moving spawning into Broadcast()!
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

        internal static void NetworkLateUpdate()
        {
            // only broadcast world if active
            if (active)
                Broadcast();

            // process all outgoing messages after updating the world
            // (even if not active. still want to process disconnects etc.)
            if (Transport.activeTransport != null)
                Transport.activeTransport.ServerLateUpdate();
        }
    }
}
