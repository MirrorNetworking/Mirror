using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Mirror
{
    public static class NetworkServer
    {
        static bool initialized;
        static int maxConnections;

        // original HLAPI has .localConnections list with only m_LocalConnection in it
        // (for backwards compatibility because they removed the real localConnections list a while ago)
        // => removed it for easier code. use .localConnection now!
        public static NetworkConnection localConnection { get; private set; }

        // <connectionId, NetworkConnection>
        public static Dictionary<int, NetworkConnection> connections = new Dictionary<int, NetworkConnection>();
        public static Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();

        public static bool dontListen;

        public static bool active { get; private set; }
        public static bool localClientActive { get; private set; }

        public static void Reset()
        {
            active = false;
        }

        public static void Shutdown()
        {
            if (initialized)
            {
                DisconnectAll();

                if (dontListen)
                {
                    // was never started, so dont stop
                }
                else
                {
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

            NetworkIdentity.ResetNextNetworkId();
        }

        static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            if (LogFilter.Debug) Debug.Log("NetworkServer Created version " + Version.Current);

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
            RegisterHandler<RemovePlayerMessage>(OnRemovePlayerMessage);
            RegisterHandler<NetworkPingMessage>(NetworkTime.OnServerPing);
        }

        public static bool Listen(int maxConns)
        {
            Initialize();
            maxConnections = maxConns;

            // only start server if we want to listen
            if (!dontListen)
            {
                Transport.activeTransport.ServerStart();
                if (LogFilter.Debug) Debug.Log("Server started listening");
            }

            active = true;
            RegisterMessageHandlers();
            return true;
        }

        public static bool AddConnection(NetworkConnection conn)
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

        public static bool RemoveConnection(int connectionId)
        {
            return connections.Remove(connectionId);
        }

        // called by LocalClient to add itself. dont call directly.
        internal static void SetLocalConnection(ULocalConnectionToClient conn)
        {
            if (localConnection != null)
            {
                Debug.LogError("Local Connection already exists");
                return;
            }

            localConnection = conn;
            OnConnected(localConnection);
        }

        internal static void RemoveLocalConnection()
        {
            if (localConnection != null)
            {
                localConnection.Disconnect();
                localConnection.Dispose();
                localConnection = null;
            }
            localClientActive = false;
            RemoveConnection(0);
        }

        internal static void ActivateLocalClientScene()
        {
            if (localClientActive)
                return;

            // ClientScene for a local connection is becoming active. any spawned objects need to be started as client objects
            localClientActive = true;
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (!identity.isClient)
                {
                    if (LogFilter.Debug) Debug.Log("ActivateClientScene " + identity.netId + " " + identity);

                    identity.isClient = true;
                    identity.OnStartClient();
                }
            }
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("use SendToObservers<T> instead")]
        static bool SendToObservers(NetworkIdentity identity, short msgType, MessageBase msg)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToObservers id:" + msgType);

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

                // send to all observers
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    result &= kvp.Value.SendBytes(bytes);
                }
                return result;
            }
            return false;
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        static bool SendToObservers<T>(NetworkIdentity identity, T msg) where T: IMessageBase
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToObservers id:" + typeof(T));

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.Pack(msg);

                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    result &= kvp.Value.SendBytes(bytes);
                }
                return result;
            }
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SendToAll<T> instead")]
        public static bool SendToAll(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToAll id:" + msgType);

            // pack message into byte[] once
            byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

            // send to all
            bool result = true;
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                result &= kvp.Value.SendBytes(bytes, channelId);
            }
            return result;
        }

        public static bool SendToAll<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToAll id:" + typeof(T));

            // pack message into byte[] once
            byte[] bytes = MessagePacker.Pack(msg);

            bool result = true;
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                result &= kvp.Value.SendBytes(bytes, channelId);
            }
            return result;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SendToReady<T> instead")]
        public static bool SendToReady(NetworkIdentity identity, short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToReady msgType:" + msgType);

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.PackMessage((ushort)msgType, msg);

                // send to all ready observers
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    if (kvp.Value.isReady)
                    {
                        result &= kvp.Value.SendBytes(bytes, channelId);
                    }
                }
                return result;
            }
            return false;
        }

        public static bool SendToReady<T>(NetworkIdentity identity,T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase
        {
            if (LogFilter.Debug) Debug.Log("Server.SendToReady msgType:" + typeof(T));

            if (identity != null && identity.observers != null)
            {
                // pack message into byte[] once
                byte[] bytes = MessagePacker.Pack(msg);

                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    if (kvp.Value.isReady)
                    {
                        result &= kvp.Value.SendBytes(bytes, channelId);
                    }
                }
                return result;
            }
            return false;
        }

        public static void DisconnectAll()
        {
            DisconnectAllConnections();
            localConnection = null;

            active = false;
            localClientActive = false;
        }

        public static void DisconnectAllConnections()
        {
            foreach (NetworkConnection conn in connections.Values)
            {
                conn.Disconnect();
                // call OnDisconnected unless local player in host mode
                if (conn.connectionId != 0)
                    OnDisconnected(conn);
                conn.Dispose();
            }
            connections.Clear();
        }

        // The user should never need to pump the update loop manually
        internal static void Update()
        {
            if (!active)
                return;

            // update all server objects
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in NetworkIdentity.spawned)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    kvp.Value.MirrorUpdate();
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
            if (LogFilter.Debug) Debug.Log("Server accepted client:" + connectionId);

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
                if (LogFilter.Debug) Debug.Log("Server connectionId " + connectionId + " already in use. kicked client:" + connectionId);
                return;
            }

            // are more connections allowed? if not, kick
            // (it's easier to handle this in Mirror, so Transports can have
            //  less code and third party transport might not do that anyway)
            // (this way we could also send a custom 'tooFull' message later,
            //  Transport can't do that)
            if (connections.Count < maxConnections)
            {
                // get ip address from connection
                string address = Transport.activeTransport.ServerGetClientAddress(connectionId);

                // add player info
                NetworkConnection conn = new NetworkConnection(address, connectionId);
                OnConnected(conn);
            }
            else
            {
                // kick
                Transport.activeTransport.ServerDisconnect(connectionId);
                if (LogFilter.Debug) Debug.Log("Server full, kicked client:" + connectionId);
            }
        }

        static void OnConnected(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("Server accepted client:" + conn.connectionId);

            // add connection and invoke connected event
            AddConnection(conn);
            conn.InvokeHandler(new ConnectMessage());
        }

        static void OnDisconnected(int connectionId)
        {
            if (LogFilter.Debug) Debug.Log("Server disconnect client:" + connectionId);

            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                conn.Disconnect();
                RemoveConnection(connectionId);
                if (LogFilter.Debug) Debug.Log("Server lost client:" + connectionId);

                OnDisconnected(conn);
            }
        }

        static void OnDisconnected(NetworkConnection conn)
        {
            conn.InvokeHandler(new DisconnectMessage());
            if (LogFilter.Debug) Debug.Log("Server lost client:" + conn.connectionId);
        }

        static void OnDataReceived(int connectionId, ArraySegment<byte> data)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
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

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = handler;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler) where T: IMessageBase, new()
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = MessagePacker.MessageHandler<T>(handler);
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        public static void UnregisterHandler<T>() where T : IMessageBase
        {
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        public static void ClearHandlers()
        {
            handlers.Clear();
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SendToClient<T> instead")]
        public static void SendToClient(int connectionId, int msgType, MessageBase msg)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                conn.Send(msgType, msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }

        public static void SendToClient<T>(int connectionId, T msg) where T : IMessageBase
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                conn.Send(msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }


        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SendToClientOfPlayer<T> instead")]
        public static void SendToClientOfPlayer(NetworkIdentity identity, int msgType, MessageBase msg)
        {
            if (identity != null)
            {
                identity.connectionToClient.Send(msgType, msg);
            }
            else
            {
                Debug.LogError("SendToClientOfPlayer: player has no NetworkIdentity: " + identity.name);
            }
        }

        // send this message to the player only
        public static void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg) where T: IMessageBase
        {
            if (identity != null)
            {
                identity.connectionToClient.Send(msg);
            }
            else
            {
                Debug.LogError("SendToClientOfPlayer: player has no NetworkIdentity: " + identity.name);
            }
        }

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return InternalReplacePlayerForConnection(conn, player);
        }

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player)
        {
            return InternalReplacePlayerForConnection(conn, player);
        }

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return AddPlayerForConnection(conn, player);
        }

        static void SpawnObserversForConnection(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("Spawning " + NetworkIdentity.spawned.Count + " objects for conn " + conn.connectionId);

            // let connection know that we are about to start spawning...
            conn.Send(new ObjectSpawnStartedMessage());

            // add connection to each nearby NetworkIdentity's observers, which
            // internally sends a spawn message for each one to the connection.
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (identity.gameObject.activeSelf) //TODO this is different // try with far away ones in ummorpg!
                {
                    if (LogFilter.Debug) Debug.Log("Sending spawn message for current server objects name='" + identity.name + "' netId=" + identity.netId + " sceneId=" + identity.sceneId);

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

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.Log("AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }
            identity.Reset();

            // cannot have a player object in "Add" version
            if (conn.playerController != null)
            {
                Debug.Log("AddPlayer: player object already exists");
                return false;
            }

            conn.playerController = identity;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.connectionToClient = conn;

            // set ready if not set yet
            SetClientReady(conn);

            // add connection to observers AFTER the playerController was set.
            // by definition, there is nothing to observe if there is no player
            // controller.
            //
            // IMPORTANT: do this in AddPlayerForConnection & ReplacePlayerForConnection!
            SpawnObserversForConnection(conn);

            if (SetupLocalPlayerForConnection(conn, identity))
            {
                return true;
            }

            if (LogFilter.Debug) Debug.Log("Adding new playerGameObject object netId: " + identity.netId + " asset ID " + identity.assetId);

            FinishPlayerForConnection(identity, player);
            if (identity.localPlayerAuthority)
            {
                identity.SetClientOwner(conn);
            }
            return true;
        }

        static bool SetupLocalPlayerForConnection(NetworkConnection conn, NetworkIdentity identity)
        {
            if (LogFilter.Debug) Debug.Log("NetworkServer SetupLocalPlayerForConnection netID:" + identity.netId);

            if (conn is ULocalConnectionToClient)
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer AddPlayer handling ULocalConnectionToClient");

                // Spawn this player for other players, instead of SpawnObject:
                if (identity.netId == 0)
                {
                    // it is allowed to provide an already spawned object as the new player object.
                    // so dont spawn it again.
                    identity.OnStartServer(true);
                }
                identity.RebuildObservers(true);
                SendSpawnMessage(identity, null);

                // Set up local player instance on the client instance and update local object map
                NetworkClient.AddLocalPlayer(identity);
                identity.SetClientOwner(conn);

                // Trigger OnAuthority
                identity.ForceAuthority(true);

                // Trigger OnStartLocalPlayer
                identity.SetLocalPlayer();
                return true;
            }
            return false;
        }

        static void FinishPlayerForConnection(NetworkIdentity identity, GameObject playerGameObject)
        {
            if (identity.netId == 0)
            {
                // it is allowed to provide an already spawned object as the new player object.
                // so dont spawn it again.
                Spawn(playerGameObject);
            }
        }

        internal static bool InternalReplacePlayerForConnection(NetworkConnection conn, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + player);
                return false;
            }

            //NOTE: there can be an existing player
            if (LogFilter.Debug) Debug.Log("NetworkServer ReplacePlayer");

            // is there already an owner that is a different object??
            if (conn.playerController != null)
            {
                conn.playerController.SetNotLocalPlayer();
                conn.playerController.clientAuthorityOwner = null;
            }

            conn.playerController = identity;

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.connectionToClient = conn;

            //NOTE: DONT set connection ready.

            // add connection to observers AFTER the playerController was set.
            // by definition, there is nothing to observe if there is no player
            // controller.
            //
            // IMPORTANT: do this in AddPlayerForConnection & ReplacePlayerForConnection!
            SpawnObserversForConnection(conn);

            if (LogFilter.Debug) Debug.Log("NetworkServer ReplacePlayer setup local");

            if (SetupLocalPlayerForConnection(conn, identity))
            {
                return true;
            }

            if (LogFilter.Debug) Debug.Log("Replacing playerGameObject object netId: " + player.GetComponent<NetworkIdentity>().netId + " asset ID " + player.GetComponent<NetworkIdentity>().assetId);

            FinishPlayerForConnection(identity, player);
            if (identity.localPlayerAuthority)
            {
                identity.SetClientOwner(conn);
            }
            return true;
        }

        static bool GetNetworkIdentity(GameObject go, out NetworkIdentity identity)
        {
            identity = go.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("GameObject " + go.name + " doesn't have NetworkIdentity.");
                return false;
            }
            return true;
        }

        public static void SetClientReady(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log("SetClientReadyInternal for conn:" + conn.connectionId);

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

        // call this to make all the clients not ready, such as when changing levels.
        public static void SetAllClientsNotReady()
        {
            foreach (NetworkConnection conn in connections.Values)
            {
                SetClientNotReady(conn);
            }
        }

        public static void SetClientNotReady(NetworkConnection conn)
        {
            if (conn.isReady)
            {
                if (LogFilter.Debug) Debug.Log("PlayerNotReady " + conn);
                conn.isReady = false;
                conn.RemoveObservers();

                conn.Send(new NotReadyMessage());
            }
        }

        // default ready handler.
        static void OnClientReadyMessage(NetworkConnection conn, ReadyMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("Default handler for ready message from " + conn);
            SetClientReady(conn);
        }

        // default remove player handler
        static void OnRemovePlayerMessage(NetworkConnection conn, RemovePlayerMessage msg)
        {
            if (conn.playerController != null)
            {
                Destroy(conn.playerController.gameObject);
                conn.playerController = null;
            }
            else
            {
                Debug.LogError("Received remove player message but connection has no player");
            }
        }

        // Handle command from specific player, this could be one of multiple players on a single client
        static void OnCommandMessage(NetworkConnection conn, CommandMessage msg)
        {
            if (!NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                Debug.LogWarning("Spawned object not found when handling Command message [netId=" + msg.netId + "]");
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            if (conn.playerController != null && conn.playerController.netId != identity.netId)
            {
                if (identity.clientAuthorityOwner != conn)
                {
                    Debug.LogWarning("Command for object without authority [netId=" + msg.netId + "]");
                    return;
                }
            }

            if (LogFilter.Debug) Debug.Log("OnCommandMessage for netId=" + msg.netId + " conn=" + conn);
            identity.HandleCommand(msg.componentIndex, msg.functionHash, new NetworkReader(msg.payload));
        }

        internal static void SpawnObject(GameObject obj)
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
            identity.Reset();

            identity.OnStartServer(false);

            if (LogFilter.Debug) Debug.Log("SpawnObject instance ID " + identity.netId + " asset ID " + identity.assetId);

            identity.RebuildObservers(true);
            //SendSpawnMessage(objNetworkIdentity, null);
        }

        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly)
                return;

            if (LogFilter.Debug) Debug.Log("Server SendSpawnMessage: name=" + identity.name + " sceneId=" + identity.sceneId.ToString("X") + " netid=" + identity.netId); // for easier debugging

            NetworkWriter writer = NetworkWriterPool.GetWriter();
           
            

            // convert to ArraySegment to avoid reader allocations
            // (need to handle null case too)
            ArraySegment<byte> segment = default;

            // serialize all components with initialState = true
            // (can be null if has none)
            if (identity.OnSerializeAllSafely(true, writer))
            {
                segment = writer.ToArraySegment();
            }

            // 'identity' is a prefab that should be spawned
            if (identity.sceneId == 0)
            {
                SpawnPrefabMessage msg = new SpawnPrefabMessage
                {
                    netId = identity.netId,
                    owner = conn?.playerController == identity,
                    assetId = identity.assetId,
                    // use local values for VR support
                    position = identity.transform.localPosition,
                    rotation = identity.transform.localRotation,
                    scale = identity.transform.localScale,
                    payload = segment
                };

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send(msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(identity, msg);
                }
            }
            // 'identity' is a scene object that should be spawned again
            else
            {
                SpawnSceneObjectMessage msg = new SpawnSceneObjectMessage
                {
                    netId = identity.netId,
                    owner = conn?.playerController == identity,
                    sceneId = identity.sceneId,
                    // use local values for VR support
                    position = identity.transform.localPosition,
                    rotation = identity.transform.localRotation,
                    scale = identity.transform.localScale,
                    payload = segment
                };

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send(msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(identity, msg);
                }
            }

            NetworkWriterPool.Recycle(writer);
        }

        public static void DestroyPlayerForConnection(NetworkConnection conn)
        {
            // => destroy what we can destroy.
            HashSet<uint> tmp = new HashSet<uint>(conn.clientOwnedObjects);
            foreach (uint netId in tmp)
            {
                if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                {
                    Destroy(identity.gameObject);
                }
            }

            if (conn.playerController != null)
            {
                DestroyObject(conn.playerController, true);
                conn.playerController = null;
            }
        }

        public static void Spawn(GameObject obj)
        {
            if (VerifyCanSpawn(obj))
            {
                SpawnObject(obj);
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
                Debug.LogErrorFormat("GameObject {0} is a prefab, it can't be spawned. This will cause errors in builds.", obj.name);
                return false;
            }

            return true;
        }

        public static bool SpawnWithClientAuthority(GameObject obj, GameObject player)
        {
            NetworkIdentity identity = player.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("SpawnWithClientAuthority player object has no NetworkIdentity");
                return false;
            }

            if (identity.connectionToClient == null)
            {
                Debug.LogError("SpawnWithClientAuthority player object is not a player.");
                return false;
            }

            return SpawnWithClientAuthority(obj, identity.connectionToClient);
        }

        public static bool SpawnWithClientAuthority(GameObject obj, NetworkConnection conn)
        {
            if (!conn.isReady)
            {
                Debug.LogError("SpawnWithClientAuthority NetworkConnection is not ready!");
                return false;
            }

            Spawn(obj);

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null || !identity.isServer)
            {
                // spawning the object failed.
                return false;
            }

            return identity.AssignClientAuthority(conn);
        }

        public static bool SpawnWithClientAuthority(GameObject obj, Guid assetId, NetworkConnection conn)
        {
            Spawn(obj, assetId);

            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null || !identity.isServer)
            {
                // spawning the object failed.
                return false;
            }

            return identity.AssignClientAuthority(conn);
        }

        public static void Spawn(GameObject obj, Guid assetId)
        {
            if (VerifyCanSpawn(obj))
            {
                if (GetNetworkIdentity(obj, out NetworkIdentity identity))
                {
                    identity.assetId = assetId;
                }
                SpawnObject(obj);
            }
        }

        static void DestroyObject(NetworkIdentity identity, bool destroyServerObject)
        {
            if (LogFilter.Debug) Debug.Log("DestroyObject instance:" + identity.netId);
            NetworkIdentity.spawned.Remove(identity.netId);

            identity.clientAuthorityOwner?.RemoveOwnedObject(identity);

            ObjectDestroyMessage msg = new ObjectDestroyMessage
            {
                netId = identity.netId
            };
            SendToObservers(identity, msg);

            identity.ClearObservers();
            if (NetworkClient.active && localClientActive)
            {
                identity.OnNetworkDestroy();
            }

            // when unspawning, dont destroy the server's object
            if (destroyServerObject)
            {
                UnityEngine.Object.Destroy(identity.gameObject);
            }
            identity.MarkForReset();
        }

        public static void Destroy(GameObject obj)
        {
            if (obj == null)
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer DestroyObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, true);
            }
        }

        public static void UnSpawn(GameObject obj)
        {
            if (obj == null)
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer UnspawnObject is null");
                return;
            }

            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                DestroyObject(identity, false);
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
        public static GameObject FindLocalObject(uint netId)
        {
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity.gameObject;
            }
            return null;
        }

        static bool ValidateSceneObject(NetworkIdentity identity)
        {
            if (identity.gameObject.hideFlags == HideFlags.NotEditable || identity.gameObject.hideFlags == HideFlags.HideAndDontSave)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(identity.gameObject))
                return false;
#endif

            // If not a scene object
            return identity.sceneId != 0;
        }

        public static bool SpawnObjects()
        {
            if (!active)
                return true;

            NetworkIdentity[] identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                {
                    if (LogFilter.Debug) Debug.Log("SpawnObjects sceneId:" + identity.sceneId.ToString("X") + " name:" + identity.gameObject.name);
                    identity.Reset();
                    identity.gameObject.SetActive(true);
                }
            }

            foreach (NetworkIdentity identity in identities)
            {
                if (ValidateSceneObject(identity))
                {
                    Spawn(identity.gameObject);

                    // these objects are server authority - even if "localPlayerAuthority" is set on them
                    identity.ForceAuthority(true);
                }
            }
            return true;
        }
    }
}
