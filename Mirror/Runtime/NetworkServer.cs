using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

namespace Mirror
{
    public sealed class NetworkServer
    {
        static bool s_Active;
        static bool s_DontListen;
        static bool s_LocalClientActive;
        static ULocalConnectionToClient s_LocalConnection;

        static NetworkScene s_NetworkScene = new NetworkScene();

        static Dictionary<short, NetworkMessageDelegate> s_MessageHandlers = new Dictionary<short, NetworkMessageDelegate>();
        static List<NetworkConnection> s_Connections = new List<NetworkConnection>();

        static int s_ServerHostId = -1;
        static int s_ServerPort = -1;
        static bool s_UseWebSockets;
        static bool s_Initialized = false;

        // original HLAPI has .localConnections list with only m_LocalConnection in it
        // (for downwards compatibility because they removed the real localConnections list a while ago)
        // => removed it for easier code. use .localConection now!
        public static NetworkConnection localConnection { get { return (NetworkConnection)s_LocalConnection; } }

        public static int listenPort { get { return s_ServerPort; } }
        public static int serverHostId { get { return s_ServerHostId; } }

        public static List<NetworkConnection> connections { get { return s_Connections; } }
        public static Dictionary<short, NetworkMessageDelegate> handlers { get { return s_MessageHandlers; } }

        public static Dictionary<NetworkInstanceId, NetworkIdentity> objects { get { return s_NetworkScene.localObjects; } }
        public static bool dontListen { get { return s_DontListen; } set { s_DontListen = value; } }
        public static bool useWebSockets { get { return s_UseWebSockets; } set { s_UseWebSockets = value; } }

        public static bool active { get { return s_Active; } }
        public static bool localClientActive { get { return s_LocalClientActive; } }

        static Type s_NetworkConnectionClass = typeof(NetworkConnection);
        public static Type networkConnectionClass { get { return s_NetworkConnectionClass; } }
        public static void SetNetworkConnectionClass<T>() where T : NetworkConnection
        {
            s_NetworkConnectionClass = typeof(T);
        }

        public static void Reset()
        {
            s_Active = false;
        }

        public static void Shutdown()
        {
            if (s_Initialized)
            {
                InternalDisconnectAll();

                if (s_DontListen)
                {
                    // was never started, so dont stop
                }
                else
                {
                    Transport.layer.ServerStop();
                    s_ServerHostId = -1;
                }

                s_Initialized = false;
            }
            s_DontListen = false;
            s_Active = false;
        }

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            if (LogFilter.logDev) { Debug.Log("NetworkServer Created version " + Version.Current); }

            if (LogFilter.logDebug) { Debug.Log("NetworkServer initialize."); }
        }

        internal static void RegisterMessageHandlers()
        {
            RegisterHandler((short)MsgType.Ready, OnClientReadyMessage);
            RegisterHandler((short)MsgType.Command, OnCommandMessage);
            RegisterHandler((short)MsgType.LocalPlayerTransform, NetworkTransform.HandleTransform);
            RegisterHandler((short)MsgType.LocalChildTransform, NetworkTransformChild.HandleChildTransform);
            RegisterHandler((short)MsgType.RemovePlayer, OnRemovePlayerMessage);
            RegisterHandler((short)MsgType.Animation, NetworkAnimator.OnAnimationServerMessage);
            RegisterHandler((short)MsgType.AnimationParameters, NetworkAnimator.OnAnimationParametersServerMessage);
            RegisterHandler((short)MsgType.AnimationTrigger, NetworkAnimator.OnAnimationTriggerServerMessage);
        }

        public static bool Listen(int serverPort, int maxConnections)
        {
            return InternalListen("127.0.0.1", serverPort, maxConnections);
        }

        public static bool Listen(string ipAddress, int serverPort, int maxConnections)
        {
            return InternalListen(ipAddress, serverPort, maxConnections);
        }

        internal static bool InternalListen(string ipAddress, int serverPort, int maxConnections)
        {
            Initialize();

            // only start server if we want to listen
            if (!s_DontListen)
            {
                s_ServerPort = serverPort;

                if (s_UseWebSockets)
                {
                    Transport.layer.ServerStartWebsockets(ipAddress, serverPort, maxConnections);
                    s_ServerHostId = 0; // so it doesn't return false
                }
                else
                {
                    Transport.layer.ServerStart(ipAddress, serverPort, maxConnections);
                    s_ServerHostId = 0; // so it doesn't return false
                }

                if (s_ServerHostId == -1)
                {
                    return false;
                }

                if (LogFilter.logDebug) { Debug.Log("Server listen: " + ipAddress + ":" + s_ServerPort); }
            }

            s_Active = true;
            RegisterMessageHandlers();
            return true;
        }

