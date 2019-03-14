using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class NetworkServer
    {
        static ULocalConnectionToClient s_LocalConnection;
        static bool s_Initialized;
        static int s_MaxConnections;

        // original HLAPI has .localConnections list with only m_LocalConnection in it
        // (for downwards compatibility because they removed the real localConnections list a while ago)
        // => removed it for easier code. use .localConection now!
        public static NetworkConnection localConnection => s_LocalConnection;

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
            if (s_Initialized)
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

                s_Initialized = false;
            }
            dontListen = false;
            active = false;
        }

        static void Initialize()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
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

        public static bool Listen(int maxConnections)
        {
            Initialize();
            s_MaxConnections = maxConnections;

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
        internal static int AddLocalClient(LocalClient localClient)
        {
            if (s_LocalConnection != null)
            {
                Debug.LogError("Local Connection already exists");
                return -1;
            }

            s_LocalConnection = new ULocalConnectionToClient(localClient)
            {
                connectionId = 0
            };
            OnConnected(s_LocalConnection);
            return 0;
        }

        internal static void RemoveLocalClient()
        {
            if (s_LocalConnection != null)
            {
                s_LocalConnection.Disconnect();
                s_LocalConnection.Dispose();
                s_LocalConnection = null;
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
        [Obsolete("use SendToObservers<T> instead")]
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
        static bool SendToObservers<T>(NetworkIdentity identity, T msg) where T: MessageBase
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

        [Obsolete("Use SendToAll<T> instead")]
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

        public static bool SendToAll<T>(T msg, int channelId = Channels.DefaultReliable) where T : MessageBase
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

        [Obsolete("Use SendToReady<T> instead")]
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

        public static bool SendToReady<T>(NetworkIdentity identity,T msg, int channelId = Channels.DefaultReliable) where T: MessageBase
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

            if (s_LocalConnection != null)
            {
                s_LocalConnection.Disconnect();
                s_LocalConnection.Dispose();
                s_LocalConnection = null;
            }

            active = false;
            localClientActive = false;
        }

        public static void DisconnectAllConnections()
        {
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                NetworkConnection conn = kvp.Value;
                conn.Disconnect();
                OnDisconnected(conn);
                conn.Dispose();
            }
            connections.Clear();
        }

        static void UpdateServerObjects()
        {
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

        // The user should never need to pump the update loop manually
        internal static void Update()
        {
            if (active)
            {
                UpdateServerObjects();
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
            if (connections.Count < s_MaxConnections)
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

            if (conn.playerController != null)
            {
                //NOTE: should there be default behaviour here to destroy the associated player?
                Debug.LogWarning("Player not destroyed when connection disconnected.");
            }

            if (LogFilter.Debug) Debug.Log("Server lost client:" + conn.connectionId);
            conn.RemoveObservers();
            conn.Dispose();
        }

        static void OnDataReceived(int connectionId, byte[] data)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                OnData(conn, data);
            }
            else
            {
                Debug.LogError("HandleData Unknown connectionId:" + connectionId);
            }
        }

        private static void OnError(int connectionId, Exception exception)
        {
            // TODO Let's discuss how we will handle errors
            Debug.LogException(exception);
        }

        static void OnData(NetworkConnection conn, byte[] data)
        {
            conn.TransportReceive(data);
        }

        static void GenerateConnectError(byte error)
        {
            Debug.LogError("Mirror Server Connect Error: " + error);
            GenerateError(null, error);
        }

        /* TODO use or remove
        static void GenerateDataError(NetworkConnection conn, byte error)
        {
            NetworkError dataError = (NetworkError)error;
            Debug.LogError("Mirror Server Data Error: " + dataError);
            GenerateError(conn, error);
        }

        static void GenerateDisconnectError(NetworkConnection conn, byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            Debug.LogError("Mirror Server Disconnect Error: " + disconnectError + " conn:[" + conn + "]:" + conn.connectionId);
            GenerateError(conn, error);
        }
        */

        static void GenerateError(NetworkConnection conn, byte error)
        {
            int msgId = MessagePacker.GetId<ErrorMessage>();
            if (handlers.ContainsKey(msgId))
            {
                ErrorMessage msg = new ErrorMessage
                {
                    value = error
                };

                // write the message to a local buffer
                NetworkWriter writer = new NetworkWriter();
                msg.Serialize(writer);

                // pass a reader (attached to local buffer) to handler
                NetworkReader reader = new NetworkReader(writer.ToArray());
                conn.InvokeHandler(msgId, reader);
            }
        }

        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = handler;
        }

        [Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler) where T: MessageBase, new()
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = networkMessage =>
            {
                T message = networkMessage.ReadMessage<T>();
                handler(networkMessage.conn, message);
            };
        }

        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        public static void UnregisterHandler<T>() where T:MessageBase
        {
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        public static void ClearHandlers()
        {
            handlers.Clear();
        }

        [Obsolete("Use SendToClient<T> instead")]
        public static void SendToClient(int connectionId, int msgType, MessageBase msg)
        {
            if (connections.TryGetValue(connectionId, out NetworkConnection conn))
            {
                conn.Send(msgType, msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }

        public static void SendToClient<T>(int connectionId, T msg) where T : MessageBase
        {
            NetworkConnection conn;
            if (connections.TryGetValue(connectionId, out conn))
            {
                conn.Send(msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }


        [Obsolete("Use SendToClientOfPlayer<T> instead")]
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
        public static void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg) where T: MessageBase
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
            return InternalAddPlayerForConnection(conn, player);
        }

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player)
        {
            return InternalAddPlayerForConnection(conn, player);
        }

        internal static bool InternalAddPlayerForConnection(NetworkConnection conn, GameObject playerGameObject)
        {
            NetworkIdentity identity = playerGameObject.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.Log("AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + playerGameObject);
                return false;
            }
            identity.Reset();

            // cannot have a player object in "Add" version
            if (conn.playerController != null)
            {
                Debug.Log("AddPlayer: player object already exists");
                return false;
            }

            conn.SetPlayerController(identity);

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            identity.connectionToClient = conn;

            SetClientReady(conn);

            if (SetupLocalPlayerForConnection(conn, identity))
            {
                return true;
            }

            if (LogFilter.Debug) Debug.Log("Adding new playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId);

            FinishPlayerForConnection(conn, identity, playerGameObject);
            if (identity.localPlayerAuthority)
            {
                identity.SetClientOwner(conn);
            }
            return true;
        }

        static bool SetupLocalPlayerForConnection(NetworkConnection conn, NetworkIdentity identity)
        {
            if (LogFilter.Debug) Debug.Log("NetworkServer SetupLocalPlayerForConnection netID:" + identity.netId);

            if (conn is ULocalConnectionToClient localConnection)
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
                localConnection.localClient.AddLocalPlayer(identity);
                identity.SetClientOwner(conn);

                // Trigger OnAuthority
                identity.ForceAuthority(true);

                // Trigger OnStartLocalPlayer
                identity.SetLocalPlayer();
                return true;
            }
            return false;
        }

        static void FinishPlayerForConnection(NetworkConnection conn, NetworkIdentity identity, GameObject playerGameObject)
        {
            if (identity.netId == 0)
            {
                // it is allowed to provide an already spawned object as the new player object.
                // so dont spawn it again.
                Spawn(playerGameObject);
            }

            OwnerMessage owner = new OwnerMessage
            {
                netId = identity.netId
            };
            conn.Send(owner);
        }

        internal static bool InternalReplacePlayerForConnection(NetworkConnection conn, GameObject playerGameObject)
        {
            NetworkIdentity playerNetworkIdentity = playerGameObject.GetComponent<NetworkIdentity>();
            if (playerNetworkIdentity == null)
            {
                Debug.LogError("ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + playerGameObject);
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

            conn.SetPlayerController(playerNetworkIdentity);

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            playerNetworkIdentity.connectionToClient = conn;

            //NOTE: DONT set connection ready.

            if (LogFilter.Debug) Debug.Log("NetworkServer ReplacePlayer setup local");

            if (SetupLocalPlayerForConnection(conn, playerNetworkIdentity))
            {
                return true;
            }

            if (LogFilter.Debug) Debug.Log("Replacing playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId);

            FinishPlayerForConnection(conn, playerNetworkIdentity, playerGameObject);
            if (playerNetworkIdentity.localPlayerAuthority)
            {
                playerNetworkIdentity.SetClientOwner(conn);
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

            if (conn.isReady)
            {
                if (LogFilter.Debug) Debug.Log("SetClientReady conn " + conn.connectionId + " already ready");
                return;
            }

            if (conn.playerController == null)
            {
                // this is now allowed
                if (LogFilter.Debug) Debug.LogWarning("Ready with no player object");
            }

            conn.isReady = true;

            if (conn is ULocalConnectionToClient localConnection)
            {
                if (LogFilter.Debug) Debug.Log("NetworkServer Ready handling ULocalConnectionToClient");

                // Setup spawned objects for local player
                // Only handle the local objects for the first player (no need to redo it when doing more local players)
                // and don't handle player objects here, they were done above
                foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
                {
                    // Need to call OnStartClient directly here, as it's already been added to the local object dictionary
                    // in the above SetLocalPlayer call
                    if (identity.gameObject != null)
                    {
                        bool visible = identity.OnCheckObserver(conn);
                        if (visible)
                        {
                            identity.AddObserver(conn);
                        }
                        if (!identity.isClient)
                        {
                            if (LogFilter.Debug) Debug.Log("LocalClient.SetSpawnObject calling OnStartClient");
                            identity.OnStartClient();
                        }
                    }
                }
                return;
            }

            // Spawn/update all current server objects
            if (LogFilter.Debug) Debug.Log("Spawning " + NetworkIdentity.spawned.Count + " objects for conn " + conn.connectionId);

            conn.Send(new ObjectSpawnStartedMessage());

            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (identity == null)
                {
                    Debug.LogWarning("Invalid object found in server local object list (null NetworkIdentity).");
                    continue;
                }
                if (!identity.gameObject.activeSelf)
                {
                    continue;
                }

                if (LogFilter.Debug) Debug.Log("Sending spawn message for current server objects name='" + identity.name + "' netId=" + identity.netId);

                bool visible = identity.OnCheckObserver(conn);
                if (visible)
                {
                    identity.AddObserver(conn);
                }
            }

            conn.Send(new ObjectSpawnFinishedMessage());
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
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                NetworkConnection conn = kvp.Value;
                SetClientNotReady(conn);
            }
        }

        public static void SetClientNotReady(NetworkConnection conn)
        {
            InternalSetClientNotReady(conn);
        }

        internal static void InternalSetClientNotReady(NetworkConnection conn)
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
                conn.RemovePlayerController();
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
            if (!NetworkServer.active)
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

            // 'identity' is a prefab that should be spawned
            if (identity.sceneId == 0)
            {
                SpawnPrefabMessage msg = new SpawnPrefabMessage
                {
                    netId = identity.netId,
                    assetId = identity.assetId,
                    position = identity.transform.position,
                    rotation = identity.transform.rotation,

                    // serialize all components with initialState = true
                    payload = identity.OnSerializeAllSafely(true)
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
                    sceneId = identity.sceneId,
                    position = identity.transform.position,
                    rotation = identity.transform.rotation,

                    // include synch data
                    payload = identity.OnSerializeAllSafely(true)
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
        }

        public static void DestroyPlayerForConnection(NetworkConnection conn)
        {
            // note: conn.playerController/clientOwnedObjects might be null if
            // the player is still in a lobby and hasn't joined the world yet,
            // so we need null checks for both of them.
            // => destroy what we can destroy.

            if (conn.clientOwnedObjects != null)
            {
                HashSet<uint> tmp = new HashSet<uint>(conn.clientOwnedObjects);
                foreach (uint netId in tmp)
                {
                    if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
                    {
                        Destroy(identity.gameObject);
                    }
                }
            }

            if (conn.playerController != null)
            {
                DestroyObject(conn.playerController, true);
            }

            conn.RemovePlayerController();
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

        [Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
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
