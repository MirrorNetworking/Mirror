using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>True if client is ready (= joined world).</summary>
        // TODO redundant state. point it to .connection.isReady instead (& test)
        // TODO OR remove NetworkConnection.isReady? unless it's used on server
        //
        // TODO maybe ClientState.Connected/Ready/AddedPlayer/etc.?
        //      way better for security if we can check states in callbacks
        public static bool ready;

        /// <summary>The NetworkConnection object that is currently "ready".</summary>
        // TODO this is from UNET. it's redundant and we should probably obsolete it.
        [Obsolete("NetworkClient.readyConnection is redundant. Use NetworkClient.connection and use NetworkClient.ready to check if it's ready.")]
        public static NetworkConnection readyConnection => ready ? connection : null;

        /// <summary>NetworkIdentity of the localPlayer </summary>
        public static NetworkIdentity localPlayer { get; internal set; }

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
        internal static Action OnConnectedEvent;
        internal static Action OnDisconnectedEvent;

        /// <summary>Registered spawnable prefabs by assetId.</summary>
        public static readonly Dictionary<Guid, GameObject> prefabs =
            new Dictionary<Guid, GameObject>();

        // spawn handlers
        internal static readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers =
            new Dictionary<Guid, SpawnHandlerDelegate>();
        internal static readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers =
            new Dictionary<Guid, UnSpawnDelegate>();

        // spawning
        static bool isSpawnFinished;

        // Disabled scene objects that can be spawned again, by sceneId.
        internal static readonly Dictionary<ulong, NetworkIdentity> spawnableObjects =
            new Dictionary<ulong, NetworkIdentity>();

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
                RegisterHandler<NetworkPongMessage>(msg => {}, false);
                RegisterHandler<SpawnMessage>(OnHostClientSpawn);
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnStartedMessage>(msg => {});
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>(msg => {});
                // host mode doesn't need state updates
                RegisterHandler<UpdateVarsMessage>(msg => {});
            }
            else
            {
                RegisterHandler<ObjectDestroyMessage>(OnObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnObjectHide);
                RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong, false);
                RegisterHandler<SpawnMessage>(OnSpawn);
                RegisterHandler<ObjectSpawnStartedMessage>(OnObjectSpawnStarted);
                RegisterHandler<ObjectSpawnFinishedMessage>(OnObjectSpawnFinished);
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

        // TODO why are there two connect host methods?
        // called from NetworkManager.FinishStartHost()
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
        // called from NetworkManager.StartHostClient
        // TODO why are there two connect host methods?
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
            ready = false;

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
                OnConnectedEvent?.Invoke();
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
            // StopClient called from user code triggers Disconnected event
            // from transport which calls StopClient again, so check here
            // and short circuit running the Shutdown process twice.
            if (connectState == ConnectState.Disconnected) return;

            connectState = ConnectState.Disconnected;
            ready = false;

            if (connection != null) OnDisconnectedEvent?.Invoke();
        }

        static void OnError(Exception exception) => Debug.LogException(exception);

        // send ////////////////////////////////////////////////////////////////
        /// <summary>Send a NetworkMessage to the server over the given channel.</summary>
        public static void Send<T>(T message, int channelId = Channels.Reliable)
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
        [Obsolete("Use RegisterHandler<T> version without NetworkConnection parameter. It always points to NetworkClient.connection anyway.")]
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
            int msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            // we use the same WrapHandler function for server and client.
            // so let's wrap it to ignore the NetworkConnection parameter.
            // it's not needed on client. it's always NetworkClient.connection.
            Action<NetworkConnection, T> handlerWrapped = (_, value) => { handler(value); };
            handlers[msgType] = MessagePacking.WrapHandler(handlerWrapped, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // TODO does anyone even use that? consider removing
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // TODO does anyone even use that? consider removing
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

        /// <summary>Removes a registered spawn prefab that was setup with NetworkClient.RegisterPrefab.</summary>
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

        /// <summary> Removes a registered spawn handler function that was registered with NetworkClient.RegisterHandler().</summary>
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

        // ready ///////////////////////////////////////////////////////////////
        /// <summary>Sends Ready message to server, indicating that we loaded the scene, ready to enter the game.</summary>
        // This could be for example when a client enters an ongoing game and
        // has finished loading the current scene. The server should respond to
        // the SYSTEM_READY event with an appropriate handler which instantiates
        // the players object for example.
        public static bool Ready()
        {
            // Debug.Log("NetworkClient.Ready() called with connection [" + conn + "]");
            if (ready)
            {
                Debug.LogError("NetworkClient is already ready. It shouldn't be called twice.");
                return false;
            }

            // need a valid connection to become ready
            if (connection == null)
            {
                Debug.LogError("Ready() called with invalid connection object: conn=null");
                return false;
            }

            // Set these before sending the ReadyMessage, otherwise host client
            // will fail in InternalAddPlayer with null readyConnection.
            // TODO this is redundant. have one source of truth for .ready
            ready = true;
            connection.isReady = true;

            // Tell server we're ready to have a player object spawned
            connection.Send(new ReadyMessage());
            return true;
        }

        [Obsolete("NetworkClient.Ready doesn't need a NetworkConnection parameter anymore. It always uses NetworkClient.connection anyway.")]
        public static bool Ready(NetworkConnection conn) => Ready();

        // add player //////////////////////////////////////////////////////////
        // called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity identity)
        {
            //Debug.Log("NetworkClient.InternalAddPlayer");

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            localPlayer = identity;

            // NOTE: we DONT need to set isClient=true here, because OnStartClient
            // is called before OnStartLocalPlayer, hence it's already set.
            // localPlayer.isClient = true;

            // TODO this check might not be necessary
            //if (readyConnection != null)
            if (ready && connection != null)
            {
                connection.identity = identity;
            }
            else Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
        }

        /// <summary>Sends AddPlayer message to the server, indicating that we want to join the world.</summary>
        public static bool AddPlayer()
        {
            // ensure valid ready connection
            if (connection == null)
            {
                Debug.LogError("AddPlayer requires a valid NetworkClient.connection.");
                return false;
            }

            // UNET checked 'if readyConnection != null'.
            // in other words, we need a connection and we need to be ready.
            if (!ready)
            {
                Debug.LogError("AddPlayer requires a ready NetworkClient.");
                return false;
            }

            if (connection.identity != null)
            {
                Debug.LogError("NetworkClient.AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
                return false;
            }

            // Debug.Log("NetworkClient.AddPlayer() called with connection [" + readyConnection + "]");
            connection.Send(new AddPlayerMessage());
            return true;
        }

        [Obsolete("NetworkClient.AddPlayer doesn't need a NetworkConnection parameter anymore. It always uses NetworkClient.connection anyway.")]
        public static bool AddPlayer(NetworkConnection readyConn) => AddPlayer();

        // spawning ////////////////////////////////////////////////////////////
        internal static void ApplySpawnPayload(NetworkIdentity identity, SpawnMessage message)
        {
            if (message.assetId != Guid.Empty)
                identity.assetId = message.assetId;

            if (!identity.gameObject.activeSelf)
            {
                identity.gameObject.SetActive(true);
            }

            // apply local values for VR support
            identity.transform.localPosition = message.position;
            identity.transform.localRotation = message.rotation;
            identity.transform.localScale = message.scale;
            identity.hasAuthority = message.isOwner;
            identity.netId = message.netId;

            if (message.isLocalPlayer)
                InternalAddPlayer(identity);

            // deserialize components if any payload
            // (Count is 0 if there were no components)
            if (message.payload.Count > 0)
            {
                using (PooledNetworkReader payloadReader = NetworkReaderPool.GetReader(message.payload))
                {
                    identity.OnDeserializeAllSafely(payloadReader, true);
                }
            }

            NetworkIdentity.spawned[message.netId] = identity;

            // objects spawned as part of initial state are started on a second pass
            if (isSpawnFinished)
            {
                identity.NotifyAuthority();
                identity.OnStartClient();
                CheckForLocalPlayer(identity);
            }
        }

        // Finds Existing Object with NetId or spawns a new one using AssetId or sceneId
        internal static bool FindOrSpawnObject(SpawnMessage message, out NetworkIdentity identity)
        {
            // was the object already spawned?
            identity = GetExistingObject(message.netId);

            // if found, return early
            if (identity != null)
            {
                return true;
            }

            if (message.assetId == Guid.Empty && message.sceneId == 0)
            {
                Debug.LogError($"OnSpawn message with netId '{message.netId}' has no AssetId or sceneId");
                return false;
            }

            identity = message.sceneId == 0 ? SpawnPrefab(message) : SpawnSceneObject(message);

            if (identity == null)
            {
                Debug.LogError($"Could not spawn assetId={message.assetId} scene={message.sceneId:X} netId={message.netId}");
                return false;
            }

            return true;
        }

        static NetworkIdentity GetExistingObject(uint netid)
        {
            NetworkIdentity.spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        static NetworkIdentity SpawnPrefab(SpawnMessage message)
        {
            if (GetPrefab(message.assetId, out GameObject prefab))
            {
                GameObject obj = GameObject.Instantiate(prefab, message.position, message.rotation);
                //Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                return obj.GetComponent<NetworkIdentity>();
            }
            if (spawnHandlers.TryGetValue(message.assetId, out SpawnHandlerDelegate handler))
            {
                GameObject obj = handler(message);
                if (obj == null)
                {
                    Debug.LogError($"Spawn Handler returned null, Handler assetId '{message.assetId}'");
                    return null;
                }
                NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    Debug.LogError($"Object Spawned by handler did not have a NetworkIdentity, Handler assetId '{message.assetId}'");
                    return null;
                }
                return identity;
            }
            Debug.LogError($"Failed to spawn server object, did you forget to add it to the NetworkManager? assetId={message.assetId} netId={message.netId}");
            return null;
        }

        static NetworkIdentity SpawnSceneObject(SpawnMessage message)
        {
            NetworkIdentity identity = GetAndRemoveSceneObject(message.sceneId);
            if (identity == null)
            {
                Debug.LogError($"Spawn scene object not found for {message.sceneId:X} SpawnableObjects.Count={spawnableObjects.Count}");

                // dump the whole spawnable objects dict for easier debugging
                //foreach (KeyValuePair<ulong, NetworkIdentity> kvp in spawnableObjects)
                //    Debug.Log($"Spawnable: SceneId={kvp.Key:X} name={kvp.Value.name}");
            }
            //else Debug.Log($"Client spawn for [netId:{msg.netId}] [sceneId:{msg.sceneId:X}] obj:{identity}");
            return identity;
        }

        static NetworkIdentity GetAndRemoveSceneObject(ulong sceneId)
        {
            if (spawnableObjects.TryGetValue(sceneId, out NetworkIdentity identity))
            {
                spawnableObjects.Remove(sceneId);
                return identity;
            }
            return null;
        }

        // Checks if identity is not spawned yet, not hidden and has sceneId
        static bool ConsiderForSpawning(NetworkIdentity identity)
        {
            // not spawned yet, not hidden, etc.?
            return !identity.gameObject.activeSelf &&
                   identity.gameObject.hideFlags != HideFlags.NotEditable &&
                   identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                   identity.sceneId != 0;
        }

        /// <summary>Call this after loading/unloading a scene in the client after connection to register the spawnable objects</summary>
        public static void PrepareToSpawnSceneObjects()
        {
            // remove existing items, they will be re-added below
            spawnableObjects.Clear();

            // finds all NetworkIdentity currently loaded by unity (includes disabled objects)
            NetworkIdentity[] allIdentities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            foreach (NetworkIdentity identity in allIdentities)
            {
                // add all unspawned NetworkIdentities to spawnable objects
                if (ConsiderForSpawning(identity))
                {
                    spawnableObjects.Add(identity.sceneId, identity);
                }
            }
        }

        internal static void OnObjectSpawnStarted(ObjectSpawnStartedMessage _)
        {
            // Debug.Log("SpawnStarted");
            PrepareToSpawnSceneObjects();
            isSpawnFinished = false;
        }

        internal static void OnObjectSpawnFinished(ObjectSpawnFinishedMessage _)
        {
            //Debug.Log("SpawnFinished");
            ClearNullFromSpawned();

            // paul: Initialize the objects in the same order as they were
            // initialized in the server. This is important if spawned objects
            // use data from scene objects
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values.OrderBy(uv => uv.netId))
            {
                identity.NotifyAuthority();
                identity.OnStartClient();
                CheckForLocalPlayer(identity);
            }
            isSpawnFinished = true;
        }

        static readonly List<uint> removeFromSpawned = new List<uint>();
        static void ClearNullFromSpawned()
        {
            // spawned has null objects after changing scenes on client using
            // NetworkManager.ServerChangeScene remove them here so that 2nd
            // loop below does not get NullReferenceException
            // see https://github.com/vis2k/Mirror/pull/2240
            // TODO fix scene logic so that client scene doesn't have null objects
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in NetworkIdentity.spawned)
            {
                if (kvp.Value == null)
                {
                    removeFromSpawned.Add(kvp.Key);
                }
            }

            // can't modify NetworkIdentity.spawned inside foreach so need 2nd loop to remove
            foreach (uint id in removeFromSpawned)
            {
                NetworkIdentity.spawned.Remove(id);
            }
            removeFromSpawned.Clear();
        }

        // host mode callbacks /////////////////////////////////////////////////
        static void OnHostClientObjectDestroy(ObjectDestroyMessage message)
        {
            // Debug.Log("NetworkClient.OnLocalObjectObjDestroy netId:" + msg.netId);

            // TODO why do we do this?
            // in host mode, .spawned is shared between server and client.
            // removing it on client would remove it on server.
            // huh.
            NetworkIdentity.spawned.Remove(message.netId);
        }

        static void OnHostClientObjectHide(ObjectHideMessage message)
        {
            // Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(message.netId, out NetworkIdentity localObject) &&
                localObject != null)
            {
                localObject.OnSetHostVisibility(false);
            }
        }

        internal static void OnHostClientSpawn(SpawnMessage message)
        {
            if (NetworkIdentity.spawned.TryGetValue(message.netId, out NetworkIdentity localObject) &&
                localObject != null)
            {
                if (message.isLocalPlayer)
                    InternalAddPlayer(localObject);

                localObject.hasAuthority = message.isOwner;
                localObject.NotifyAuthority();
                localObject.OnStartClient();
                localObject.OnSetHostVisibility(true);
                CheckForLocalPlayer(localObject);
            }
        }

        // client-only mode callbacks //////////////////////////////////////////
        static void OnUpdateVarsMessage(UpdateVarsMessage message)
        {
            // Debug.Log("NetworkClient.OnUpdateVarsMessage " + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(message.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(message.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else Debug.LogWarning("Did not find target for sync message for " + message.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
        }

        static void OnRPCMessage(RpcMessage message)
        {
            // Debug.Log("NetworkClient.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);
            if (NetworkIdentity.spawned.TryGetValue(message.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(message.payload))
                    identity.HandleRemoteCall(message.componentIndex, message.functionHash, MirrorInvokeType.ClientRpc, networkReader);
            }
        }

        static void OnObjectHide(ObjectHideMessage message) => DestroyObject(message.netId);

        internal static void OnObjectDestroy(ObjectDestroyMessage message) => DestroyObject(message.netId);

        internal static void OnSpawn(SpawnMessage message)
        {
            // Debug.Log($"Client spawn handler instantiating netId={msg.netId} assetID={msg.assetId} sceneId={msg.sceneId:X} pos={msg.position}");
            if (FindOrSpawnObject(message, out NetworkIdentity identity))
            {
                ApplySpawnPayload(identity, message);
            }
        }

        internal static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity == localPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger
                // OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = connection;
                identity.OnStartLocalPlayer();
                // Debug.Log("NetworkClient.OnOwnerMessage - player=" + identity.name);
            }
        }

        // destroy /////////////////////////////////////////////////////////////
        static void DestroyObject(uint netId)
        {
            // Debug.Log("NetworkClient.OnObjDestroy netId:" + netId);
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
                    spawnableObjects[localObject.sceneId] = localObject;
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
        /// <summary>Destroys all networked objects on the client.</summary>
        // Note: NetworkServer.CleanupNetworkIdentities does the same on server.
        public static void DestroyAllClientObjects()
        {
            // user can modify spawned lists which causes InvalidOperationException
            // list can modified either in UnSpawnHandler or in OnDisable/OnDestroy
            // we need the Try/Catch so that the rest of the shutdown does not get stopped
            try
            {
                foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
                {
                    if (identity != null && identity.gameObject != null)
                    {
                        identity.OnStopClient();
                        bool wasUnspawned = InvokeUnSpawnHandler(identity.assetId, identity.gameObject);
                        if (!wasUnspawned)
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
                }
                NetworkIdentity.spawned.Clear();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogException(e);
                Debug.LogError("Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            }
        }

        /// <summary>Shutdown the client.</summary>
        public static void Shutdown()
        {
            Debug.Log("Shutting down client.");
            ClearSpawners();
            spawnableObjects.Clear();
            ready = false;
            isSpawnFinished = false;
            DestroyAllClientObjects();
            connectState = ConnectState.None;
            handlers.Clear();
            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientDisconnect();
        }
    }
}
