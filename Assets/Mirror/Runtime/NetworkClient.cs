using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public enum ConnectState
    {
        None,
        Connecting,
        Connected,
        Disconnected
    }

    /// <summary>NetworkClient with connection to server.</summary>
    public static class NetworkClient
    {
        // message handlers by messageId
        static readonly Dictionary<int, NetworkMessageDelegate> handlers =
            new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>Client's NetworkConnection to server.</summary>
        public static NetworkConnection connection { get; internal set; }

        // NetworkClient state
        internal static ConnectState connectState = ConnectState.None;

        /// <summary>IP address of the connection to server.</summary>
        // empty if the client has not connected yet.
        public static string serverIp => connection.address;

        /// <summary>active is true while a client is connecting/connected</summary>
        // (= while the network is active)
        public static bool active => connectState == ConnectState.Connecting ||
                                     connectState == ConnectState.Connected;

        /// <summary>Check if client is connecting (before connected).</summary>
        public static bool isConnecting => connectState == ConnectState.Connecting;

        /// <summary>Check if client is connected (after connecting).</summary>
        public static bool isConnected => connectState == ConnectState.Connected;

        /// <summary>NetworkClient can connect to local server in host mode too</summary>
        public static bool isLocalClient => connection is LocalConnectionToServer;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        internal static Action<NetworkConnection> OnConnectedEvent;
        internal static Action<NetworkConnection> OnDisconnectedEvent;

        /// <summary>Registered spawnable prefabs by assetId.</summary>
        public static readonly Dictionary<Guid, GameObject> prefabs =
            new Dictionary<Guid, GameObject>();

        // spawn handlers
        internal static readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers =
            new Dictionary<Guid, SpawnHandlerDelegate>();
        internal static readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers =
            new Dictionary<Guid, UnSpawnDelegate>();

        // initialization //////////////////////////////////////////////////////
        static void AddTransportHandlers()
        {
            Transport.activeTransport.OnClientConnected = OnConnected;
            Transport.activeTransport.OnClientDataReceived = OnDataReceived;
            Transport.activeTransport.OnClientDisconnected = OnDisconnected;
            Transport.activeTransport.OnClientError = OnError;
        }

        internal static void RegisterSystemHandlers(bool hostMode)
        {
            // host mode client / regular client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (hostMode)
            {
                RegisterHandler<ObjectDestroyMessage>(OnHostClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnHostClientObjectHide);
                RegisterHandler<NetworkPongMessage>((conn, msg) => {}, false);
                RegisterHandler<SpawnMessage>(OnHostClientSpawn);
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnStartedMessage>((conn, msg) => {});
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>((conn, msg) => {});
                // host mode doesn't need state updates
                RegisterHandler<UpdateVarsMessage>((conn, msg) => {});
            }
            else
            {
                RegisterHandler<ObjectDestroyMessage>(OnObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnObjectHide);
                RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong, false);
                RegisterHandler<SpawnMessage>(ClientScene.OnSpawn);
                RegisterHandler<ObjectSpawnStartedMessage>(ClientScene.OnObjectSpawnStarted);
                RegisterHandler<ObjectSpawnFinishedMessage>(ClientScene.OnObjectSpawnFinished);
                RegisterHandler<UpdateVarsMessage>(OnUpdateVarsMessage);
            }
            RegisterHandler<RpcMessage>(OnRPCMessage);
        }

        // connect /////////////////////////////////////////////////////////////
        /// <summary>Connect client to a NetworkServer by address.</summary>
        public static void Connect(string address)
        {
            // Debug.Log("Client Connect: " + address);
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(address);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        /// <summary>Connect client to a NetworkServer by Uri.</summary>
        public static void Connect(Uri uri)
        {
            // Debug.Log("Client Connect: " + uri);
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(uri);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        public static void ConnectHost()
        {
            //Debug.Log("Client Connect Host to Server");

            RegisterSystemHandlers(true);

            connectState = ConnectState.Connected;

            // create local connection objects and connect them
            LocalConnectionToServer connectionToServer = new LocalConnectionToServer();
            LocalConnectionToClient connectionToClient = new LocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;

            connection = connectionToServer;
            connection.SetHandlers(handlers);

            // create server connection to local client
            NetworkServer.SetLocalConnection(connectionToClient);
        }

        /// <summary>Connect host mode</summary>
        public static void ConnectLocalServer()
        {
            // call server OnConnected with server's connection to client
            NetworkServer.OnConnected(NetworkServer.localConnection);

            // call client OnConnected with client's connection to server
            // => previously we used to send a ConnectMessage to
            //    NetworkServer.localConnection. this would queue the message
            //    until NetworkClient.Update processes it.
            // => invoking the client's OnConnected event directly here makes
            //    tests fail. so let's do it exactly the same order as before by
            //    queueing the event for next Update!
            //OnConnectedEvent?.Invoke(connection);
            ((LocalConnectionToServer)connection).QueueConnectedEvent();
        }

        // disconnect //////////////////////////////////////////////////////////
        /// <summary>Disconnect from server.</summary>
        public static void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(connection);

            // local or remote connection?
            if (isLocalClient)
            {
                if (isConnected)
                {
                    // call client OnDisconnected with connection to server
                    // => previously we used to send a DisconnectMessage to
                    //    NetworkServer.localConnection. this would queue the
                    //    message until NetworkClient.Update processes it.
                    // => invoking the client's OnDisconnected event directly
                    //    here makes tests fail. so let's do it exactly the same
                    //    order as before by queueing the event for next Update!
                    //OnDisconnectedEvent?.Invoke(connection);
                    ((LocalConnectionToServer)connection).QueueDisconnectedEvent();
                }
                NetworkServer.RemoveLocalConnection();
            }
            else
            {
                if (connection != null)
                {
                    connection.Disconnect();
                    connection = null;
                }
            }
        }

        /// <summary>Disconnect host mode.</summary>
        // this is needed to call DisconnectMessage for the host client too.
        public static void DisconnectLocalServer()
        {
            // only if host connection is running
            if (NetworkServer.localConnection != null)
            {
                // TODO ConnectLocalServer manually sends a ConnectMessage to the
                // local connection. should we send a DisconnectMessage here too?
                // (if we do then we get an Unknown Message ID log)
                //NetworkServer.localConnection.Send(new DisconnectMessage());
                NetworkServer.OnDisconnected(NetworkServer.localConnection.connectionId);
            }
        }

        // transport events ////////////////////////////////////////////////////
        static void OnConnected()
        {
            if (connection != null)
            {
                // reset network time stats
                NetworkTime.Reset();

                // the handler may want to send messages to the client
                // thus we should set the connected state before calling the handler
                connectState = ConnectState.Connected;
                NetworkTime.UpdateClient();
                OnConnectedEvent?.Invoke(connection);
            }
            else Debug.LogError("Skipped Connect message handling because connection is null.");
        }

        internal static void OnDataReceived(ArraySegment<byte> data, int channelId)
        {
            if (connection != null)
            {
                connection.TransportReceive(data, channelId);
            }
            else Debug.LogError("Skipped Data message handling because connection is null.");
        }

        static void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(connection);

            if (connection != null) OnDisconnectedEvent?.Invoke(connection);
        }

        static void OnError(Exception exception) => Debug.LogException(exception);

        // send ////////////////////////////////////////////////////////////////
        /// <summary>Send a NetworkMessage to the server over the given channel.</summary>
        public static void Send<T>(T message, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            if (connection != null)
            {
                if (connectState == ConnectState.Connected)
                {
                    connection.Send(message, channelId);
                }
                else Debug.LogError("NetworkClient Send when not connected to a server");
            }
            else Debug.LogError("NetworkClient Send with no connection");
        }

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            RegisterHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Unregister a message handler of type T.</summary>
        public static bool UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            // use int to minimize collisions
            int msgType = MessagePacking.GetId<T>();
            return handlers.Remove(msgType);
        }

        // spawnable prefabs ///////////////////////////////////////////////////
        /// <summary>Find the registered prefab for this asset id.</summary>
        // Useful for debuggers
        public static bool GetPrefab(Guid assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != Guid.Empty &&
                   prefabs.TryGetValue(assetId, out prefab) && prefab != null;
        }

        /// <summary>Validates Prefab then adds it to prefabs dictionary.</summary>
        static void RegisterPrefabIdentity(NetworkIdentity prefab)
        {
            if (prefab.assetId == Guid.Empty)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (prefab.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                Debug.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            if (prefabs.ContainsKey(prefab.assetId))
            {
                GameObject existingPrefab = prefabs[prefab.assetId];
                Debug.LogWarning($"Replacing existing prefab with assetId '{prefab.assetId}'. Old prefab '{existingPrefab.name}', New prefab '{prefab.name}'");
            }

            if (spawnHandlers.ContainsKey(prefab.assetId) || unspawnHandlers.ContainsKey(prefab.assetId))
            {
                Debug.LogWarning($"Adding prefab '{prefab.name}' with assetId '{prefab.assetId}' when spawnHandlers with same assetId already exists.");
            }

            // Debug.Log($"Registering prefab '{prefab.name}' as asset:{prefab.assetId}");

            prefabs[prefab.assetId] = prefab.gameObject;
        }

        /// <summary>Register spawnable prefab with custom assetId.</summary>
        // Note: newAssetId can not be set on GameObjects that already have an assetId
        // Note: registering with assetId is useful for assetbundles etc. a lot
        //       of people use this.
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not register prefab because it was null");
                return;
            }

            if (newAssetId == Guid.Empty)
            {
                Debug.LogError($"Could not register '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != Guid.Empty && identity.assetId != newAssetId)
            {
                Debug.LogError($"Could not register '{prefab.name}' to {newAssetId} because it already had an AssetId, Existing assetId {identity.assetId}");
                return;
            }

            identity.assetId = newAssetId;

            RegisterPrefabIdentity(identity);
        }

        /// <summary>Register spawnable prefab.</summary>
        public static void RegisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not register prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            RegisterPrefabIdentity(identity);
        }

        /// <summary>Register a spawnable prefab with custom assetId and custom spawn/unspawn handlers.</summary>
        // Note: newAssetId can not be set on GameObjects that already have an assetId
        // Note: registering with assetId is useful for assetbundles etc. a lot
        //       of people use this.
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {newAssetId}");
                return;
            }

            RegisterPrefab(prefab, newAssetId, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>Register a spawnable prefab with custom spawn/unspawn handlers.</summary>
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            Guid assetId = identity.assetId;

            if (assetId == Guid.Empty)
            {
                Debug.LogError($"Can not Register handler for '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            RegisterPrefab(prefab, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>Register a spawnable prefab with custom assetId and custom spawn/unspawn handlers.</summary>
        // Note: newAssetId can not be set on GameObjects that already have an assetId
        // Note: registering with assetId is useful for assetbundles etc. a lot
        //       of people use this.
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (newAssetId == Guid.Empty)
            {
                Debug.LogError($"Could not register handler for '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            if (prefab == null)
            {
                Debug.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != Guid.Empty && identity.assetId != newAssetId)
            {
                Debug.LogError($"Could not register Handler for '{prefab.name}' to {newAssetId} because it already had an AssetId, Existing assetId {identity.assetId}");
                return;
            }

            if (identity.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            identity.assetId = newAssetId;
            Guid assetId = identity.assetId;

            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                Debug.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                Debug.LogWarning($"Replacing existing spawnHandlers for prefab '{prefab.name}' with assetId '{assetId}'");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                Debug.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}', unregister the prefab first before trying to add handler");
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                Debug.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            // Debug.Log("Registering custom prefab '" + prefab.name + "' as asset:" + assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>Register a spawnable prefab with custom spawn/unspawn handlers.</summary>
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            Guid assetId = identity.assetId;

            if (assetId == Guid.Empty)
            {
                Debug.LogError($"Can not Register handler for '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                Debug.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                Debug.LogWarning($"Replacing existing spawnHandlers for prefab '{prefab.name}' with assetId '{assetId}'");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                Debug.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}', unregister the prefab first before trying to add handler");
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                Debug.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            // Debug.Log("Registering custom prefab '" + prefab.name + "' as asset:" + assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.</summary>
        public static void UnregisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not unregister prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            Guid assetId = identity.assetId;

            prefabs.Remove(assetId);
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        // spawn handlers //////////////////////////////////////////////////////
        /// <summary>This is an advanced spawning function that registers a custom assetId with the spawning system.</summary>
        // This can be used to register custom spawning methods for an assetId -
        // instead of the usual method of registering spawning methods for a
        // prefab. This should be used when no prefab exists for the spawned
        // objects - such as when they are constructed dynamically at runtime
        // from configuration data.
        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            RegisterSpawnHandler(assetId, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>This is an advanced spawning function that registers a custom assetId with the spawning system.</summary>
        // This can be used to register custom spawning methods for an assetId -
        // instead of the usual method of registering spawning methods for a
        // prefab. This should be used when no prefab exists for the spawned
        // objects - such as when they are constructed dynamically at runtime
        // from configuration data.
        public static void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                Debug.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (assetId == Guid.Empty)
            {
                Debug.LogError("Can not Register SpawnHandler for empty Guid");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                Debug.LogWarning($"Replacing existing spawnHandlers for {assetId}");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                Debug.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}'");
            }

            // Debug.Log("RegisterSpawnHandler asset '" + assetId + "' " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary> Removes a registered spawn handler function that was registered with ClientScene.RegisterHandler().</summary>
        public static void UnregisterSpawnHandler(Guid assetId)
        {
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        /// <summary>This clears the registered spawn prefabs and spawn handler functions for this client.</summary>
        public static void ClearSpawners()
        {
            prefabs.Clear();
            spawnHandlers.Clear();
            unspawnHandlers.Clear();
        }

        internal static bool InvokeUnSpawnHandler(Guid assetId, GameObject obj)
        {
            if (unspawnHandlers.TryGetValue(assetId, out UnSpawnDelegate handler) && handler != null)
            {
                handler(obj);
                return true;
            }
            return false;
        }

        // host mode callbacks /////////////////////////////////////////////////
        static void OnHostClientObjectDestroy(ObjectDestroyMessage msg)
        {
            // Debug.Log("ClientScene.OnLocalObjectObjDestroy netId:" + msg.netId);
            NetworkIdentity.spawned.Remove(msg.netId);
        }

        static void OnHostClientObjectHide(ObjectHideMessage msg)
        {
            // Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) &&
                localObject != null)
            {
                localObject.OnSetHostVisibility(false);
            }
        }

        internal static void OnHostClientSpawn(SpawnMessage msg)
        {
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) &&
                localObject != null)
            {
                if (msg.isLocalPlayer)
                    ClientScene.InternalAddPlayer(localObject);

                localObject.hasAuthority = msg.isOwner;
                localObject.NotifyAuthority();
                localObject.OnStartClient();
                localObject.OnSetHostVisibility(true);
                CheckForLocalPlayer(localObject);
            }
        }

        // callbacks ///////////////////////////////////////////////////////////
        static void OnUpdateVarsMessage(UpdateVarsMessage msg)
        {
            // Debug.Log("ClientScene.OnUpdateVarsMessage " + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else Debug.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
        }

        static void OnRPCMessage(RpcMessage msg)
        {
            // Debug.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, MirrorInvokeType.ClientRpc, networkReader);
            }
        }

        static void OnObjectHide(ObjectHideMessage msg) => DestroyObject(msg.netId);

        internal static void OnObjectDestroy(ObjectDestroyMessage msg) => DestroyObject(msg.netId);

        internal static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity == ClientScene.localPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger
                // OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = ClientScene.readyConnection;
                identity.OnStartLocalPlayer();
                // Debug.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
            }
        }

        // destroy /////////////////////////////////////////////////////////////
        static void DestroyObject(uint netId)
        {
            // Debug.Log("ClientScene.OnObjDestroy netId:" + netId);
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnStopClient();

                // user handling
                if (InvokeUnSpawnHandler(localObject.assetId, localObject.gameObject))
                {
                    // reset object after user's handler
                    localObject.Reset();
                }
                // default handling
                else if (localObject.sceneId == 0)
                {
                    // don't call reset before destroy so that values are still set in OnDestroy
                    GameObject.Destroy(localObject.gameObject);
                }
                // scene object.. disable it in scene instead of destroying
                else
                {
                    localObject.gameObject.SetActive(false);
                    ClientScene.spawnableObjects[localObject.sceneId] = localObject;
                    // reset for scene objects
                    localObject.Reset();
                }

                // remove from dictionary no matter how it is unspawned
                NetworkIdentity.spawned.Remove(netId);
            }
            //else Debug.LogWarning("Did not find target for destroy message for " + netId);
        }

        // update //////////////////////////////////////////////////////////////
        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate()
        {
            // process all incoming messages first before updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientEarlyUpdate();
        }

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkLateUpdate()
        {
            // local connection?
            if (connection is LocalConnectionToServer localConnection)
            {
                localConnection.Update();
            }
            // remote connection?
            else
            {
                // only update things while connected
                if (active && connectState == ConnectState.Connected)
                {
                    NetworkTime.UpdateClient();
                }
            }

            // process all incoming messages after updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientLateUpdate();
        }

        // obsolete to not break people's projects. Update was public.
        [Obsolete("NetworkClient.Update is now called internally from our custom update loop. No need to call Update manually anymore.")]
        public static void Update() => NetworkLateUpdate();

        // shutdown ////////////////////////////////////////////////////////////
        /// <summary>Shutdown the client.</summary>
        public static void Shutdown()
        {
            Debug.Log("Shutting down client.");
            ClientScene.Shutdown();
            connectState = ConnectState.None;
            handlers.Clear();
            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