        public static bool SetConnectionAtIndex(NetworkConnection conn)
        {
            while (s_Connections.Count <= conn.connectionId)
            {
                s_Connections.Add(null);
            }

            if (s_Connections[conn.connectionId] != null)
            {
                // already a connection at this index
                return false;
            }

            s_Connections[conn.connectionId] = conn;
            conn.SetHandlers(s_MessageHandlers);
            return true;
        }

        public static bool RemoveConnectionAtIndex(int connectionId)
        {
            if (connectionId < 0 || connectionId >= s_Connections.Count)
                return false;
            s_Connections.RemoveAt(connectionId);
            return true;
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
            SetConnectionAtIndex(s_LocalConnection);

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
            RemoveConnectionAtIndex(0);
        }

        internal static void SetLocalObjectOnServer(NetworkInstanceId netId, GameObject obj)
        {
            if (LogFilter.logDev) { Debug.Log("SetLocalObjectOnServer " + netId + " " + obj); }

            s_NetworkScene.SetLocalObject(netId, obj, false, true);
        }

        internal static void ActivateLocalClientScene()
        {
            if (s_LocalClientActive)
                return;

            // ClientScene for a local connection is becoming active. any spawned objects need to be started as client objects
            s_LocalClientActive = true;
            foreach (var uv in objects.Values)
            {
                if (!uv.isClient)
                {
                    if (LogFilter.logDev) { Debug.Log("ActivateClientScene " + uv.netId + " " + uv.gameObject); }

                    ClientScene.SetLocalObject(uv.netId, uv.gameObject);
                    uv.OnStartClient();
                }
            }
        }

        // this is like SendToReady - but it doesn't check the ready flag on the connection.
        // this is used for ObjectDestroy messages.
        static bool SendToObservers(GameObject contextObj, short msgType, MessageBase msg)
        {
            if (LogFilter.logDev) { Debug.Log("Server.SendToObservers id:" + msgType); }

            var uv = contextObj.GetComponent<NetworkIdentity>();
            if (uv != null && uv.observers != null)
            {
                bool result = true;
                for (int i = 0; i < uv.observers.Count; ++i)
                {
                    var conn = uv.observers[i];
                    result &= conn.Send(msgType, msg);
                }
                return result;
            }
            return false;
        }

        public static bool SendToAll(short msgType, MessageBase msg)
        {
            if (LogFilter.logDev) { Debug.Log("Server.SendToAll id:" + msgType); }

            bool result = true;
            for (int i = 0; i < connections.Count; i++)
            {
                NetworkConnection conn = connections[i];
                if (conn != null)
                    result &= conn.Send(msgType, msg);
            }
            return result;
        }

        public static bool SendToReady(GameObject contextObj, short msgType, MessageBase msg)
        {
            if (LogFilter.logDev) { Debug.Log("Server.SendToReady msgType:" + msgType); }

            if (contextObj == null)
            {
                // no context.. send to all ready connections
                for (int i = 0; i < connections.Count; i++)
                {
                    NetworkConnection conn = connections[i];
                    if (conn != null && conn.isReady)
                    {
                        conn.Send(msgType, msg);
                    }
                }
                return true;
            }

            NetworkIdentity uv = contextObj.GetComponent<NetworkIdentity>();
            if (uv != null && uv.observers != null)
            {
                bool result = true;
                for (int i = 0; i < uv.observers.Count; ++i)
                {
                    NetworkConnection conn = uv.observers[i];
                    if (conn.isReady)
                    {
                        result &= conn.Send(msgType, msg);
                    }
                }
                return result;
            }
            return false;
        }

        public static void DisconnectAll()
        {
            InternalDisconnectAll();
        }

