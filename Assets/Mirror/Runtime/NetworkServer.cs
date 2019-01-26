using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

namespace Mirror
{
    public static class NetworkServer
    {
        static bool s_Active;
        static bool s_LocalClientActive;
        static ULocalConnectionToClient s_LocalConnection;

        static int s_ServerHostId = -1;
        static bool s_Initialized;
        static int s_MaxConnections;

        // original HLAPI has .localConnections list with only m_LocalConnection in it
        // (for downwards compatibility because they removed the real localConnections list a while ago)
        // => removed it for easier code. use .localConection now!
        public static NetworkConnection localConnection { get { return s_LocalConnection; } }

        public static int serverHostId { get { return s_ServerHostId; } }

        // <connectionId, NetworkConnection>
        public static Dictionary<int, NetworkConnection> connections = new Dictionary<int, NetworkConnection>();
        public static Dictionary<short, NetworkMessageDelegate> handlers = new Dictionary<short, NetworkMessageDelegate>();

        public static bool dontListen;

        public static bool active { get { return s_Active; } }
        public static bool localClientActive { get { return s_LocalClientActive; } }

        public static void Reset()
        {
            s_Active = false;
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
                    NetworkManager.singleton.transport.ServerStop();
                    s_ServerHostId = -1;
                }

                s_Initialized = false;
            }
            dontListen = false;
            s_Active = false;
        }

        static void Initialize()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            if (LogFilter.Debug) { Debug.Log("NetworkServer Created version " + Version.Current); }

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();
        }

        internal static void RegisterMessageHandlers()
        {
            RegisterHandler(MsgType.Ready, OnClientReadyMessage);
            RegisterHandler(MsgType.Command, OnCommandMessage);
            RegisterHandler(MsgType.RemovePlayer, OnRemovePlayerMessage);
            RegisterHandler(MsgType.Ping, NetworkTime.OnServerPing);
        }

        public static bool Listen(int maxConnections)
        {
            Initialize();
            s_MaxConnections = maxConnections;

            // only start server if we want to listen
            if (!dontListen)
            {
                NetworkManager.singleton.transport.ServerStart();
                s_ServerHostId = 0; // so it doesn't return false

                if (s_ServerHostId == -1)
                {
                    return false;
                }

                if (LogFilter.Debug) { Debug.Log("Server started listening"); }
            }

            s_Active = true;
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

            s_LocalConnection = new ULocalConnectionToClient(localClient);
            s_LocalConnection.connectionId = 0;
            AddConnection(s_LocalConnection);

            s_LocalConnection.InvokeHandlerNoData((short)MsgType.Connect);
            return 0;
        }

        internal static void RemoveLocalClient(NetworkConnection localClientConnection)
        {
            if (s_LocalConnection != null)
            {
                s_LocalConnection.Disconnect();
                s_LocalConnection.Dispose();
                s_LocalConnection = null;
            }
            s_LocalClientActive = false;
            RemoveConnection(0);
        }

        internal static void ActivateLocalClientScene()
        {
            if (s_LocalClientActive)
                return;

            // ClientScene for a local connection is becoming active. any spawned objects need to be started as client objects
            s_LocalClientActive = true;
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (!identity.isClient)
                {
                    if (LogFilter.Debug) { Debug.Log("ActivateClientScene " + identity.netId + " " + identity); }

                    identity.EnableIsClient();
                    identity.OnStartClient();
                }
            }
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        static bool SendToObservers(NetworkIdentity identity, short msgType, MessageBase msg)
        {
            if (LogFilter.Debug) { Debug.Log("Server.SendToObservers id:" + msgType); }

            if (identity != null && identity.observers != null)
            {
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    result &= kvp.Value.Send(msgType, msg);
                }
                return result;
            }
            return false;
        }

        public static bool SendToAll(short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) { Debug.Log("Server.SendToAll id:" + msgType); }

            bool result = true;
            foreach (KeyValuePair<int, NetworkConnection> kvp in connections)
            {
                result &= kvp.Value.Send(msgType, msg, channelId);
            }
            return result;
        }

        public static bool SendToReady(NetworkIdentity identity, short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)
        {
            if (LogFilter.Debug) { Debug.Log("Server.SendToReady msgType:" + msgType); }

            if (identity != null && identity.observers != null)
            {
                bool result = true;
                foreach (KeyValuePair<int, NetworkConnection> kvp in identity.observers)
                {
                    if (kvp.Value.isReady)
                    {
                        result &= kvp.Value.Send(msgType, msg, channelId);
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

            s_Active = false;
            s_LocalClientActive = false;
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
                    kvp.Value.UNetUpdate();
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
            if (s_ServerHostId == -1)
                return;

            int connectionId;
            TransportEvent transportEvent;
            byte[] data;
            while (NetworkManager.singleton.transport.ServerGetNextMessage(out connectionId, out transportEvent, out data))
            {
                switch (transportEvent)
                {
                    case TransportEvent.Connected:
                        //Debug.Log("NetworkServer loop: Connected");
                        HandleConnect(connectionId, 0);
                        break;
                    case TransportEvent.Data:
                        //Debug.Log("NetworkServer loop: clientId: " + message.connectionId + " Data: " + BitConverter.ToString(message.data));
                        HandleData(connectionId, data, 0);
                        break;
                    case TransportEvent.Disconnected:
                        //Debug.Log("NetworkServer loop: Disconnected");
                        HandleDisconnect(connectionId, 0);
                        break;
                }
            }

            UpdateServerObjects();
        }

        static void HandleConnect(int connectionId, byte error)
        {
            if (LogFilter.Debug) { Debug.Log("Server accepted client:" + connectionId); }

            if (error != 0)
            {
                GenerateConnectError(error);
                return;
            }

            // connectionId needs to be > 0 because 0 is reserved for local player
            if (connectionId <= 0)
            {
                Debug.LogError("Server.HandleConnect: invalid connectionId: " + connectionId + " . Needs to be >0, because 0 is reserved for local player.");
                NetworkManager.singleton.transport.ServerDisconnect(connectionId);
                return;
            }

            // connectionId not in use yet?
            if (connections.ContainsKey(connectionId))
            {
                NetworkManager.singleton.transport.ServerDisconnect(connectionId);
                if (LogFilter.Debug) { Debug.Log("Server connectionId " + connectionId + " already in use. kicked client:" + connectionId); }
                return;
            }

            // are more connections allowed? if not, kick
            // (it's easier to handle this in Mirror, so Transports can have
            //  less code and third party transport might not do that anyway)
            // (this way we could also send a custom 'tooFull' message later,
            //  Transport can't do that)
            if (connections.Count <= s_MaxConnections)
            {
                // get ip address from connection
                string address;
                NetworkManager.singleton.transport.GetConnectionInfo(connectionId, out address);

                // add player info
                NetworkConnection conn = new NetworkConnection(address, s_ServerHostId, connectionId);
                AddConnection(conn);
                OnConnected(conn);
            }
            else
            {
                // kick
                NetworkManager.singleton.transport.ServerDisconnect(connectionId);
                if (LogFilter.Debug) { Debug.Log("Server full, kicked client:" + connectionId); }
            }
        }

        static void OnConnected(NetworkConnection conn)
        {
            if (LogFilter.Debug) { Debug.Log("Server accepted client:" + conn.connectionId); }
            conn.InvokeHandlerNoData((short)MsgType.Connect);
        }

        static void HandleDisconnect(int connectionId, byte error)
        {
            if (LogFilter.Debug) { Debug.Log("Server disconnect client:" + connectionId); }

            NetworkConnection conn;
            if (connections.TryGetValue(connectionId, out conn))
            {
                conn.Disconnect();
                RemoveConnection(connectionId);
                if (LogFilter.Debug) { Debug.Log("Server lost client:" + connectionId); }

                OnDisconnected(conn);
            }
        }

        static void OnDisconnected(NetworkConnection conn)
        {
            conn.InvokeHandlerNoData((short)MsgType.Disconnect);

            if (conn.playerController != null)
            {
                //NOTE: should there be default behaviour here to destroy the associated player?
                Debug.LogWarning("Player not destroyed when connection disconnected.");
            }

            if (LogFilter.Debug) { Debug.Log("Server lost client:" + conn.connectionId); }
            conn.RemoveObservers();
            conn.Dispose();
        }

        static void HandleData(int connectionId, byte[] data, byte error)
        {
            NetworkConnection conn;
            if (connections.TryGetValue(connectionId, out conn))
            {
                OnData(conn, data);
            }
            else
            {
                Debug.LogError("HandleData Unknown connectionId:" + connectionId);
            }
        }

        static void OnData(NetworkConnection conn, byte[] data)
        {
            conn.TransportReceive(data);
        }

        static void GenerateConnectError(byte error)
        {
            Debug.LogError("UNet Server Connect Error: " + error);
            GenerateError(null, error);
        }

        /* TODO use or remove
        static void GenerateDataError(NetworkConnection conn, byte error)
        {
            NetworkError dataError = (NetworkError)error;
            Debug.LogError("UNet Server Data Error: " + dataError);
            GenerateError(conn, error);
        }

        static void GenerateDisconnectError(NetworkConnection conn, byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            Debug.LogError("UNet Server Disconnect Error: " + disconnectError + " conn:[" + conn + "]:" + conn.connectionId);
            GenerateError(conn, error);
        }
        */

        static void GenerateError(NetworkConnection conn, byte error)
        {
            if (handlers.ContainsKey((short)MsgType.Error))
            {
                ErrorMessage msg = new ErrorMessage();
                msg.value = error;

                // write the message to a local buffer
                NetworkWriter writer = new NetworkWriter();
                msg.Serialize(writer);

                // pass a reader (attached to local buffer) to handler
                NetworkReader reader = new NetworkReader(writer.ToArray());
                conn.InvokeHandler((short)MsgType.Error, reader);
            }
        }

        public static void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) { Debug.Log("NetworkServer.RegisterHandler replacing " + msgType); }
            }
            handlers[msgType] = handler;
        }

        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((short)msgType, handler);
        }

        public static void UnregisterHandler(short msgType)
        {
            handlers.Remove(msgType);
        }

        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((short)msgType);
        }

        public static void ClearHandlers()
        {
            handlers.Clear();
        }

        public static void SendToClient(int connectionId, short msgType, MessageBase msg)
        {
            NetworkConnection conn;
            if (connections.TryGetValue(connectionId, out conn))
            {
                conn.Send(msgType, msg);
                return;
            }
            Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list");
        }

        // send this message to the player only
        public static void SendToClientOfPlayer(NetworkIdentity identity, short msgType, MessageBase msg)
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

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            NetworkIdentity identity;
            if (GetNetworkIdentity(player, out identity))
            {
                identity.SetDynamicAssetId(assetId);
            }
            return InternalReplacePlayerForConnection(conn, player);
        }

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player)
        {
            return InternalReplacePlayerForConnection(conn, player);
        }

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, Guid assetId)
        {
            NetworkIdentity identity;
            if (GetNetworkIdentity(player, out identity))
            {
                identity.SetDynamicAssetId(assetId);
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
            identity.SetConnectionToClient(conn);

            SetClientReady(conn);

            if (SetupLocalPlayerForConnection(conn, identity))
            {
                return true;
            }

            if (LogFilter.Debug) { Debug.Log("Adding new playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId); }

            FinishPlayerForConnection(conn, identity, playerGameObject);
            if (identity.localPlayerAuthority)
            {
                identity.SetClientOwner(conn);
            }
            return true;
        }

        static bool SetupLocalPlayerForConnection(NetworkConnection conn, NetworkIdentity identity)
        {
            if (LogFilter.Debug) { Debug.Log("NetworkServer SetupLocalPlayerForConnection netID:" + identity.netId); }

            var localConnection = conn as ULocalConnectionToClient;
            if (localConnection != null)
            {
                if (LogFilter.Debug) { Debug.Log("NetworkServer AddPlayer handling ULocalConnectionToClient"); }

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

            OwnerMessage owner = new OwnerMessage();
            owner.netId = identity.netId;
            conn.Send((short)MsgType.Owner, owner);
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
            if (LogFilter.Debug) { Debug.Log("NetworkServer ReplacePlayer"); }

            // is there already an owner that is a different object??
            if (conn.playerController != null)
            {
                conn.playerController.SetNotLocalPlayer();
                conn.playerController.ClearClientOwner();
            }

            conn.SetPlayerController(playerNetworkIdentity);

            // Set the connection on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients)
            playerNetworkIdentity.SetConnectionToClient(conn);

            //NOTE: DONT set connection ready.

            if (LogFilter.Debug) { Debug.Log("NetworkServer ReplacePlayer setup local"); }

            if (SetupLocalPlayerForConnection(conn, playerNetworkIdentity))
            {
                return true;
            }

            if (LogFilter.Debug) { Debug.Log("Replacing playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId); }

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
            if (LogFilter.Debug) { Debug.Log("SetClientReadyInternal for conn:" + conn.connectionId); }

            if (conn.isReady)
            {
                if (LogFilter.Debug) { Debug.Log("SetClientReady conn " + conn.connectionId + " already ready"); }
                return;
            }

            if (conn.playerController == null)
            {
                // this is now allowed
                if (LogFilter.Debug) { Debug.LogWarning("Ready with no player object"); }
            }

            conn.isReady = true;

            var localConnection = conn as ULocalConnectionToClient;
            if (localConnection != null)
            {
                if (LogFilter.Debug) { Debug.Log("NetworkServer Ready handling ULocalConnectionToClient"); }

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
                            if (LogFilter.Debug) { Debug.Log("LocalClient.SetSpawnObject calling OnStartClient"); }
                            identity.OnStartClient();
                        }
                    }
                }
                return;
            }

            // Spawn/update all current server objects
            if (LogFilter.Debug) { Debug.Log("Spawning " + NetworkIdentity.spawned.Count + " objects for conn " + conn.connectionId); }

            conn.Send((short)MsgType.SpawnStarted, new ObjectSpawnStartedMessage());

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

                if (LogFilter.Debug) { Debug.Log("Sending spawn message for current server objects name='" + identity.name + "' netId=" + identity.netId); }

                bool visible = identity.OnCheckObserver(conn);
                if (visible)
                {
                    identity.AddObserver(conn);
                }
            }

            conn.Send((short)MsgType.SpawnFinished, new ObjectSpawnFinishedMessage());
        }

        internal static void ShowForConnection(NetworkIdentity identity, NetworkConnection conn)
        {
            if (conn.isReady)
                SendSpawnMessage(identity, conn);
        }

        internal static void HideForConnection(NetworkIdentity identity, NetworkConnection conn)
        {
            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            msg.netId = identity.netId;
            conn.Send((short)MsgType.ObjectHide, msg);
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
                if (LogFilter.Debug) { Debug.Log("PlayerNotReady " + conn); }
                conn.isReady = false;
                conn.RemoveObservers();

                NotReadyMessage msg = new NotReadyMessage();
                conn.Send((short)MsgType.NotReady, msg);
            }
        }

        // default ready handler.
        static void OnClientReadyMessage(NetworkMessage netMsg)
        {
            if (LogFilter.Debug) { Debug.Log("Default handler for ready message from " + netMsg.conn); }
            SetClientReady(netMsg.conn);
        }

        // default remove player handler
        static void OnRemovePlayerMessage(NetworkMessage netMsg)
        {
            if (netMsg.conn.playerController != null)
            {
                Destroy(netMsg.conn.playerController.gameObject);
                netMsg.conn.RemovePlayerController();
            }
            else
            {
                Debug.LogError("Received remove player message but connection has no player");
            }
        }

        // Handle command from specific player, this could be one of multiple players on a single client
        static void OnCommandMessage(NetworkMessage netMsg)
        {
            CommandMessage message = netMsg.ReadMessage<CommandMessage>();

            NetworkIdentity identity;
            if (!NetworkIdentity.spawned.TryGetValue(message.netId, out identity))
            {
                Debug.LogWarning("Spawned object not found when handling Command message [netId=" + message.netId + "]");
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            if (netMsg.conn.playerController != null && netMsg.conn.playerController.netId != identity.netId)
            {
                if (identity.clientAuthorityOwner != netMsg.conn)
                {
                    Debug.LogWarning("Command for object without authority [netId=" + message.netId + "]");
                    return;
                }
            }

            if (LogFilter.Debug) { Debug.Log("OnCommandMessage for netId=" + message.netId + " conn=" + netMsg.conn); }
            identity.HandleCommand(message.componentIndex, message.functionHash, new NetworkReader(message.payload));
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

            if (LogFilter.Debug) { Debug.Log("SpawnObject instance ID " + identity.netId + " asset ID " + identity.assetId); }

            identity.RebuildObservers(true);
            //SendSpawnMessage(objNetworkIdentity, null);
        }

        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly)
                return;

            if (LogFilter.Debug) { Debug.Log("Server SendSpawnMessage: name=" + identity.name + " sceneId=" + identity.sceneId + " netid=" + identity.netId); } // for easier debugging

            // 'identity' is a prefab that should be spawned
            if (identity.sceneId == 0)
            {
                SpawnPrefabMessage msg = new SpawnPrefabMessage();
                msg.netId = identity.netId;
                msg.assetId = identity.assetId;
                msg.position = identity.transform.position;
                msg.rotation = identity.transform.rotation;

                // serialize all components with initialState = true
                msg.payload = identity.OnSerializeAllSafely(true);

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send((short)MsgType.SpawnPrefab, msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(identity, (short)MsgType.SpawnPrefab, msg);
                }
            }
            // 'identity' is a scene object that should be spawned again
            else
            {
                SpawnSceneObjectMessage msg = new SpawnSceneObjectMessage();
                msg.netId = identity.netId;
                msg.sceneId = identity.sceneId;
                msg.position = identity.transform.position;
                msg.rotation = identity.transform.rotation;

                // include synch data
                msg.payload = identity.OnSerializeAllSafely(true);

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send((short)MsgType.SpawnSceneObject, msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(identity, (short)MsgType.SpawnSceneObject, msg);
                }
            }
        }

        public static void DestroyPlayerForConnection(NetworkConnection conn)
        {
            if (conn.playerController == null)
            {
                // null if players are still in a lobby etc., no need to show a warning
                return;
            }

            if (conn.clientOwnedObjects != null)
            {
                HashSet<uint> tmp = new HashSet<uint>(conn.clientOwnedObjects);
                foreach (uint netId in tmp)
                {
                    NetworkIdentity identity;
                    if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
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
                NetworkIdentity identity;
                if (GetNetworkIdentity(obj, out identity))
                {
                    identity.SetDynamicAssetId(assetId);
                }
                SpawnObject(obj);
            }
        }

        static void DestroyObject(NetworkIdentity identity, bool destroyServerObject)
        {
            if (LogFilter.Debug) { Debug.Log("DestroyObject instance:" + identity.netId); }
            NetworkIdentity.spawned.Remove(identity.netId);

            if (identity.clientAuthorityOwner != null)
            {
                identity.clientAuthorityOwner.RemoveOwnedObject(identity);
            }

            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            msg.netId = identity.netId;
            SendToObservers(identity, (short)MsgType.ObjectDestroy, msg);

            identity.ClearObservers();
            if (NetworkClient.active && s_LocalClientActive)
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
                if (LogFilter.Debug) { Debug.Log("NetworkServer DestroyObject is null"); }
                return;
            }

            NetworkIdentity identity;
            if (GetNetworkIdentity(obj, out identity))
            {
                DestroyObject(identity, true);
            }
        }

        public static void UnSpawn(GameObject obj)
        {
            if (obj == null)
            {
                if (LogFilter.Debug) { Debug.Log("NetworkServer UnspawnObject is null"); }
                return;
            }

            NetworkIdentity identity;
            if (GetNetworkIdentity(obj, out identity))
            {
                DestroyObject(identity, false);
            }
        }

        internal static bool InvokeBytes(ULocalConnectionToServer conn, byte[] buffer)
        {
            ushort msgType;
            byte[] content;
            if (Protocol.UnpackMessage(buffer, out msgType, out content))
            {
                if (handlers.ContainsKey((short)msgType) && s_LocalConnection != null)
                {
                    // this must be invoked with the connection to the client, not the client's connection to the server
                    s_LocalConnection.InvokeHandler((short)msgType, new NetworkReader(content));
                    return true;
                }
            }
            Debug.LogError("InvokeBytes: failed to unpack message:" + BitConverter.ToString(buffer));
            return false;
        }

        [Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
        public static GameObject FindLocalObject(uint netId)
        {
            NetworkIdentity identity;
            if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
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
                    if (LogFilter.Debug) { Debug.Log("SpawnObjects sceneId:" + identity.sceneId + " name:" + identity.gameObject.name); }
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
