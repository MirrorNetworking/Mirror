using System;
using System.Collections.Generic;
using System.Linq;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    public enum ConnectState
    {
        None,
        // connecting between Connect() and OnTransportConnected()
        Connecting,
        Connected,
        // disconnecting between Disconnect() and OnTransportDisconnected()
        Disconnecting,
        Disconnected
    }

    /// <summary>NetworkClient with connection to server.</summary>
    public static class NetworkClient
    {
        // message handlers by messageId
        internal static readonly Dictionary<ushort, NetworkMessageDelegate> handlers =
            new Dictionary<ushort, NetworkMessageDelegate>();

        /// <summary>All spawned NetworkIdentities by netId.</summary>
        // client sees OBSERVED spawned ones.
        public static readonly Dictionary<uint, NetworkIdentity> spawned =
            new Dictionary<uint, NetworkIdentity>();

        /// <summary>Client's NetworkConnection to server.</summary>
        public static NetworkConnection connection { get; internal set; }

        /// <summary>True if client is ready (= joined world).</summary>
        // TODO redundant state. point it to .connection.isReady instead (& test)
        // TODO OR remove NetworkConnection.isReady? unless it's used on server
        //
        // TODO maybe ClientState.Connected/Ready/AddedPlayer/etc.?
        //      way better for security if we can check states in callbacks
        public static bool ready;

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

        /// <summary>True if client is running in host mode.</summary>
        public static bool isHostClient => connection is LocalConnectionToServer;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action OnConnectedEvent;
        public static Action OnDisconnectedEvent;
        public static Action<Exception> OnErrorEvent;

        /// <summary>Registered spawnable prefabs by assetId.</summary>
        public static readonly Dictionary<Guid, GameObject> prefabs =
            new Dictionary<Guid, GameObject>();

        // spawn handlers
        internal static readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers =
            new Dictionary<Guid, SpawnHandlerDelegate>();
        internal static readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers =
            new Dictionary<Guid, UnSpawnDelegate>();

        // spawning
        // internal for tests
        internal static bool isSpawnFinished;

        // Disabled scene objects that can be spawned again, by sceneId.
        internal static readonly Dictionary<ulong, NetworkIdentity> spawnableObjects =
            new Dictionary<ulong, NetworkIdentity>();

        static Unbatcher unbatcher = new Unbatcher();

        // interest management component (optional)
        // only needed for SetHostVisibility
        public static InterestManagement aoi;

        // scene loading
        public static bool isLoadingScene;

        // initialization //////////////////////////////////////////////////////
        static void AddTransportHandlers()
        {
            Transport.activeTransport.OnClientConnected = OnTransportConnected;
            Transport.activeTransport.OnClientDataReceived = OnTransportData;
            Transport.activeTransport.OnClientDisconnected = OnTransportDisconnected;
            Transport.activeTransport.OnClientError = OnError;
        }

        internal static void RegisterSystemHandlers(bool hostMode)
        {
            // host mode client / remote client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (hostMode)
            {
                RegisterHandler<ObjectDestroyMessage>(OnHostClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnHostClientObjectHide);
                RegisterHandler<NetworkPongMessage>(_ => {}, false);
                RegisterHandler<SpawnMessage>(OnHostClientSpawn);
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnStartedMessage>(_ => {});
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>(_ => {});
                // host mode doesn't need state updates
                RegisterHandler<EntityStateMessage>(_ => {});
            }
            else
            {
                RegisterHandler<ObjectDestroyMessage>(OnObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnObjectHide);
                RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong, false);
                RegisterHandler<SpawnMessage>(OnSpawn);
                RegisterHandler<ObjectSpawnStartedMessage>(OnObjectSpawnStarted);
                RegisterHandler<ObjectSpawnFinishedMessage>(OnObjectSpawnFinished);
                RegisterHandler<EntityStateMessage>(OnEntityStateMessage);
            }

            // These handlers are the same for host and remote clients
            RegisterHandler<ChangeOwnerMessage>(OnChangeOwner);
            RegisterHandler<RpcMessage>(OnRPCMessage);
        }

        // connect /////////////////////////////////////////////////////////////
        /// <summary>Connect client to a NetworkServer by address.</summary>
        public static void Connect(string address)
        {
            // Debug.Log($"Client Connect: {address}");
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(address);

            connection = new NetworkConnectionToServer();
        }

        /// <summary>Connect client to a NetworkServer by Uri.</summary>
        public static void Connect(Uri uri)
        {
            // Debug.Log($"Client Connect: {uri}");
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(uri);

            connection = new NetworkConnectionToServer();
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
            // only if connected or connecting.
            // don't disconnect() again if already in the process of
            // disconnecting or fully disconnected.
            if (connectState != ConnectState.Connecting &&
                connectState != ConnectState.Connected)
                return;

            // we are disconnecting until OnTransportDisconnected is called.
            // setting state to Disconnected would stop OnTransportDisconnected
            // from calling cleanup code because it would think we are already
            // disconnected fully.
            // TODO move to 'cleanup' code below if safe
            connectState = ConnectState.Disconnecting;
            ready = false;

            // call Disconnect on the NetworkConnection
            connection?.Disconnect();

            // IMPORTANT: do NOT clear connection here yet.
            // we still need it in OnTransportDisconnected for callbacks.
            // connection = null;
        }

        // transport events ////////////////////////////////////////////////////
        // called by Transport
        static void OnTransportConnected()
        {
            if (connection != null)
            {
                // reset network time stats
                NetworkTime.ResetStatics();

                // reset unbatcher in case any batches from last session remain.
                unbatcher = new Unbatcher();

                // the handler may want to send messages to the client
                // thus we should set the connected state before calling the handler
                connectState = ConnectState.Connected;
                NetworkTime.UpdateClient();
                OnConnectedEvent?.Invoke();
            }
            else Debug.LogError("Skipped Connect message handling because connection is null.");
        }

        // helper function
        static bool UnpackAndInvoke(NetworkReader reader, int channelId)
        {
            if (MessagePacking.Unpack(reader, out ushort msgType))
            {
                // try to invoke the handler for that message
                if (handlers.TryGetValue(msgType, out NetworkMessageDelegate handler))
                {
                    handler.Invoke(connection, reader, channelId);

                    // message handler may disconnect client, making connection = null
                    // therefore must check for null to avoid NRE.
                    if (connection != null)
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
                    Debug.LogWarning($"Unknown message id: {msgType}. This can happen if no handler was registered for this message.");
                    // simply return false. caller is responsible for disconnecting.
                    //connection.Disconnect();
                    return false;
                }
            }
            else
            {
                // => WARNING, not error. can happen if attacker sends random data.
                Debug.LogWarning("Invalid message header.");
                // simply return false. caller is responsible for disconnecting.
                //connection.Disconnect();
                return false;
            }
        }

        // called by Transport
        internal static void OnTransportData(ArraySegment<byte> data, int channelId)
        {
            if (connection != null)
            {
                // server might batch multiple messages into one packet.
                // feed it to the Unbatcher.
                // NOTE: we don't need to associate a channelId because we
                //       always process all messages in the batch.
                if (!unbatcher.AddBatch(data))
                {
                    Debug.LogWarning($"NetworkClient: failed to add batch, disconnecting.");
                    connection.Disconnect();
                    return;
                }

                // process all messages in the batch.
                // only while NOT loading a scene.
                // if we get a scene change message, then we need to stop
                // processing. otherwise we might apply them to the old scene.
                // => fixes https://github.com/vis2k/Mirror/issues/2651
                //
                // NOTE: is scene starts loading, then the rest of the batch
                //       would only be processed when OnTransportData is called
                //       the next time.
                //       => consider moving processing to NetworkEarlyUpdate.
                while (!isLoadingScene &&
                       unbatcher.GetNextMessage(out NetworkReader reader, out double remoteTimestamp))
                {
                    // enough to read at least header size?
                    if (reader.Remaining >= MessagePacking.HeaderSize)
                    {
                        // make remoteTimeStamp available to the user
                        connection.remoteTimeStamp = remoteTimestamp;

                        // handle message
                        if (!UnpackAndInvoke(reader, channelId))
                        {
                            // warn, disconnect and return if failed
                            // -> warning because attackers might send random data
                            // -> messages in a batch aren't length prefixed.
                            //    failing to read one would cause undefined
                            //    behaviour for every message afterwards.
                            //    so we need to disconnect.
                            // -> return to avoid the below unbatches.count error.
                            //    we already disconnected and handled it.
                            Debug.LogWarning($"NetworkClient: failed to unpack and invoke message. Disconnecting.");
                            connection.Disconnect();
                            return;
                        }
                    }
                    // otherwise disconnect
                    else
                    {
                        // WARNING, not error. can happen if attacker sends random data.
                        Debug.LogWarning($"NetworkClient: received Message was too short (messages should start with message id)");
                        connection.Disconnect();
                        return;
                    }
                }

                // if we weren't interrupted by a scene change,
                // then all batched messages should have been processed now.
                // if not, we need to log an error to avoid debugging hell.
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
                if (!isLoadingScene && unbatcher.BatchesCount > 0)
                {
                    Debug.LogError($"Still had {unbatcher.BatchesCount} batches remaining after processing, even though processing was not interrupted by a scene change. This should never happen, as it would cause ever growing batches.\nPossible reasons:\n* A message didn't deserialize as much as it serialized\n*There was no message handler for a message id, so the reader wasn't read until the end.");
                }
            }
            else Debug.LogError("Skipped Data message handling because connection is null.");
        }

        // called by Transport
        // IMPORTANT: often times when disconnecting, we call this from Mirror
        //            too because we want to remove the connection and handle
        //            the disconnect immediately.
        //            => which is fine as long as we guarantee it only runs once
        //            => which we do by setting the state to Disconnected!
        internal static void OnTransportDisconnected()
        {
            // StopClient called from user code triggers Disconnected event
            // from transport which calls StopClient again, so check here
            // and short circuit running the Shutdown process twice.
            if (connectState == ConnectState.Disconnected) return;

            // Raise the event before changing ConnectState
            // because 'active' depends on this during shutdown
            if (connection != null) OnDisconnectedEvent?.Invoke();

            connectState = ConnectState.Disconnected;
            ready = false;

            // now that everything was handled, clear the connection.
            // previously this was done in Disconnect() already, but we still
            // need it for the above OnDisconnectedEvent.
            connection = null;
        }

        static void OnError(Exception exception)
        {
            Debug.LogException(exception);
            OnErrorEvent?.Invoke(exception);
        }

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
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            // we use the same WrapHandler function for server and client.
            // so let's wrap it to ignore the NetworkConnection parameter.
            // it's not needed on client. it's always NetworkClient.connection.
            void HandlerWrapped(NetworkConnection _, T value) => handler(value);
            handlers[msgType] = MessagePacking.WrapHandler((Action<NetworkConnection, T>) HandlerWrapped, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // RegisterHandler throws a warning (as it should) if a handler is assigned twice
        // Use of ReplaceHandler makes it clear the user intended to replace the handler
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // RegisterHandler throws a warning (as it should) if a handler is assigned twice
        // Use of ReplaceHandler makes it clear the user intended to replace the handler
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
            ushort msgType = MessagePacking.GetId<T>();
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
                Debug.LogError($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
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
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
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
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
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
                Debug.LogError($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            //Debug.Log($"Registering custom prefab {prefab.name} as asset:{assetId} {spawnHandler.GetMethodName()}/{unspawnHandler.GetMethodName()}");

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
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
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
                Debug.LogError($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            //Debug.Log($"Registering custom prefab {prefab.name} as asset:{assetId} {spawnHandler.GetMethodName()}/{unspawnHandler.GetMethodName()}");

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
                Debug.LogError($"Could not unregister '{prefab.name}' since it contains no NetworkIdentity component");
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

            // Debug.Log("RegisterSpawnHandler asset {assetId} {spawnHandler.GetMethodName()}/{unspawnHandler.GetMethodName()}");

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
            // Debug.Log($"NetworkClient.Ready() called with connection {conn}");
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

            // Debug.Log($"NetworkClient.AddPlayer() called with connection {readyConnection}");
            connection.Send(new AddPlayerMessage());
            return true;
        }

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

            spawned[message.netId] = identity;

            // the initial spawn with OnObjectSpawnStarted/Finished calls all
            // object's OnStartClient/OnStartLocalPlayer after they were all
            // spawned.
            // this only happens once though.
            // for all future spawns, we need to call OnStartClient/LocalPlayer
            // here immediately since there won't be another OnObjectSpawnFinished.
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

            identity = message.sceneId == 0 ? SpawnPrefab(message) : SpawnSceneObject(message.sceneId);

            if (identity == null)
            {
                Debug.LogError($"Could not spawn assetId={message.assetId} scene={message.sceneId:X} netId={message.netId}");
                return false;
            }

            return true;
        }

        static NetworkIdentity GetExistingObject(uint netid)
        {
            spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        static NetworkIdentity SpawnPrefab(SpawnMessage message)
        {
            if (GetPrefab(message.assetId, out GameObject prefab))
            {
                GameObject obj = GameObject.Instantiate(prefab, message.position, message.rotation);
                //Debug.Log($"Client spawn handler instantiating [netId{message.netId} asset ID:{message.assetId} pos:{message.position} rotation:{message.rotation}]");
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

        static NetworkIdentity SpawnSceneObject(ulong sceneId)
        {
            NetworkIdentity identity = GetAndRemoveSceneObject(sceneId);
            if (identity == null)
            {
                Debug.LogError($"Spawn scene object not found for {sceneId:X}. Make sure that client and server use exactly the same project. This only happens if the hierarchy gets out of sync.");

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
            foreach (NetworkIdentity identity in spawned.Values.OrderBy(uv => uv.netId))
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
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in spawned)
            {
                if (kvp.Value == null)
                {
                    removeFromSpawned.Add(kvp.Key);
                }
            }

            // can't modify NetworkIdentity.spawned inside foreach so need 2nd loop to remove
            foreach (uint id in removeFromSpawned)
            {
                spawned.Remove(id);
            }
            removeFromSpawned.Clear();
        }

        // host mode callbacks /////////////////////////////////////////////////
        static void OnHostClientObjectDestroy(ObjectDestroyMessage message)
        {
            //Debug.Log($"NetworkClient.OnLocalObjectObjDestroy netId:{message.netId}");
            spawned.Remove(message.netId);
        }

        static void OnHostClientObjectHide(ObjectHideMessage message)
        {
            //Debug.Log($"ClientScene::OnLocalObjectObjHide netId:{message.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity localObject) &&
                localObject != null)
            {
                if (aoi != null)
                    aoi.SetHostVisibility(localObject, false);
            }
        }

        internal static void OnHostClientSpawn(SpawnMessage message)
        {
            // on host mode, the object already exist in NetworkServer.spawned.
            // simply add it to NetworkClient.spawned too.
            if (NetworkServer.spawned.TryGetValue(message.netId, out NetworkIdentity localObject) && localObject != null)
            {
                spawned[message.netId] = localObject;

                // now do the actual 'spawning' on host mode
                if (message.isLocalPlayer)
                    InternalAddPlayer(localObject);

                localObject.hasAuthority = message.isOwner;
                localObject.NotifyAuthority();
                localObject.OnStartClient();

                if (aoi != null)
                    aoi.SetHostVisibility(localObject, true);

                CheckForLocalPlayer(localObject);
            }
        }

        // client-only mode callbacks //////////////////////////////////////////
        static void OnEntityStateMessage(EntityStateMessage message)
        {
            // Debug.Log($"NetworkClient.OnUpdateVarsMessage {msg.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(message.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else Debug.LogWarning($"Did not find target for sync message for {message.netId} . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
        }

        static void OnRPCMessage(RpcMessage message)
        {
            // Debug.Log($"NetworkClient.OnRPCMessage hash:{msg.functionHash} netId:{msg.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(message.payload))
                    identity.HandleRemoteCall(message.componentIndex, message.functionHash, RemoteCallType.ClientRpc, networkReader);
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

        internal static void OnChangeOwner(ChangeOwnerMessage message)
        {
            NetworkIdentity identity = GetExistingObject(message.netId);

            if (identity != null)
                ChangeOwner(identity, message);
            else
                Debug.LogError($"OnChangeOwner: Could not find object with netId {message.netId}");
        }

        internal static void ChangeOwner(NetworkIdentity identity, ChangeOwnerMessage message)
        {
            identity.hasAuthority = message.isOwner;
            identity.NotifyAuthority();

            identity.isLocalPlayer = message.isLocalPlayer;
            if (identity.isLocalPlayer)
                localPlayer = identity;
            else if (localPlayer == identity)
            {
                // localPlayer may already be assigned to something else
                // so only make it null if it's this identity.
                localPlayer = null;
                identity.OnStopLocalPlayer();
            }

            CheckForLocalPlayer(identity);
        }

        internal static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity == localPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger
                // OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = connection;
                identity.OnStartLocalPlayer();
                // Debug.Log($"NetworkClient.OnOwnerMessage player:{identity.name}");
            }
        }

        // destroy /////////////////////////////////////////////////////////////
        static void DestroyObject(uint netId)
        {
            // Debug.Log($"NetworkClient.OnObjDestroy netId: {netId}");
            if (spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                if (localObject.isLocalPlayer)
                    localObject.OnStopLocalPlayer();

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
                spawned.Remove(netId);
            }
            //else Debug.LogWarning($"Did not find target for destroy message for {netId}");
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
            else if (connection is NetworkConnectionToServer remoteConnection)
            {
                // only update things while connected
                if (active && connectState == ConnectState.Connected)
                {
                    // update NetworkTime
                    NetworkTime.UpdateClient();

                    // update connection to flush out batched messages
                    remoteConnection.Update();
                }
            }

            // process all outgoing messages after updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientLateUpdate();
        }

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
                foreach (NetworkIdentity identity in spawned.Values)
                {
                    if (identity != null && identity.gameObject != null)
                    {
                        if (identity.isLocalPlayer)
                            identity.OnStopLocalPlayer();

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
                spawned.Clear();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogException(e);
                Debug.LogError("Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            }
        }

        /// <summary>Shutdown the client.</summary>
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Shutdown()
        {
            //Debug.Log("Shutting down client.");

            // calls prefabs.Clear();
            // calls spawnHandlers.Clear();
            // calls unspawnHandlers.Clear();
            ClearSpawners();

            // calls spawned.Clear() if no exception occurs
            DestroyAllClientObjects();

            spawned.Clear();
            handlers.Clear();
            spawnableObjects.Clear();

            // sets nextNetworkId to 1
            // sets clientAuthorityCallback to null
            // sets previousLocalPlayer to null
            NetworkIdentity.ResetStatics();

            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientDisconnect();

            // reset statics
            connectState = ConnectState.None;
            connection = null;
            localPlayer = null;
            ready = false;
            isSpawnFinished = false;
            isLoadingScene = false;

            unbatcher = new Unbatcher();

            // clear events. someone might have hooked into them before, but
            // we don't want to use those hooks after Shutdown anymore.
            OnConnectedEvent = null;
            OnDisconnectedEvent = null;
            OnErrorEvent = null;
        }
    }
}