        public static void DisconnectAllConnections()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                NetworkConnection conn = connections[i];
                if (conn != null)
                {
                    conn.Disconnect();
                    conn.Dispose();
                }
            }
        }

        internal static void InternalDisconnectAll()
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

        // The user should never need to pump the update loop manually
        internal static void Update()
        {
            InternalUpdate();
        }

        static void UpdateServerObjects()
        {
            // vis2k: original code only removed null entries every 100 frames. this was unnecessarily complicated and
            // probably even slower than removing null entries each time (hence less iterations next time).
            List<NetworkInstanceId> remove = new List<NetworkInstanceId>();
            foreach (var kvp in objects)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    kvp.Value.UNetUpdate();
                }
                else
                {
                    remove.Add(kvp.Key);
                }
            }

            // now remove
            foreach (NetworkInstanceId key in remove)
            {
                objects.Remove(key);
            }
        }

        internal static void InternalUpdate()
        {
            if (s_ServerHostId == -1)
                return;

            int connectionId;
            TransportEvent transportEvent;
            byte[] data;
            while (Transport.layer.ServerGetNextMessage(out connectionId, out transportEvent, out data))
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
            if (LogFilter.logDebug) { Debug.Log("Server accepted client:" + connectionId); }

            if (error != 0)
            {
                GenerateConnectError(error);
                return;
            }

            // get ip address from connection
            string address;
            Transport.layer.GetConnectionInfo(connectionId, out address);

            // add player info
            NetworkConnection conn = (NetworkConnection)Activator.CreateInstance(s_NetworkConnectionClass);
            conn.SetHandlers(s_MessageHandlers);
            conn.Initialize(address, s_ServerHostId, connectionId);

            // add connection at correct index
            while (s_Connections.Count <= connectionId)
            {
                s_Connections.Add(null);
            }
            s_Connections[connectionId] = conn;

            OnConnected(conn);
        }

        static void OnConnected(NetworkConnection conn)
        {
            if (LogFilter.logDebug) { Debug.Log("Server accepted client:" + conn.connectionId); }
            conn.InvokeHandlerNoData((short)MsgType.Connect);
        }

        static void HandleDisconnect(int connectionId, byte error)
        {
            if (LogFilter.logDebug) { Debug.Log("Server disconnect client:" + connectionId); }

            var conn = FindConnection(connectionId);
            if (conn == null)
            {
                return;
            }

            conn.Disconnect();
            s_Connections[connectionId] = null;
            if (LogFilter.logDebug) { Debug.Log("Server lost client:" + connectionId); }

            OnDisconnected(conn);
        }

        static void OnDisconnected(NetworkConnection conn)
        {
            conn.InvokeHandlerNoData((short)MsgType.Disconnect);

            if (conn.playerControllers.Any(pc => pc.gameObject != null))
            {
                //NOTE: should there be default behaviour here to destroy the associated player?
                if (LogFilter.logWarn) { Debug.LogWarning("Player not destroyed when connection disconnected."); }
            }

            if (LogFilter.logDebug) { Debug.Log("Server lost client:" + conn.connectionId); }
            conn.RemoveObservers();
            conn.Dispose();
        }

        public static NetworkConnection FindConnection(int connectionId)
        {
            if (connectionId < 0 || connectionId >= s_Connections.Count)
                return null;

            return s_Connections[connectionId];
        }

        static void HandleData(int connectionId, byte[] data, byte error)
        {
            var conn = FindConnection(connectionId);
            if (conn == null)
            {
                if (LogFilter.logError) { Debug.LogError("HandleData Unknown connectionId:" + connectionId); }
                return;
            }

            OnData(conn, data);
        }

        static void OnData(NetworkConnection conn, byte[] data)
        {
            conn.TransportReceive(data);
        }

        static void GenerateConnectError(byte error)
        {
            if (LogFilter.logError) { Debug.LogError("UNet Server Connect Error: " + error); }
            GenerateError(null, error);
        }

        /* TODO use or remove
        static void GenerateDataError(NetworkConnection conn, byte error)
        {
            NetworkError dataError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Server Data Error: " + dataError); }
            GenerateError(conn, error);
        }

        static void GenerateDisconnectError(NetworkConnection conn, byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            if (LogFilter.logError) { Debug.LogError("UNet Server Disconnect Error: " + disconnectError + " conn:[" + conn + "]:" + conn.connectionId); }
            GenerateError(conn, error);
        }
        */

        static void GenerateError(NetworkConnection conn, byte error)
        {
            if (handlers.ContainsKey((short)MsgType.Error))
            {
                ErrorMessage msg = new ErrorMessage();
                msg.errorCode = error;

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
            if (s_MessageHandlers.ContainsKey(msgType))
            {
                if (LogFilter.logDebug) { Debug.Log("NetworkServer.RegisterHandler replacing " + msgType); }
            }
            s_MessageHandlers[msgType] = handler;
        }

        static public void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((short)msgType, handler);
        }

        public static void UnregisterHandler(short msgType)
        {
            s_MessageHandlers.Remove(msgType);
        }

        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((short)msgType);
        }

        public static void ClearHandlers()
        {
            s_MessageHandlers.Clear();
        }

        public static void ClearSpawners()
        {
            NetworkScene.ClearSpawners();
        }

        // send this message to the player only
        public static void SendToClientOfPlayer(GameObject player, short msgType, MessageBase msg)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn != null)
                {
                    for (int j = 0; j < conn.playerControllers.Count; j++)
                    {
                        if (conn.playerControllers[j].IsValid && conn.playerControllers[j].gameObject == player)
                        {
                            conn.Send(msgType, msg);
                            return;
                        }
                    }
                }
            }

            if (LogFilter.logError) { Debug.LogError("Failed to send message to player object '" + player.name + ", not found in connection list"); }
        }

        public static void SendToClient(int connectionId, short msgType, MessageBase msg)
        {
            if (connectionId < connections.Count)
            {
                var conn = connections[connectionId];
                if (conn != null)
                {
                    conn.Send(msgType, msg);
                    return;
                }
            }
            if (LogFilter.logError) { Debug.LogError("Failed to send message to connection ID '" + connectionId + ", not found in connection list"); }
        }

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, short playerControllerId, NetworkHash128 assetId)
        {
            NetworkIdentity id;
            if (GetNetworkIdentity(player, out id))
            {
                id.SetDynamicAssetId(assetId);
            }
            return InternalReplacePlayerForConnection(conn, player, playerControllerId);
        }

        public static bool ReplacePlayerForConnection(NetworkConnection conn, GameObject player, short playerControllerId)
        {
            return InternalReplacePlayerForConnection(conn, player, playerControllerId);
        }

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, short playerControllerId, NetworkHash128 assetId)
        {
            NetworkIdentity id;
            if (GetNetworkIdentity(player, out id))
            {
                id.SetDynamicAssetId(assetId);
            }
            return InternalAddPlayerForConnection(conn, player, playerControllerId);
        }

        public static bool AddPlayerForConnection(NetworkConnection conn, GameObject player, short playerControllerId)
        {
            return InternalAddPlayerForConnection(conn, player, playerControllerId);
        }

        internal static bool InternalAddPlayerForConnection(NetworkConnection conn, GameObject playerGameObject, short playerControllerId)
        {
            NetworkIdentity playerNetworkIdentity;
            if (!GetNetworkIdentity(playerGameObject, out playerNetworkIdentity))
            {
                if (LogFilter.logError) { Debug.Log("AddPlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + playerGameObject); }
                return false;
            }
            playerNetworkIdentity.Reset();

            if (!CheckPlayerControllerIdForConnection(conn, playerControllerId))
                return false;

            // cannot have a player object in "Add" version
            PlayerController oldController = null;
            GameObject oldPlayer = null;
            if (conn.GetPlayerController(playerControllerId, out oldController))
            {
                oldPlayer = oldController.gameObject;
            }
            if (oldPlayer != null)
            {
                if (LogFilter.logError) { Debug.Log("AddPlayer: player object already exists for playerControllerId of " + playerControllerId); }
                return false;
            }

            PlayerController newPlayerController = new PlayerController(playerGameObject, playerControllerId);
            conn.SetPlayerController(newPlayerController);

            // Set the playerControllerId on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients and that sets the playerControllerId there)
            playerNetworkIdentity.SetConnectionToClient(conn, newPlayerController.playerControllerId);

            SetClientReady(conn);

            if (SetupLocalPlayerForConnection(conn, playerNetworkIdentity, newPlayerController))
            {
                return true;
            }

            if (LogFilter.logDebug) { Debug.Log("Adding new playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId); }

            FinishPlayerForConnection(conn, playerNetworkIdentity, playerGameObject);
            if (playerNetworkIdentity.localPlayerAuthority)
            {
                playerNetworkIdentity.SetClientOwner(conn);
            }
            return true;
        }

        static bool CheckPlayerControllerIdForConnection(NetworkConnection conn, short playerControllerId)
        {
            if (playerControllerId < 0)
            {
                if (LogFilter.logError) { Debug.LogError("AddPlayer: playerControllerId of " + playerControllerId + " is negative"); }
                return false;
            }
            if (playerControllerId > PlayerController.MaxPlayersPerClient)
            {
                if (LogFilter.logError) { Debug.Log("AddPlayer: playerControllerId of " + playerControllerId + " is too high. max is " + PlayerController.MaxPlayersPerClient); }
                return false;
            }
            if (playerControllerId > PlayerController.MaxPlayersPerClient / 2)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("AddPlayer: playerControllerId of " + playerControllerId + " is unusually high"); }
            }
            return true;
        }

        static bool SetupLocalPlayerForConnection(NetworkConnection conn, NetworkIdentity uv, PlayerController newPlayerController)
        {
            if (LogFilter.logDev) { Debug.Log("NetworkServer SetupLocalPlayerForConnection netID:" + uv.netId); }

            var localConnection = conn as ULocalConnectionToClient;
            if (localConnection != null)
            {
                if (LogFilter.logDev) { Debug.Log("NetworkServer AddPlayer handling ULocalConnectionToClient"); }

                // Spawn this player for other players, instead of SpawnObject:
                if (uv.netId.IsEmpty())
                {
                    // it is allowed to provide an already spawned object as the new player object.
                    // so dont spawn it again.
                    uv.OnStartServer(true);
                }
                uv.RebuildObservers(true);
                SendSpawnMessage(uv, null);

                // Set up local player instance on the client instance and update local object map
                localConnection.localClient.AddLocalPlayer(newPlayerController);
                uv.SetClientOwner(conn);

                // Trigger OnAuthority
                uv.ForceAuthority(true);

                // Trigger OnStartLocalPlayer
                uv.SetLocalPlayer(newPlayerController.playerControllerId);
                return true;
            }
            return false;
        }

        static void FinishPlayerForConnection(NetworkConnection conn, NetworkIdentity uv, GameObject playerGameObject)
        {
            if (uv.netId.IsEmpty())
            {
                // it is allowed to provide an already spawned object as the new player object.
                // so dont spawn it again.
                Spawn(playerGameObject);
            }

            OwnerMessage owner = new OwnerMessage();
            owner.netId = uv.netId;
            owner.playerControllerId = uv.playerControllerId;
            conn.Send((short)MsgType.Owner, owner);
        }

        internal static bool InternalReplacePlayerForConnection(NetworkConnection conn, GameObject playerGameObject, short playerControllerId)
        {
            NetworkIdentity playerNetworkIdentity;
            if (!GetNetworkIdentity(playerGameObject, out playerNetworkIdentity))
            {
                if (LogFilter.logError) { Debug.LogError("ReplacePlayer: playerGameObject has no NetworkIdentity. Please add a NetworkIdentity to " + playerGameObject); }
                return false;
            }

            if (!CheckPlayerControllerIdForConnection(conn, playerControllerId))
                return false;

            //NOTE: there can be an existing player
            if (LogFilter.logDev) { Debug.Log("NetworkServer ReplacePlayer"); }

            // is there already an owner that is a different object??
            PlayerController oldOwner;
            if (conn.GetPlayerController(playerControllerId, out oldOwner))
            {
                oldOwner.unetView.SetNotLocalPlayer();
                oldOwner.unetView.ClearClientOwner();
            }

            PlayerController newPlayerController = new PlayerController(playerGameObject, playerControllerId);
            conn.SetPlayerController(newPlayerController);

            // Set the playerControllerId on the NetworkIdentity on the server, NetworkIdentity.SetLocalPlayer is not called on the server (it is on clients and that sets the playerControllerId there)
            playerNetworkIdentity.SetConnectionToClient(conn, newPlayerController.playerControllerId);

            //NOTE: DONT set connection ready.

            if (LogFilter.logDev) { Debug.Log("NetworkServer ReplacePlayer setup local"); }

            if (SetupLocalPlayerForConnection(conn, playerNetworkIdentity, newPlayerController))
            {
                return true;
            }

            if (LogFilter.logDebug) { Debug.Log("Replacing playerGameObject object netId: " + playerGameObject.GetComponent<NetworkIdentity>().netId + " asset ID " + playerGameObject.GetComponent<NetworkIdentity>().assetId); }

            FinishPlayerForConnection(conn, playerNetworkIdentity, playerGameObject);
            if (playerNetworkIdentity.localPlayerAuthority)
            {
                playerNetworkIdentity.SetClientOwner(conn);
            }
            return true;
        }

        static bool GetNetworkIdentity(GameObject go, out NetworkIdentity view)
        {
            view = go.GetComponent<NetworkIdentity>();
            if (view == null)
            {
                if (LogFilter.logError) { Debug.LogError("UNET failure. GameObject doesn't have NetworkIdentity."); }
                return false;
            }
            return true;
        }

        public static void SetClientReady(NetworkConnection conn)
        {
            SetClientReadyInternal(conn);
        }

        internal static void SetClientReadyInternal(NetworkConnection conn)
        {
            if (LogFilter.logDebug) { Debug.Log("SetClientReadyInternal for conn:" + conn.connectionId); }

            if (conn.isReady)
            {
                if (LogFilter.logDebug) { Debug.Log("SetClientReady conn " + conn.connectionId + " already ready"); }
                return;
            }

            if (conn.playerControllers.Count == 0)
            {
                // this is now allowed
                if (LogFilter.logDebug) { Debug.LogWarning("Ready with no player object"); }
            }

            conn.isReady = true;

            var localConnection = conn as ULocalConnectionToClient;
            if (localConnection != null)
            {
                if (LogFilter.logDev) { Debug.Log("NetworkServer Ready handling ULocalConnectionToClient"); }

                // Setup spawned objects for local player
                // Only handle the local objects for the first player (no need to redo it when doing more local players)
                // and don't handle player objects here, they were done above
                foreach (NetworkIdentity uv in objects.Values)
                {
                    // Need to call OnStartClient directly here, as it's already been added to the local object dictionary
                    // in the above SetLocalPlayer call
                    if (uv != null && uv.gameObject != null)
                    {
                        var vis = uv.OnCheckObserver(conn);
                        if (vis)
                        {
                            uv.AddObserver(conn);
                        }
                        if (!uv.isClient)
                        {
                            if (LogFilter.logDev) { Debug.Log("LocalClient.SetSpawnObject calling OnStartClient"); }
                            uv.OnStartClient();
                        }
                    }
                }
                return;
            }

            // Spawn/update all current server objects
            if (LogFilter.logDebug) { Debug.Log("Spawning " + objects.Count + " objects for conn " + conn.connectionId); }

            ObjectSpawnFinishedMessage msg = new ObjectSpawnFinishedMessage();
            msg.state = 0;
            conn.Send((short)MsgType.SpawnFinished, msg);

            foreach (NetworkIdentity uv in objects.Values)
            {
                if (uv == null)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("Invalid object found in server local object list (null NetworkIdentity)."); }
                    continue;
                }
                if (!uv.gameObject.activeSelf)
                {
                    continue;
                }

                if (LogFilter.logDebug) { Debug.Log("Sending spawn message for current server objects name='" + uv.gameObject.name + "' netId=" + uv.netId); }

                var vis = uv.OnCheckObserver(conn);
                if (vis)
                {
                    uv.AddObserver(conn);
                }
            }

            msg.state = 1;
            conn.Send((short)MsgType.SpawnFinished, msg);
        }

        internal static void ShowForConnection(NetworkIdentity uv, NetworkConnection conn)
        {
            if (conn.isReady)
                SendSpawnMessage(uv, conn);
        }

        internal static void HideForConnection(NetworkIdentity uv, NetworkConnection conn)
        {
            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            msg.netId = uv.netId;
            conn.Send((short)MsgType.ObjectHide, msg);
        }

        // call this to make all the clients not ready, such as when changing levels.
        public static void SetAllClientsNotReady()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                if (conn != null)
                {
                    SetClientNotReady(conn);
                }
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
                if (LogFilter.logDebug) { Debug.Log("PlayerNotReady " + conn); }
                conn.isReady = false;
                conn.RemoveObservers();

                NotReadyMessage msg = new NotReadyMessage();
                conn.Send((short)MsgType.NotReady, msg);
            }
        }

        // default ready handler.
        static void OnClientReadyMessage(NetworkMessage netMsg)
        {
            if (LogFilter.logDebug) { Debug.Log("Default handler for ready message from " + netMsg.conn); }
            SetClientReady(netMsg.conn);
        }

        // default remove player handler
        static void OnRemovePlayerMessage(NetworkMessage netMsg)
        {
            RemovePlayerMessage msg = new RemovePlayerMessage();
            netMsg.ReadMessage(msg);

            PlayerController player = null;
            netMsg.conn.GetPlayerController(msg.playerControllerId, out player);
            if (player != null)
            {
                netMsg.conn.RemovePlayerController(msg.playerControllerId);
                Destroy(player.gameObject);
            }
            else
            {
                if (LogFilter.logError) { Debug.LogError("Received remove player message but could not find the player ID: " + msg.playerControllerId); }
            }
        }

        // Handle command from specific player, this could be one of multiple players on a single client
        static  void OnCommandMessage(NetworkMessage netMsg)
        {
            CommandMessage message = netMsg.ReadMessage<CommandMessage>();

            var cmdObject = FindLocalObject(message.netId);
            if (cmdObject == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Instance not found when handling Command message [netId=" + message.netId + "]"); }
                return;
            }

            var uv = cmdObject.GetComponent<NetworkIdentity>();
            if (uv == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("NetworkIdentity deleted when handling Command message [netId=" + message.netId + "]"); }
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // => check if there is no owner
            if (!netMsg.conn.playerControllers.Any(
                pc => pc.gameObject != null &&
                pc.gameObject.GetComponent<NetworkIdentity>().netId == uv.netId))
            {
                if (uv.clientAuthorityOwner != netMsg.conn)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("Command for object without authority [netId=" + message.netId + "]"); }
                    return;
                }
            }

            if (LogFilter.logDev) { Debug.Log("OnCommandMessage for netId=" + message.netId + " conn=" + netMsg.conn); }
            uv.HandleCommand(message.cmdHash, new NetworkReader(message.payload));
        }

        internal static void SpawnObject(GameObject obj)
        {
            if (!NetworkServer.active)
            {
                if (LogFilter.logError) { Debug.LogError("SpawnObject for " + obj + ", NetworkServer is not active. Cannot spawn objects without an active server."); }
                return;
            }

            NetworkIdentity objNetworkIdentity;
            if (!GetNetworkIdentity(obj, out objNetworkIdentity))
            {
                if (LogFilter.logError) { Debug.LogError("SpawnObject " + obj + " has no NetworkIdentity. Please add a NetworkIdentity to " + obj); }
                return;
            }
            objNetworkIdentity.Reset();

            objNetworkIdentity.OnStartServer(false);

            if (LogFilter.logDebug) { Debug.Log("SpawnObject instance ID " + objNetworkIdentity.netId + " asset ID " + objNetworkIdentity.assetId); }

            objNetworkIdentity.RebuildObservers(true);
            //SendSpawnMessage(objNetworkIdentity, null);
        }

        internal static void SendSpawnMessage(NetworkIdentity uv, NetworkConnection conn)
        {
            if (uv.serverOnly)
                return;

            if (LogFilter.logDebug) { Debug.Log("Server SendSpawnMessage: name=" + uv.name + " sceneId=" + uv.sceneId + " netid=" + uv.netId); } // for easier debugging

            // 'uv' is a prefab that should be spawned
            if (uv.sceneId.IsEmpty())
            {
                SpawnPrefabMessage msg = new SpawnPrefabMessage();
                msg.netId = uv.netId;
                msg.assetId = uv.assetId;
                msg.position = uv.transform.position;
                msg.rotation = uv.transform.rotation;

                // serialize all components with initialState = true
                NetworkWriter writer = new NetworkWriter();
                uv.OnSerializeAllSafely(writer, true);
                msg.payload = writer.ToArray();

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send((short)MsgType.SpawnPrefab, msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(uv.gameObject, (short)MsgType.SpawnPrefab, msg);
                }
            }
            // 'uv' is a scene object that should be spawned again
            else
            {
                SpawnSceneObjectMessage msg = new SpawnSceneObjectMessage();
                msg.netId = uv.netId;
                msg.sceneId = uv.sceneId;
                msg.position = uv.transform.position;

                // include synch data
                NetworkWriter writer = new NetworkWriter();
                uv.OnSerializeAllSafely(writer, true);
                msg.payload = writer.ToArray();

                // conn is != null when spawning it for a client
                if (conn != null)
                {
                    conn.Send((short)MsgType.SpawnSceneObject, msg);
                }
                // conn is == null when spawning it for the local player
                else
                {
                    SendToReady(uv.gameObject, (short)MsgType.SpawnSceneObject, msg);
                }
            }
        }

        public static void DestroyPlayersForConnection(NetworkConnection conn)
        {
            if (conn.playerControllers.Count == 0)
            {
                // list is empty if players are still in a lobby etc., no need to show a warning
                return;
            }

            if (conn.clientOwnedObjects != null)
            {
                var tmp = new HashSet<NetworkInstanceId>(conn.clientOwnedObjects);
                foreach (var netId in tmp)
                {
                    var obj = FindLocalObject(netId);
                    if (obj != null)
                    {
                        DestroyObject(obj);
                    }
                }
            }

            for (int i = 0; i < conn.playerControllers.Count; i++)
            {
                var player = conn.playerControllers[i];
                if (player.IsValid)
                {
                    if (player.unetView == null)
                    {
                        // the playerController's object has been destroyed, but RemovePlayerForConnection was never called.
                        // this is ok, just dont double destroy it.
                    }
                    else
                    {
                        DestroyObject(player.unetView, true);
                    }
                    player.gameObject = null;
                }
            }
            conn.playerControllers.Clear();
        }

        static void UnSpawnObject(GameObject obj)
        {
            if (obj == null)
            {
                if (LogFilter.logDev) { Debug.Log("NetworkServer UnspawnObject is null"); }
                return;
            }

            NetworkIdentity objNetworkIdentity;
            if (!GetNetworkIdentity(obj, out objNetworkIdentity)) return;

            UnSpawnObject(objNetworkIdentity);
        }

        static void UnSpawnObject(NetworkIdentity uv)
        {
            DestroyObject(uv, false);
        }

        static void DestroyObject(GameObject obj)
        {
            if (obj == null)
            {
                if (LogFilter.logDev) { Debug.Log("NetworkServer DestroyObject is null"); }
                return;
            }

            NetworkIdentity objNetworkIdentity;
            if (!GetNetworkIdentity(obj, out objNetworkIdentity)) return;

            DestroyObject(objNetworkIdentity, true);
        }

        static void DestroyObject(NetworkIdentity uv, bool destroyServerObject)
        {
            if (LogFilter.logDebug) { Debug.Log("DestroyObject instance:" + uv.netId); }
            if (objects.ContainsKey(uv.netId))
            {
                objects.Remove(uv.netId);
            }

            if (uv.clientAuthorityOwner != null)
            {
                uv.clientAuthorityOwner.RemoveOwnedObject(uv);
            }

            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            msg.netId = uv.netId;
            SendToObservers(uv.gameObject, (short)MsgType.ObjectDestroy, msg);

            uv.ClearObservers();
            if (NetworkClient.active && s_LocalClientActive)
            {
                uv.OnNetworkDestroy();
                ClientScene.SetLocalObject(msg.netId, null);
            }

            // when unspawning, dont destroy the server's object
            if (destroyServerObject)
            {
                UnityEngine.Object.Destroy(uv.gameObject);
            }
            uv.MarkForReset();
        }

        public static void ClearLocalObjects()
        {
            objects.Clear();
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
            return (UnityEditor.PrefabUtility.GetPrefabParent(obj) == null) && (UnityEditor.PrefabUtility.GetPrefabObject(obj) != null);
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

        public static Boolean SpawnWithClientAuthority(GameObject obj, GameObject player)
        {
            var uv = player.GetComponent<NetworkIdentity>();
            if (uv == null)
            {
                Debug.LogError("SpawnWithClientAuthority player object has no NetworkIdentity");
                return false;
            }

            if (uv.connectionToClient == null)
            {
                Debug.LogError("SpawnWithClientAuthority player object is not a player.");
                return false;
            }

            return SpawnWithClientAuthority(obj, uv.connectionToClient);
        }

        public static bool SpawnWithClientAuthority(GameObject obj, NetworkConnection conn)
        {
            if (!conn.isReady)
            {
                Debug.LogError("SpawnWithClientAuthority NetworkConnection is not ready!");
                return false;
            }

            Spawn(obj);

            var uv = obj.GetComponent<NetworkIdentity>();
            if (uv == null || !uv.isServer)
            {
                // spawning the object failed.
                return false;
            }

            return uv.AssignClientAuthority(conn);
        }

        public static bool SpawnWithClientAuthority(GameObject obj, NetworkHash128 assetId, NetworkConnection conn)
        {
            Spawn(obj, assetId);

            var uv = obj.GetComponent<NetworkIdentity>();
            if (uv == null || !uv.isServer)
            {
                // spawning the object failed.
                return false;
            }

            return uv.AssignClientAuthority(conn);
        }

        public static void Spawn(GameObject obj, NetworkHash128 assetId)
        {
            if (VerifyCanSpawn(obj))
            {
                NetworkIdentity id;
                if (GetNetworkIdentity(obj, out id))
                {
                    id.SetDynamicAssetId(assetId);
                }
                SpawnObject(obj);
            }
        }

        public static void Destroy(GameObject obj)
        {
            DestroyObject(obj);
        }

        public static void UnSpawn(GameObject obj)
        {
            UnSpawnObject(obj);
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
            if (LogFilter.logError) { Debug.LogError("InvokeBytes: failed to unpack message:" + BitConverter.ToString(buffer)); }
            return false;
        }

        public static GameObject FindLocalObject(NetworkInstanceId netId)
        {
            return s_NetworkScene.FindLocalObject(netId);
        }

        static bool ValidateSceneObject(NetworkIdentity netId)
        {
            if (netId.gameObject.hideFlags == HideFlags.NotEditable || netId.gameObject.hideFlags == HideFlags.HideAndDontSave)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(netId.gameObject))
                return false;
#endif

            // If not a scene object
            if (netId.sceneId.IsEmpty())
                return false;

            return true;
        }

        public static bool SpawnObjects()
        {
            if (!active)
                return true;

            NetworkIdentity[] netIds = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            for (int i = 0; i < netIds.Length; i++)
            {
                var netId = netIds[i];
                if (!ValidateSceneObject(netId))
                    continue;

                if (LogFilter.logDebug) { Debug.Log("SpawnObjects sceneId:" + netId.sceneId + " name:" + netId.gameObject.name); }
                netId.Reset();
                netId.gameObject.SetActive(true);
            }
            for (int i = 0; i < netIds.Length; i++)
            {
                var netId = netIds[i];
                if (!ValidateSceneObject(netId))
                    continue;

                Spawn(netId.gameObject);

                // these objects are server authority - even if "localPlayerAuthority" is set on them
                netId.ForceAuthority(true);
            }
            return true;
        }
    };
}
