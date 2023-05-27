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
    public static partial class NetworkClient
    {
        // time & value snapshot interpolation are separate.
        // -> time is interpolated globally on NetworkClient / NetworkConnection
        // -> value is interpolated per-component, i.e. NetworkTransform.
        // however, both need to be on the same send interval.
        //
        // additionally, server & client need to use the same send interval.
        // otherwise it's too easy to accidentally cause interpolation issues if
        // a component sends with client.interval but interpolates with
        // server.interval, etc.
        public static int sendRate => NetworkServer.sendRate;
        public static float sendInterval => sendRate < int.MaxValue ? 1f / sendRate : 0; // for 30 Hz, that's 33ms
        static double lastSendTime;

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

        /// <summary>active is true while a client is connecting/connected either as standalone or as host client.</summary>
        // (= while the network is active)
        public static bool active => connectState == ConnectState.Connecting ||
                                     connectState == ConnectState.Connected;

        /// <summary>active is true while the client is connected in host mode.</summary>
        // naming consistent with NetworkServer.activeHost.
        public static bool activeHost => connection is LocalConnectionToServer;

        /// <summary>Check if client is connecting (before connected).</summary>
        public static bool isConnecting => connectState == ConnectState.Connecting;

        /// <summary>Check if client is connected (after connecting).</summary>
        public static bool isConnected => connectState == ConnectState.Connected;

        // Deprecated 2022-12-12
        [Obsolete("NetworkClient.isHostClient was renamed to .activeHost to be more obvious")]
        public static bool isHostClient => activeHost;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action OnConnectedEvent;
        public static Action OnDisconnectedEvent;
        public static Action<TransportError, string> OnErrorEvent;

        /// <summary>Registered spawnable prefabs by assetId.</summary>
        public static readonly Dictionary<uint, GameObject> prefabs =
            new Dictionary<uint, GameObject>();

        // custom spawn / unspawn handlers by assetId.
        // useful to support prefab pooling etc.:
        // https://mirror-networking.gitbook.io/docs/guides/gameobjects/custom-spawnfunctions
        internal static readonly Dictionary<uint, SpawnHandlerDelegate> spawnHandlers =
            new Dictionary<uint, SpawnHandlerDelegate>();
        internal static readonly Dictionary<uint, UnSpawnDelegate> unspawnHandlers =
            new Dictionary<uint, UnSpawnDelegate>();

        // spawning
        // internal for tests
        internal static bool isSpawnFinished;

        // Disabled scene objects that can be spawned again, by sceneId.
        internal static readonly Dictionary<ulong, NetworkIdentity> spawnableObjects =
            new Dictionary<ulong, NetworkIdentity>();

        static Unbatcher unbatcher = new Unbatcher();

        // interest management component (optional)
        // only needed for SetHostVisibility
        public static InterestManagementBase aoi;

        // scene loading
        public static bool isLoadingScene;

        // initialization //////////////////////////////////////////////////////
        static void AddTransportHandlers()
        {
            // community Transports may forget to call OnDisconnected.
            // which could cause handlers to be added twice with +=.
            // ensure we always clear the old ones first.
            // fixes: https://github.com/vis2k/Mirror/issues/3152
            RemoveTransportHandlers();

            // += so that other systems can also hook into it (i.e. statistics)
            Transport.active.OnClientConnected += OnTransportConnected;
            Transport.active.OnClientDataReceived += OnTransportData;
            Transport.active.OnClientDisconnected += OnTransportDisconnected;
            Transport.active.OnClientError += OnTransportError;
        }

        static void RemoveTransportHandlers()
        {
            // -= so that other systems can also hook into it (i.e. statistics)
            Transport.active.OnClientConnected -= OnTransportConnected;
            Transport.active.OnClientDataReceived -= OnTransportData;
            Transport.active.OnClientDisconnected -= OnTransportDisconnected;
            Transport.active.OnClientError -= OnTransportError;
        }

        // connect /////////////////////////////////////////////////////////////
        // initialize is called before every connect
        static void Initialize(bool hostMode)
        {
            // Debug.Log($"Client Connect: {address}");
            Debug.Assert(Transport.active != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.active' first");

            // reset time interpolation on every new connect.
            // ensures last sessions' state is cleared before starting again.
            InitTimeInterpolation();

            RegisterMessageHandlers(hostMode);
            Transport.active.enabled = true;
        }

        /// <summary>Connect client to a NetworkServer by address.</summary>
        public static void Connect(string address)
        {
            Initialize(false);

            AddTransportHandlers();
            connectState = ConnectState.Connecting;
            Transport.active.ClientConnect(address);
            connection = new NetworkConnectionToServer();
        }

        /// <summary>Connect client to a NetworkServer by Uri.</summary>
        public static void Connect(Uri uri)
        {
            Initialize(false);

            AddTransportHandlers();
            connectState = ConnectState.Connecting;
            Transport.active.ClientConnect(uri);
            connection = new NetworkConnectionToServer();
        }

        // TODO why are there two connect host methods?
        // called from NetworkManager.FinishStartHost()
        public static void ConnectHost()
        {
            Initialize(true);
            connectState = ConnectState.Connected;
            HostMode.SetupConnections();
        }

        // Deprecated 2022-12-12
        [Obsolete("NetworkClient.ConnectLocalServer was moved to HostMode.InvokeOnConnected")]
        public static void ConnectLocalServer() => HostMode.InvokeOnConnected();

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
            if (NetworkMessages.UnpackId(reader, out ushort msgType))
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
                    if (reader.Remaining >= NetworkMessages.IdSize)
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
            //
            // previously OnDisconnected was only invoked if connection != null.
            // however, if DNS resolve fails in Transport.Connect(),
            // OnDisconnected would never be called because 'connection' is only
            // created after the Transport.Connect() call.
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3365
            OnDisconnectedEvent?.Invoke();

            connectState = ConnectState.Disconnected;
            ready = false;
            snapshots.Clear();
            localTimeline = 0;

            // now that everything was handled, clear the connection.
            // previously this was done in Disconnect() already, but we still
            // need it for the above OnDisconnectedEvent.
            connection = null;

            // transport handlers are only added when connecting.
            // so only remove when actually disconnecting.
            RemoveTransportHandlers();
        }

        // transport errors are forwarded to high level
        static void OnTransportError(TransportError error, string reason)
        {
            // transport errors will happen. logging a warning is enough.
            // make sure the user does not panic.
            Debug.LogWarning($"Client Transport Error: {error}: {reason}. This is fine.");
            OnErrorEvent?.Invoke(error, reason);
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
        internal static void RegisterMessageHandlers(bool hostMode)
        {
            // host mode client / remote client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (hostMode)
            {
                RegisterHandler<ObjectDestroyMessage>(OnHostClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(OnHostClientObjectHide);
                RegisterHandler<NetworkPongMessage>(_ => { }, false);
                RegisterHandler<SpawnMessage>(OnHostClientSpawn);
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnStartedMessage>(_ => { });
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>(_ => { });
                // host mode doesn't need state updates
                RegisterHandler<EntityStateMessage>(_ => { });
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
            RegisterHandler<TimeSnapshotMessage>(OnTimeSnapshotMessage);
            RegisterHandler<ChangeOwnerMessage>(OnChangeOwner);
            RegisterHandler<RpcBufferMessage>(OnRPCBufferMessage);
        }

        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }

            // we use the same WrapHandler function for server and client.
            // so let's wrap it to ignore the NetworkConnection parameter.
            // it's not needed on client. it's always NetworkClient.connection.
            void HandlerWrapped(NetworkConnection _, T value) => handler(value);
            handlers[msgType] = NetworkMessages.WrapHandler((Action<NetworkConnection, T>)HandlerWrapped, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        // RegisterHandler throws a warning (as it should) if a handler is assigned twice
        // Use of ReplaceHandler makes it clear the user intended to replace the handler
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;
            handlers[msgType] = NetworkMessages.WrapHandler(handler, requireAuthentication);
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
            ushort msgType = NetworkMessageId<T>.Id;
            return handlers.Remove(msgType);
        }

        // spawnable prefabs ///////////////////////////////////////////////////
        /// <summary>Find the registered prefab for this asset id.</summary>
        // Useful for debuggers
        public static bool GetPrefab(uint assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != 0 &&
                   prefabs.TryGetValue(assetId, out prefab) &&
                   prefab != null;
        }

        /// <summary>Validates Prefab then adds it to prefabs dictionary.</summary>
        static void RegisterPrefabIdentity(NetworkIdentity prefab)
        {
            if (prefab.assetId == 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (prefab.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            // disallow child NetworkIdentities.
            // TODO likely not necessary anymore due to the new check in
            // NetworkIdentity.OnValidate.
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
                Debug.LogWarning($"Adding prefab '{prefab.name}' with assetId '{prefab.assetId}' when spawnHandlers with same assetId already exists. If you want to use custom spawn handling, then remove the prefab from NetworkManager's registered prefabs first.");
            }

            // Debug.Log($"Registering prefab '{prefab.name}' as asset:{prefab.assetId}");

            prefabs[prefab.assetId] = prefab.gameObject;
        }

        /// <summary>Register spawnable prefab with custom assetId.</summary>
        // Note: newAssetId can not be set on GameObjects that already have an assetId
        // Note: registering with assetId is useful for assetbundles etc. a lot
        //       of people use this.
        public static void RegisterPrefab(GameObject prefab, uint newAssetId)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not register prefab because it was null");
                return;
            }

            if (newAssetId == 0)
            {
                Debug.LogError($"Could not register '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != 0 && identity.assetId != newAssetId)
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

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
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
        public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
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

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            if (identity.assetId == 0)
            {
                Debug.LogError($"Can not Register handler for '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                Debug.LogError($"Can not Register null SpawnHandler for {identity.assetId}");
                return;
            }

            RegisterPrefab(prefab, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>Register a spawnable prefab with custom assetId and custom spawn/unspawn handlers.</summary>
        // Note: newAssetId can not be set on GameObjects that already have an assetId
        // Note: registering with assetId is useful for assetbundles etc. a lot
        //       of people use this.
        // TODO why do we have one with SpawnDelegate and one with SpawnHandlerDelegate?
        public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (newAssetId == 0)
            {
                Debug.LogError($"Could not register handler for '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            if (prefab == null)
            {
                Debug.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != 0 && identity.assetId != newAssetId)
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
            uint assetId = identity.assetId;

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

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"Could not register handler for '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            uint assetId = identity.assetId;

            if (assetId == 0)
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

            if (!prefab.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"Could not unregister '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            uint assetId = identity.assetId;

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
        public static void RegisterSpawnHandler(uint assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
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
        public static void RegisterSpawnHandler(uint assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
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

            if (assetId == 0)
            {
                Debug.LogError("Can not Register SpawnHandler for empty assetId");
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
        public static void UnregisterSpawnHandler(uint assetId)
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

        internal static bool InvokeUnSpawnHandler(uint assetId, GameObject obj)
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
            else Debug.LogWarning("NetworkClient can't AddPlayer before being ready. Please call NetworkClient.Ready() first. Clients are considered ready after joining the game world.");
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
            if (message.assetId != 0)
                identity.assetId = message.assetId;

            if (!identity.gameObject.activeSelf)
            {
                identity.gameObject.SetActive(true);
            }

            // apply local values for VR support
            identity.transform.localPosition = message.position;
            identity.transform.localRotation = message.rotation;
            identity.transform.localScale = message.scale;

            // configure flags
            // the below DeserializeClient call invokes SyncVarHooks.
            // flags always need to be initialized before that.
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3259
            identity.isOwned = message.isOwner;
            identity.netId = message.netId;

            if (message.isLocalPlayer)
                InternalAddPlayer(identity);

            // configure isClient/isLocalPlayer flags.
            // => after InternalAddPlayer. can't initialize .isLocalPlayer
            //    before InternalAddPlayer sets .localPlayer
            // => before DeserializeClient, otherwise SyncVar hooks wouldn't
            //    have isClient/isLocalPlayer set yet.
            //    fixes: https://github.com/MirrorNetworking/Mirror/issues/3259
            InitializeIdentityFlags(identity);

            // deserialize components if any payload
            // (Count is 0 if there were no components)
            if (message.payload.Count > 0)
            {
                using (NetworkReaderPooled payloadReader = NetworkReaderPool.Get(message.payload))
                {
                    identity.DeserializeClient(payloadReader, true);
                }
            }

            spawned[message.netId] = identity;
            if (identity.isOwned) connection?.owned.Add(identity);

            // the initial spawn with OnObjectSpawnStarted/Finished calls all
            // object's OnStartClient/OnStartLocalPlayer after they were all
            // spawned.
            // this only happens once though.
            // for all future spawns, we need to call OnStartClient/LocalPlayer
            // here immediately since there won't be another OnObjectSpawnFinished.
            if (isSpawnFinished)
            {
                InvokeIdentityCallbacks(identity);
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

            if (message.assetId == 0 && message.sceneId == 0)
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
            spawned.TryGetValue(netid, out NetworkIdentity identity);
            return identity;
        }

        static NetworkIdentity SpawnPrefab(SpawnMessage message)
        {
            // custom spawn handler for this prefab? (for prefab pools etc.)
            //
            // IMPORTANT: look for spawn handlers BEFORE looking for registered
            //            prefabs. Unspawning also looks for unspawn handlers
            //            before falling back to regular Destroy. this needs to
            //            be consistent.
            //            https://github.com/vis2k/Mirror/issues/2705
            if (spawnHandlers.TryGetValue(message.assetId, out SpawnHandlerDelegate handler))
            {
                GameObject obj = handler(message);
                if (obj == null)
                {
                    Debug.LogError($"Spawn Handler returned null, Handler assetId '{message.assetId}'");
                    return null;
                }

                if (!obj.TryGetComponent(out NetworkIdentity identity))
                {
                    Debug.LogError($"Object Spawned by handler did not have a NetworkIdentity, Handler assetId '{message.assetId}'");
                    return null;
                }

                return identity;
            }

            // otherwise look in NetworkManager registered prefabs
            if (GetPrefab(message.assetId, out GameObject prefab))
            {
                GameObject obj = GameObject.Instantiate(prefab, message.position, message.rotation);
                //Debug.Log($"Client spawn handler instantiating [netId{message.netId} asset ID:{message.assetId} pos:{message.position} rotation:{message.rotation}]");
                return obj.GetComponent<NetworkIdentity>();
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
                // need to ensure it's not active yet because
                // PrepareToSpawnSceneObjects may be called multiple times in case
                // the ObjectSpawnStarted message is received multiple times.
                if (Utils.IsSceneObject(identity) &&
                    !identity.gameObject.activeSelf)
                {
                    if (spawnableObjects.TryGetValue(identity.sceneId, out NetworkIdentity existingIdentity))
                    {
                        string msg = $"NetworkClient: Duplicate sceneId {identity.sceneId} detected on {identity.gameObject.name} and {existingIdentity.gameObject.name}\n" +
                            $"This can happen if a networked object is persisted in DontDestroyOnLoad through loading / changing to the scene where it originated,\n" +
                            $"otherwise you may need to open and re-save the {identity.gameObject.scene} to reset scene id's.";
                        Debug.LogWarning(msg, identity.gameObject);
                    }
                    else
                    {
                        spawnableObjects.Add(identity.sceneId, identity);
                    }
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
            // paul: Initialize the objects in the same order as they were
            // initialized in the server. This is important if spawned objects
            // use data from scene objects
            foreach (NetworkIdentity identity in spawned.Values.OrderBy(uv => uv.netId))
            {
                // NetworkIdentities should always be removed from .spawned when
                // they are destroyed. for safety, let's double check here.
                if (identity != null)
                {
                    BootstrapIdentity(identity);
                }
                else Debug.LogWarning("Found null entry in NetworkClient.spawned. This is unexpected. Was the NetworkIdentity not destroyed properly?");
            }
            isSpawnFinished = true;
        }

        // host mode callbacks /////////////////////////////////////////////////
        static void OnHostClientObjectDestroy(ObjectDestroyMessage message)
        {
            //Debug.Log($"NetworkClient.OnLocalObjectObjDestroy netId:{message.netId}");

            // remove from owned (if any)
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity))
                connection.owned.Remove(identity);

            spawned.Remove(message.netId);
        }

        static void OnHostClientObjectHide(ObjectHideMessage message)
        {
            //Debug.Log($"ClientScene::OnLocalObjectObjHide netId:{message.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity) &&
                identity != null)
            {
                if (aoi != null)
                    aoi.SetHostVisibility(identity, false);
            }
        }

        internal static void OnHostClientSpawn(SpawnMessage message)
        {
            // on host mode, the object already exist in NetworkServer.spawned.
            // simply add it to NetworkClient.spawned too.
            if (NetworkServer.spawned.TryGetValue(message.netId, out NetworkIdentity identity) && identity != null)
            {
                spawned[message.netId] = identity;
                if (message.isOwner) connection.owned.Add(identity);

                // now do the actual 'spawning' on host mode
                if (message.isLocalPlayer)
                    InternalAddPlayer(identity);

                // set visibility before invoking OnStartClient etc. callbacks
                if (aoi != null)
                    aoi.SetHostVisibility(identity, true);

                identity.isOwned = message.isOwner;
                BootstrapIdentity(identity);
            }
        }

        // client-only mode callbacks //////////////////////////////////////////
        static void OnEntityStateMessage(EntityStateMessage message)
        {
            // Debug.Log($"NetworkClient.OnUpdateVarsMessage {msg.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity) && identity != null)
            {
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(message.payload))
                    identity.DeserializeClient(reader, false);
            }
            else Debug.LogWarning($"Did not find target for sync message for {message.netId} . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
        }

        static void OnRPCMessage(RpcMessage message)
        {
            // Debug.Log($"NetworkClient.OnRPCMessage hash:{message.functionHash} netId:{message.netId}");
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity))
            {
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(message.payload))
                    identity.HandleRemoteCall(message.componentIndex, message.functionHash, RemoteCallType.ClientRpc, reader);
            }
            // Rpcs often can't be applied if interest management unspawned them
        }

        static void OnRPCBufferMessage(RpcBufferMessage message)
        {
            // Debug.Log($"NetworkClient.OnRPCBufferMessage of {message.payload.Count} bytes");
            // parse all rpc messages from the buffer
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(message.payload))
            {
                while (reader.Remaining > 0)
                {
                    // read message without header
                    RpcMessage rpcMessage = reader.Read<RpcMessage>();
                    OnRPCMessage(rpcMessage);
                }
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

        // ChangeOwnerMessage contains new 'owned' and new 'localPlayer'
        // that we need to apply to the identity.
        internal static void ChangeOwner(NetworkIdentity identity, ChangeOwnerMessage message)
        {
            // local player before, but not anymore?
            // call OnStopLocalPlayer before setting new values.
            if (identity.isLocalPlayer && !message.isLocalPlayer)
            {
                identity.OnStopLocalPlayer();
            }

            // set ownership flag (aka authority)
            identity.isOwned = message.isOwner;

            // Add / Remove to client's connectionToServer.owned hashset.
            if (identity.isOwned)
                connection?.owned.Add(identity);
            else
                connection?.owned.Remove(identity);

            // Call OnStartAuthority / OnStopAuthority
            identity.NotifyAuthority();

            // set localPlayer flag
            identity.isLocalPlayer = message.isLocalPlayer;

            // identity is now local player. set our static helper field to it.
            if (identity.isLocalPlayer)
            {
                localPlayer = identity;
                identity.connectionToServer = connection;
                identity.OnStartLocalPlayer();
            }
            // identity's isLocalPlayer was set to false.
            // clear our static localPlayer IF (and only IF) it was that one before.
            else if (localPlayer == identity)
            {
                localPlayer = null;
                // TODO set .connectionToServer to null for old local player?
                // since we set it in the above 'if' case too.
            }
        }

        // set up NetworkIdentity flags on the client.
        // needs to be separate from invoking callbacks.
        // cleaner, and some places need to set flags first.
        static void InitializeIdentityFlags(NetworkIdentity identity)
        {
            // initialize flags before invoking callbacks.
            // this way isClient/isLocalPlayer is correct during callbacks.
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3362
            identity.isClient = true;
            identity.isLocalPlayer = localPlayer == identity;

            // .connectionToServer is only available for local players.
            // set it here, before invoking any callbacks.
            // this way it's available in _all_ callbacks.
            if (identity.isLocalPlayer)
                identity.connectionToServer = connection;
        }

        // invoke NetworkIdentity callbacks on the client.
        // needs to be separate from configuring flags.
        // cleaner, and some places need to set flags first.
        static void InvokeIdentityCallbacks(NetworkIdentity identity)
        {
            // invoke OnStartAuthority
            identity.NotifyAuthority();

            // invoke OnStartClient
            identity.OnStartClient();

            // invoke OnStartLocalPlayer
            if (identity.isLocalPlayer)
                identity.OnStartLocalPlayer();
        }

        // configure flags & invoke callbacks
        static void BootstrapIdentity(NetworkIdentity identity)
        {
            InitializeIdentityFlags(identity);
            InvokeIdentityCallbacks(identity);
        }

        // broadcast ///////////////////////////////////////////////////////////
        static void BroadcastTimeSnapshot()
        {
            Send(new TimeSnapshotMessage(), Channels.Unreliable);
        }

        // make sure Broadcast() is only called every sendInterval.
        // calling it every update() would require too much bandwidth.
        static void Broadcast()
        {
            // joined the world yet?
            if (!connection.isReady) return;

            // nothing to do in host mode. server already knows the state.
            if (NetworkServer.active) return;

            // send time snapshot every sendInterval.
            BroadcastTimeSnapshot();

            // for each entity that the client owns
            foreach (NetworkIdentity identity in connection.owned)
            {
                // make sure it's not null or destroyed.
                // (which can happen if someone uses
                //  GameObject.Destroy instead of
                //  NetworkServer.Destroy)
                if (identity != null)
                {
                    using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                    {
                        // get serialization for this entity viewed by this connection
                        // (if anything was serialized this time)
                        identity.SerializeClient(writer);
                        if (writer.Position > 0)
                        {
                            // send state update message
                            EntityStateMessage message = new EntityStateMessage
                            {
                                netId = identity.netId,
                                payload = writer.ToArraySegment()
                            };
                            Send(message);

                            // reset dirty bits so it's not resent next time.
                            identity.ClearDirtyComponentsDirtyBits();
                        }
                    }
                }
                // spawned list should have no null entries because we
                // always call Remove in OnObjectDestroy everywhere.
                // if it does have null then we missed something.
                else Debug.LogWarning($"Found 'null' entry in owned list for client. This is unexpected behaviour.");
            }
        }

        // update //////////////////////////////////////////////////////////////
        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate()
        {
            // process all incoming messages first before updating the world
            if (Transport.active != null)
                Transport.active.ClientEarlyUpdate();

            // time snapshot interpolation
            UpdateTimeInterpolation();
        }

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkLateUpdate()
        {
            // broadcast ClientToServer components while active
            if (active)
            {
                // broadcast every sendInterval.
                // AccurateInterval to avoid update frequency inaccuracy issues:
                // https://github.com/vis2k/Mirror/pull/3153
                //
                // for example, host mode server doesn't set .targetFrameRate.
                // Broadcast() would be called every tick.
                // snapshots might be sent way too often, etc.
                //
                // during tests, we always call Broadcast() though.
                //
                // also important for syncInterval=0 components like
                // NetworkTransform, so they can sync on same interval as time
                // snapshots _but_ not every single tick.
                //
                // Unity 2019 doesn't have Time.timeAsDouble yet
                if (!Application.isPlaying ||
                    AccurateInterval.Elapsed(NetworkTime.localTime, sendInterval, ref lastSendTime))
                {
                    Broadcast();
                }
            }

            // update connections to flush out messages _after_ broadcast
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
            if (Transport.active != null)
                Transport.active.ClientLateUpdate();
        }

        // destroy /////////////////////////////////////////////////////////////
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

                        // NetworkClient.Shutdown calls DestroyAllClientObjects.
                        // which destroys all objects in NetworkClient.spawned.
                        // => NC.spawned contains owned & observed objects
                        // => in host mode, we CAN NOT destroy observed objects.
                        // => that would destroy them other connection's objects
                        //    on the host server, making them disconnect.
                        // https://github.com/vis2k/Mirror/issues/2954
                        bool hostOwned = identity.connectionToServer is LocalConnectionToServer;
                        bool shouldDestroy = !identity.isServer || hostOwned;
                        if (shouldDestroy)
                        {
                            bool wasUnspawned = InvokeUnSpawnHandler(identity.assetId, identity.gameObject);

                            // unspawned objects should be reset for reuse later.
                            if (wasUnspawned)
                            {
                                identity.Reset();
                            }
                            // without unspawn handler, we need to disable/destroy.
                            else
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
                }
                spawned.Clear();
                connection?.owned.Clear();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogException(e);
                Debug.LogError("Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            }
        }

        static void DestroyObject(uint netId)
        {
            // Debug.Log($"NetworkClient.OnObjDestroy netId: {netId}");
            if (spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
            {
                if (identity.isLocalPlayer)
                    identity.OnStopLocalPlayer();

                identity.OnStopClient();

                // custom unspawn handler for this prefab? (for prefab pools etc.)
                if (InvokeUnSpawnHandler(identity.assetId, identity.gameObject))
                {
                    // reset object after user's handler
                    identity.Reset();
                }
                // otherwise fall back to default Destroy
                else if (identity.sceneId == 0)
                {
                    // don't call reset before destroy so that values are still set in OnDestroy
                    GameObject.Destroy(identity.gameObject);
                }
                // scene object.. disable it in scene instead of destroying
                else
                {
                    identity.gameObject.SetActive(false);
                    spawnableObjects[identity.sceneId] = identity;
                    // reset for scene objects
                    identity.Reset();
                }

                // remove from dictionary no matter how it is unspawned
                connection.owned.Remove(identity); // if any
                spawned.Remove(netId);
            }
            //else Debug.LogWarning($"Did not find target for destroy message for {netId}");
        }

        // shutdown ////////////////////////////////////////////////////////////
        /// <summary>Shutdown the client.</summary>
        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Shutdown()
        {
            //Debug.Log("Shutting down client.");

            // objects need to be destroyed before spawners are cleared
            // fixes: https://github.com/MirrorNetworking/Mirror/issues/3334
            DestroyAllClientObjects();

            // calls prefabs.Clear();
            // calls spawnHandlers.Clear();
            // calls unspawnHandlers.Clear();
            ClearSpawners();

            spawned.Clear();
            connection?.owned.Clear();
            handlers.Clear();
            spawnableObjects.Clear();

            // IMPORTANT: do NOT call NetworkIdentity.ResetStatics() here!
            // calling StopClient() in host mode would reset nextNetId to 1,
            // causing next connection to have a duplicate netId accidentally.
            // => see also: https://github.com/vis2k/Mirror/issues/2954
            //NetworkIdentity.ResetStatics();
            // => instead, reset only the client sided statics.
            NetworkIdentity.ResetClientStatics();

            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            if (Transport.active != null)
                Transport.active.ClientDisconnect();

            // reset statics
            connectState = ConnectState.None;
            connection = null;
            localPlayer = null;
            ready = false;
            isSpawnFinished = false;
            isLoadingScene = false;
            lastSendTime = 0;

            unbatcher = new Unbatcher();

            // clear events. someone might have hooked into them before, but
            // we don't want to use those hooks after Shutdown anymore.
            OnConnectedEvent = null;
            OnDisconnectedEvent = null;
            OnErrorEvent = null;
        }

        // GUI /////////////////////////////////////////////////////////////////
        // called from NetworkManager to display timeline interpolation status.
        // useful to indicate catchup / slowdown / dynamic adjustment etc.
        public static void OnGUI()
        {
            // only if in world
            if (!ready) return;

            GUILayout.BeginArea(new Rect(10, 5, 800, 50));

            GUILayout.BeginHorizontal("Box");
            GUILayout.Label("Snapshot Interp.:");
            // color while catching up / slowing down
            if (localTimescale > 1) GUI.color = Color.green; // green traffic light = go fast
            else if (localTimescale < 1) GUI.color = Color.red;   // red traffic light = go slow
            else GUI.color = Color.white;
            GUILayout.Box($"timeline: {localTimeline:F2}");
            GUILayout.Box($"buffer: {snapshots.Count}");
            GUILayout.Box($"DriftEMA: {NetworkClient.driftEma.Value:F2}");
            GUILayout.Box($"DelTimeEMA: {NetworkClient.deliveryTimeEma.Value:F2}");
            GUILayout.Box($"timescale: {localTimescale:F2}");
            GUILayout.Box($"BTM: {snapshotSettings.bufferTimeMultiplier:F2}");
            GUILayout.Box($"RTT: {NetworkTime.rtt * 1000:000}");
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
