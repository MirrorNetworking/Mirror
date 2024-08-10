using System;
using System.Collections.Generic;
using System.Linq;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    public enum ReplacePlayerOptions
    {
        /// <summary>Player Object remains active on server and clients. Ownership is not removed</summary>
        KeepAuthority,
        /// <summary>Player Object remains active on server and clients. Only ownership is removed</summary>
        KeepActive,
        /// <summary>Player Object is unspawned on clients but remains on server</summary>
        Unspawn,
        /// <summary>Player Object is destroyed on server and clients</summary>
        Destroy
    }

    public enum RemovePlayerOptions
    {
        /// <summary>Player Object remains active on server and clients. Only ownership is removed</summary>
        KeepActive,
        /// <summary>Player Object is unspawned on clients but remains on server</summary>
        Unspawn,
        /// <summary>Player Object is destroyed on server and clients</summary>
        Destroy
    }

    /// <summary>NetworkServer handles remote connections and has a local connection for a local client.</summary>
    public static partial class NetworkServer
    {
        static bool initialized;
        public static int maxConnections;

        /// <summary>Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.</summary>
        // overwritten by NetworkManager (if any)
        public static int tickRate = 60;

        // tick rate is in Hz.
        // convert to interval in seconds for convenience where needed.
        //
        // send interval is 1 / sendRate.
        // but for tests we need a way to set it to exactly 0.
        // 1 / int.max would not be exactly 0, so handel that manually.
        public static float tickInterval => tickRate < int.MaxValue ? 1f / tickRate : 0; // for 30 Hz, that's 33ms

        // time & value snapshot interpolation are separate.
        // -> time is interpolated globally on NetworkClient / NetworkConnection
        // -> value is interpolated per-component, i.e. NetworkTransform.
        // however, both need to be on the same send interval.
        public static int sendRate => tickRate;
        public static float sendInterval => sendRate < int.MaxValue ? 1f / sendRate : 0; // for 30 Hz, that's 33ms
        static double lastSendTime;

        /// <summary>Connection to host mode client (if any)</summary>
        public static LocalConnectionToClient localConnection { get; private set; }

        /// <summary>Dictionary of all server connections, with connectionId as key</summary>
        public static Dictionary<int, NetworkConnectionToClient> connections =
            new Dictionary<int, NetworkConnectionToClient>();

        /// <summary>Message Handlers dictionary, with messageId as key</summary>
        internal static Dictionary<ushort, NetworkMessageDelegate> handlers =
            new Dictionary<ushort, NetworkMessageDelegate>();

        /// <summary>All spawned NetworkIdentities by netId.</summary>
        // server sees ALL spawned ones.
        public static readonly Dictionary<uint, NetworkIdentity> spawned =
            new Dictionary<uint, NetworkIdentity>();

        /// <summary>Single player mode can use dontListen to not accept incoming connections</summary>
        // see also: https://github.com/vis2k/Mirror/pull/2595
        public static bool dontListen;

        /// <summary>active checks if the server has been started either has standalone or as host server.</summary>
        public static bool active { get; internal set; }

        /// <summary>active checks if the server has been started in host mode.</summary>
        // naming consistent with NetworkClient.activeHost.
        public static bool activeHost => localConnection != null;

        // scene loading
        public static bool isLoadingScene;

        // interest management component (optional)
        // by default, everyone observes everyone
        public static InterestManagementBase aoi;

        // For security, it is recommended to disconnect a player if a networked
        // action triggers an exception\nThis could prevent components being
        // accessed in an undefined state, which may be an attack vector for
        // exploits.
        //
        // However, some games may want to allow exceptions in order to not
        // interrupt the player's experience.
        public static bool exceptionsDisconnect = true; // security by default

        // Mirror global disconnect inactive option, independent of Transport.
        // not all Transports do this properly, and it's easiest to configure this just once.
        // this is very useful for some projects, keep it.
        public static bool disconnectInactiveConnections;
        public static float disconnectInactiveTimeout = 60;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        // => public so that custom NetworkManagers can hook into it
        public static Action<NetworkConnectionToClient> OnConnectedEvent;
        public static Action<NetworkConnectionToClient> OnDisconnectedEvent;
        public static Action<NetworkConnectionToClient, TransportError, string> OnErrorEvent;
        public static Action<NetworkConnectionToClient, Exception> OnTransportExceptionEvent;

        // keep track of actual achieved tick rate.
        // might become lower under heavy load.
        // very useful for profiling etc.
        // measured over 1s each, same as frame rate. no EMA here.
        public static int actualTickRate;
        static double actualTickRateStart;   // start time when counting
        static int actualTickRateCounter; // current counter since start

        // profiling
        // includes transport update time, because transport calls handlers etc.
        // averaged over 1s by passing 'tickRate' to constructor.
        public static TimeSample earlyUpdateDuration;
        public static TimeSample lateUpdateDuration;

        // capture full Unity update time from before Early- to after LateUpdate
        public static TimeSample fullUpdateDuration;

        /// <summary>Starts server and listens to incoming connections with max connections limit.</summary>
        public static void Listen(int maxConns)
        {
            Initialize();
            maxConnections = maxConns;

            // only start server if we want to listen
            if (!dontListen)
            {
                Transport.active.ServerStart();

                if (Transport.active is PortTransport portTransport)
                {
                    if (Utils.IsHeadless())
                    {
#if !UNITY_EDITOR
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Server listening on port {portTransport.Port}");
                        Console.ResetColor();
#else
                        Debug.Log($"Server listening on port {portTransport.Port}");
#endif
                    }
                }
                else
                    Debug.Log("Server started listening");
            }

            active = true;
            RegisterMessageHandlers();
        }

        // initialization / shutdown ///////////////////////////////////////////
        static void Initialize()
        {
            if (initialized)
                return;

            // safety: ensure Weaving succeded.
            // if it silently failed, we would get lots of 'writer not found'
            // and other random errors at runtime instead. this is cleaner.
            if (!WeaverFuse.Weaved())
            {
                // if it failed, throw an exception to early exit all Listen calls.
                throw new Exception("NetworkServer won't start because Weaving failed or didn't run.");
            }

            // Debug.Log($"NetworkServer Created version {Version.Current}");

            //Make sure connections are cleared in case any old connections references exist from previous sessions
            connections.Clear();

            // reset Interest Management so that rebuild intervals
            // start at 0 when starting again.
            if (aoi != null) aoi.ResetState();

            // reset NetworkTime
            NetworkTime.ResetStatics();

            Debug.Assert(Transport.active != null, "There was no active transport when calling NetworkServer.Listen, If you are calling Listen manually then make sure to set 'Transport.active' first");
            AddTransportHandlers();

            initialized = true;

            // profiling
            earlyUpdateDuration = new TimeSample(sendRate);
            lateUpdateDuration = new TimeSample(sendRate);
            fullUpdateDuration = new TimeSample(sendRate);
        }

        static void AddTransportHandlers()
        {
            // += so that other systems can also hook into it (i.e. statistics)
#pragma warning disable CS0618 // Type or member is obsolete
            Transport.active.OnServerConnected += OnTransportConnected;
#pragma warning restore CS0618 // Type or member is obsolete
            Transport.active.OnServerConnectedWithAddress += OnTransportConnectedWithAddress;
            Transport.active.OnServerDataReceived += OnTransportData;
            Transport.active.OnServerDisconnected += OnTransportDisconnected;
            Transport.active.OnServerError += OnTransportError;
            Transport.active.OnServerTransportException += OnTransportException;
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
                Transport.active.ServerStop();

                // transport handlers are hooked into when initializing.
                // so only remove them when shutting down.
                RemoveTransportHandlers();

                initialized = false;
            }

            // Reset all statics here....
            dontListen = false;
            isLoadingScene = false;
            lastSendTime = 0;
            actualTickRate = 0;

            localConnection = null;

            connections.Clear();
            connectionsCopy.Clear();
            handlers.Clear();

            // destroy all spawned objects, _then_ set inactive.
            // make sure .active is still true before calling this.
            // otherwise modifying SyncLists in OnStopServer would throw
            // because .IsWritable() check checks if NetworkServer.active.
            // https://github.com/MirrorNetworking/Mirror/issues/3344
            CleanupSpawned();
            active = false;

            // sets nextNetworkId to 1
            // sets clientAuthorityCallback to null
            // sets previousLocalPlayer to null
            NetworkIdentity.ResetStatics();

            // clear events. someone might have hooked into them before, but
            // we don't want to use those hooks after Shutdown anymore.
            OnConnectedEvent = null;
            OnDisconnectedEvent = null;
            OnErrorEvent = null;
            OnTransportExceptionEvent = null;

            if (aoi != null) aoi.ResetState();
        }

        static void RemoveTransportHandlers()
        {
            // -= so that other systems can also hook into it (i.e. statistics)
#pragma warning disable CS0618 // Type or member is obsolete
            Transport.active.OnServerConnected -= OnTransportConnected;
#pragma warning restore CS0618 // Type or member is obsolete
            Transport.active.OnServerConnectedWithAddress -= OnTransportConnectedWithAddress;
            Transport.active.OnServerDataReceived -= OnTransportData;
            Transport.active.OnServerDisconnected -= OnTransportDisconnected;
            Transport.active.OnServerError -= OnTransportError;
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
                    // NetworkServer.Destroy resets if scene object, destroys if prefab.
                    Destroy(identity.gameObject);
                }
            }

            spawned.Clear();
        }

        internal static void RegisterMessageHandlers()
        {
            RegisterHandler<ReadyMessage>(OnClientReadyMessage);
            RegisterHandler<CommandMessage>(OnCommandMessage);
            RegisterHandler<NetworkPingMessage>(NetworkTime.OnServerPing, false);
            RegisterHandler<NetworkPongMessage>(NetworkTime.OnServerPong, false);
            RegisterHandler<EntityStateMessage>(OnEntityStateMessage, true);
            RegisterHandler<TimeSnapshotMessage>(OnTimeSnapshotMessage, false); // unreliable may arrive before reliable authority went through
        }

        // remote calls ////////////////////////////////////////////////////////
        // Handle command from specific player, this could be one of multiple
        // players on a single client
        // default ready handler.
        static void OnClientReadyMessage(NetworkConnectionToClient conn, ReadyMessage msg)
        {
            // Debug.Log($"Default handler for ready message from {conn}");
            SetClientReady(conn);
        }

        static void OnCommandMessage(NetworkConnectionToClient conn, CommandMessage msg, int channelId)
        {
            if (!conn.isReady)
            {
                // Clients may be set NotReady due to scene change or other game logic by user, e.g. respawning.
                // Ignore commands that may have been in flight before client received NotReadyMessage message.
                // Unreliable messages may be out of order, so don't spam warnings for those.
                if (channelId == Channels.Reliable)
                {
                    // Attempt to identify the target object, component, and method to narrow down the cause of the error.
                    if (spawned.TryGetValue(msg.netId, out NetworkIdentity netIdentity))
                        if (msg.componentIndex < netIdentity.NetworkBehaviours.Length && netIdentity.NetworkBehaviours[msg.componentIndex] is NetworkBehaviour component)
                            if (RemoteProcedureCalls.GetFunctionMethodName(msg.functionHash, out string methodName))
                            {
                                Debug.LogWarning($"Command {methodName} received for {netIdentity.name} [netId={msg.netId}] component {component.name} [index={msg.componentIndex}] when client not ready.\nThis may be ignored if client intentionally set NotReady.");
                                return;
                            }

                    Debug.LogWarning("Command received while client is not ready.\nThis may be ignored if client intentionally set NotReady.");
                }
                return;
            }

            if (!spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                // over reliable channel, commands should always come after spawn.
                // over unreliable, they might come in before the object was spawned.
                // for example, NetworkTransform.
                // let's not spam the console for unreliable out of order messages.
                if (channelId == Channels.Reliable)
                    Debug.LogWarning($"Spawned object not found when handling Command message netId={msg.netId}");
                return;
            }

            // Commands can be for player objects, OR other objects with client-authority
            // -> so if this connection's controller has a different netId then
            //    only allow the command if clientAuthorityOwner
            bool requiresAuthority = RemoteProcedureCalls.CommandRequiresAuthority(msg.functionHash);
            if (requiresAuthority && identity.connectionToClient != conn)
            {
                // Attempt to identify the component and method to narrow down the cause of the error.
                if (msg.componentIndex < identity.NetworkBehaviours.Length && identity.NetworkBehaviours[msg.componentIndex] is NetworkBehaviour component)
                    if (RemoteProcedureCalls.GetFunctionMethodName(msg.functionHash, out string methodName))
                    {
                        Debug.LogWarning($"Command {methodName} received for {identity.name} [netId={msg.netId}] component {component.name} [index={msg.componentIndex}] without authority");
                        return;
                    }

                Debug.LogWarning($"Command received for {identity.name} [netId={msg.netId}] without authority");
                return;
            }

            // Debug.Log($"OnCommandMessage for netId:{msg.netId} conn:{conn}");

            using (NetworkReaderPooled networkReader = NetworkReaderPool.Get(msg.payload))
                identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, RemoteCallType.Command, networkReader, conn);
        }

        // client to server broadcast //////////////////////////////////////////
        // for client's owned ClientToServer components.
        static void OnEntityStateMessage(NetworkConnectionToClient connection, EntityStateMessage message)
        {
            // need to validate permissions carefully.
            // an attacker may attempt to modify a not-owned or not-ClientToServer component.

            // valid netId?
            if (spawned.TryGetValue(message.netId, out NetworkIdentity identity) && identity != null)
            {
                // owned by the connection?
                if (identity.connectionToClient == connection)
                {
                    using (NetworkReaderPooled reader = NetworkReaderPool.Get(message.payload))
                    {
                        // DeserializeServer checks permissions internally.
                        // failure to deserialize disconnects to prevent exploits.
                        if (!identity.DeserializeServer(reader))
                        {
                            if (exceptionsDisconnect)
                            {
                                Debug.LogError($"Server failed to deserialize client state for {identity.name} with netId={identity.netId}, Disconnecting.");
                                connection.Disconnect();
                            }
                            else
                                Debug.LogWarning($"Server failed to deserialize client state for {identity.name} with netId={identity.netId}.");
                        }
                    }
                }
                // An attacker may attempt to modify another connection's entity
                // This could also be a race condition of message in flight when
                // RemoveClientAuthority is called, so not malicious.
                // Don't disconnect, just log the warning.
                else
                    Debug.LogWarning($"EntityStateMessage from {connection} for {identity.name} without authority.");
            }
            // no warning. don't spam server logs.
            // else Debug.LogWarning($"Did not find target for sync message for {message.netId} . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
        }

        // client sends TimeSnapshotMessage every sendInterval.
        // batching already includes the remoteTimestamp.
        // we simply insert it on-message here.
        // => only for reliable channel. unreliable would always arrive earlier.
        static void OnTimeSnapshotMessage(NetworkConnectionToClient connection, TimeSnapshotMessage _)
        {
            // insert another snapshot for snapshot interpolation.
            // before calling OnDeserialize so components can use
            // NetworkTime.time and NetworkTime.timeStamp.

            // TODO validation?
            // maybe we shouldn't allow timeline to deviate more than a certain %.
            // for now, this is only used for client authority movement.

            // Unity 2019 doesn't have Time.timeAsDouble yet
            //
            // NetworkTime uses unscaled time and ignores Time.timeScale.
            // fixes Time.timeScale getting server & client time out of sync:
            // https://github.com/MirrorNetworking/Mirror/issues/3409
            connection.OnTimeSnapshot(new TimeSnapshot(connection.remoteTimeStamp, NetworkTime.localTime));
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
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // pack message only once
                NetworkMessages.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // validate packet size immediately.
                // we know how much can fit into one batch at max.
                // if it's larger, log an error immediately with the type <T>.
                // previously we only logged in Update() when processing batches,
                // but there we don't have type information anymore.
                int max = NetworkMessages.MaxMessageSize(channelId);
                if (writer.Position > max)
                {
                    Debug.LogError($"NetworkServer.SendToAll: message of type {typeof(T)} with a size of {writer.Position} bytes is larger than the max allowed message size in one batch: {max}.\nThe message was dropped, please make it smaller.");
                    return;
                }

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
            if (identity == null || identity.observers.Count == 0)
                return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // pack message into byte[] once
                NetworkMessages.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // validate packet size immediately.
                // we know how much can fit into one batch at max.
                // if it's larger, log an error immediately with the type <T>.
                // previously we only logged in Update() when processing batches,
                // but there we don't have type information anymore.
                int max = NetworkMessages.MaxMessageSize(channelId);
                if (writer.Position > max)
                {
                    Debug.LogError($"NetworkServer.SendToObservers: message of type {typeof(T)} with a size of {writer.Position} bytes is larger than the max allowed message size in one batch: {max}.\nThe message was dropped, please make it smaller.");
                    return;
                }

                foreach (NetworkConnectionToClient conn in identity.observers.Values)
                {
                    conn.Send(segment, channelId);
                }

                NetworkDiagnostics.OnSend(message, channelId, segment.Count, identity.observers.Count);
            }
        }

        /// <summary>Send a message to only clients which are ready with option to include the owner of the object identity</summary>
        // TODO obsolete this later. it's not used anymore
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable)
            where T : struct, NetworkMessage
        {
            // Debug.Log($"Server.SendToReady {typeof(T)}");
            if (identity == null || identity.observers.Count == 0)
                return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // pack message only once
                NetworkMessages.Pack(message, writer);
                ArraySegment<byte> segment = writer.ToArraySegment();

                // validate packet size immediately.
                // we know how much can fit into one batch at max.
                // if it's larger, log an error immediately with the type <T>.
                // previously we only logged in Update() when processing batches,
                // but there we don't have type information anymore.
                int max = NetworkMessages.MaxMessageSize(channelId);
                if (writer.Position > max)
                {
                    Debug.LogError($"NetworkServer.SendToReadyObservers: message of type {typeof(T)} with a size of {writer.Position} bytes is larger than the max allowed message size in one batch: {max}.\nThe message was dropped, please make it smaller.");
                    return;
                }

                int count = 0;
                foreach (NetworkConnectionToClient conn in identity.observers.Values)
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
        // TODO obsolete this later. it's not used anymore
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, int channelId)
            where T : struct, NetworkMessage
        {
            SendToReadyObservers(identity, message, true, channelId);
        }

        // transport events ////////////////////////////////////////////////////
        // called by transport
        static void OnTransportConnected(int connectionId)
            => OnTransportConnectedWithAddress(connectionId, Transport.active.ServerGetClientAddress(connectionId));

        static void OnTransportConnectedWithAddress(int connectionId, string clientAddress)
        {
            if (IsConnectionAllowed(connectionId))
            {
                // create a connection
                NetworkConnectionToClient conn = new NetworkConnectionToClient(connectionId, clientAddress);
                OnConnected(conn);
            }
            else
            {
                // kick the client immediately
                Transport.active.ServerDisconnect(connectionId);
            }
        }

        static bool IsConnectionAllowed(int connectionId)
        {
            // connectionId needs to be != 0 because 0 is reserved for local player
            // note that some transports like kcp generate connectionId by
            // hashing which can be < 0 as well, so we need to allow < 0!
            if (connectionId == 0)
            {
                Debug.LogError($"Server.HandleConnect: invalid connectionId: {connectionId} . Needs to be != 0, because 0 is reserved for local player.");
                return false;
            }

            // connectionId not in use yet?
            if (connections.ContainsKey(connectionId))
            {
                Debug.LogError($"Server connectionId {connectionId} already in use...client will be kicked");
                return false;
            }

            // are more connections allowed? if not, kick
            // (it's easier to handle this in Mirror, so Transports can have
            //  less code and third party transport might not do that anyway)
            // (this way we could also send a custom 'tooFull' message later,
            //  Transport can't do that)
            if (connections.Count >= maxConnections)
            {
                Debug.LogError($"Server full, client {connectionId} will be kicked");
                return false;
            }

            return true;
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
            if (NetworkMessages.UnpackId(reader, out ushort msgType))
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
                    if (exceptionsDisconnect)
                    {
                        Debug.LogError($"NetworkServer: received message from connectionId:{connectionId} was too short (messages should start with message id). Disconnecting.");
                        connection.Disconnect();
                    }
                    else
                        Debug.LogWarning($"NetworkServer: received message from connectionId:{connectionId} was too short (messages should start with message id).");

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
                       connection.unbatcher.GetNextMessage(out ArraySegment<byte> message, out double remoteTimestamp))
                {
                    using (NetworkReaderPooled reader = NetworkReaderPool.Get(message))
                    {
                        // enough to read at least header size?
                        if (reader.Remaining >= NetworkMessages.IdSize)
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
                                if (exceptionsDisconnect)
                                {
                                    Debug.LogError($"NetworkServer: failed to unpack and invoke message. Disconnecting {connectionId}.");
                                    connection.Disconnect();
                                }
                                else
                                    Debug.LogWarning($"NetworkServer: failed to unpack and invoke message from connectionId:{connectionId}.");

                                return;
                            }
                        }
                        // otherwise disconnect
                        else
                        {
                            if (exceptionsDisconnect)
                            {
                                Debug.LogError($"NetworkServer: received message from connectionId:{connectionId} was too short (messages should start with message id). Disconnecting.");
                                connection.Disconnect();
                            }
                            else
                                Debug.LogWarning($"NetworkServer: received message from connectionId:{connectionId} was too short (messages should start with message id).");

                            return;
                        }
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
                conn.Cleanup();
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

        // transport errors are forwarded to high level
        static void OnTransportError(int connectionId, TransportError error, string reason)
        {
            // transport errors will happen. logging a warning is enough.
            // make sure the user does not panic.
            Debug.LogWarning($"Server Transport Error for connId={connectionId}: {error}: {reason}. This is fine.");
            // try get connection. passes null otherwise.
            connections.TryGetValue(connectionId, out NetworkConnectionToClient conn);
            OnErrorEvent?.Invoke(conn, error, reason);
        }

        // transport errors are forwarded to high level
        static void OnTransportException(int connectionId, Exception exception)
        {
            // transport errors will happen. logging a warning is enough.
            // make sure the user does not panic.
            Debug.LogWarning($"Server Transport Exception for connId={connectionId}: {exception}");
            // try get connection. passes null otherwise.
            connections.TryGetValue(connectionId, out NetworkConnectionToClient conn);
            OnTransportExceptionEvent?.Invoke(conn, exception);
        }

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
            //    NetworkConnection.Disconnect()
            conn.RemoveFromObservingsObservers();
            conn.identity = null;
        }

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        // TODO obsolete this some day to always use the channelId version.
        //      all handlers in this version are wrapped with 1 extra action.
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }

            // register Id <> Type in lookup for debugging.
            NetworkMessages.Lookup[msgType] = typeof(T);

            handlers[msgType] = NetworkMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
        }

        /// <summary>Register a handler for message type T. Most should require authentication.</summary>
        // This version passes channelId to the handler.
        public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkServer.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }

            // register Id <> Type in lookup for debugging.
            NetworkMessages.Lookup[msgType] = typeof(T);

            handlers[msgType] = NetworkMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler<T>((_, value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;

            // register Id <> Type in lookup for debugging.
            NetworkMessages.Lookup[msgType] = typeof(T);

            handlers[msgType] = NetworkMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
        }

        /// <summary>Replace a handler for message type T. Most should require authentication.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;

            // register Id <> Type in lookup for debugging.
            NetworkMessages.Lookup[msgType] = typeof(T);

            handlers[msgType] = NetworkMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
        }

        /// <summary>Unregister a handler for a message type T.</summary>
        public static void UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            ushort msgType = NetworkMessageId<T>.Id;
            handlers.Remove(msgType);
        }

        /// <summary>Clears all registered message handlers.</summary>
        public static void ClearHandlers() => handlers.Clear();

        internal static bool GetNetworkIdentity(GameObject go, out NetworkIdentity identity)
        {
            if (!go.TryGetComponent(out identity))
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
            // until then, let's copy .Values to avoid InvalidOperationException.
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
            // this used to set active=false.
            // however, then Shutdown can't properly destroy objects:
            // https://github.com/MirrorNetworking/Mirror/issues/3344
            // "DisconnectAll" should only disconnect all, not set inactive.
            // active = false;
        }

        // add/remove/replace player ///////////////////////////////////////////
        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        // When a player is added for a connection, the client for that
        // connection is made ready automatically. The player object is
        // automatically spawned, so you do not need to call NetworkServer.Spawn
        // for that object. This function is used for "adding" a player, not for
        // "replacing" the player on a connection. If there is already a player
        // on this playerControllerId for this connection, this will fail.
        public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            return AddPlayerForConnection(conn, player);
        }

        /// <summary>Called by server after AddPlayer message to add the player for the connection.</summary>
        // When a player is added for a connection, the client for that
        // connection is made ready automatically. The player object is
        // automatically spawned, so you do not need to call NetworkServer.Spawn
        // for that object. This function is used for "adding" a player, not for
        // "replacing" the player on a connection. If there is already a player
        // on this playerControllerId for this connection, this will fail.
        public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player)
        {
            if (!player.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogWarning($"AddPlayer: player GameObject has no NetworkIdentity. Please add a NetworkIdentity to {player}");
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
                identity.isOwned = true;
                NetworkClient.InternalAddPlayer(identity);
            }

            // set ready if not set yet
            SetClientReady(conn);

            // Debug.Log($"Adding new playerGameObject object netId: {identity.netId} asset ID: {identity.assetId}");

            Respawn(identity);
            return true;
        }

        // Deprecated 2024-008-09
        [Obsolete("Use ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, ReplacePlayerOptions replacePlayerOptions) instead")]
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, bool keepAuthority = false)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
                identity.assetId = assetId;

            return ReplacePlayerForConnection(conn, player, keepAuthority ? ReplacePlayerOptions.KeepAuthority : ReplacePlayerOptions.KeepActive);
        }

        // Deprecated 2024-008-09
        [Obsolete("Use ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, ReplacePlayerOptions replacePlayerOptions) instead")]
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, bool keepAuthority = false)
        {
            return ReplacePlayerForConnection(conn, player, keepAuthority ? ReplacePlayerOptions.KeepAuthority : ReplacePlayerOptions.KeepActive);
        }

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        // This does NOT change the ready state of the connection, so it can safely be used while changing scenes.
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, ReplacePlayerOptions replacePlayerOptions)
        {
            if (GetNetworkIdentity(player, out NetworkIdentity identity))
                identity.assetId = assetId;

            return ReplacePlayerForConnection(conn, player, replacePlayerOptions);
        }

        /// <summary>Replaces connection's player object. The old object is not destroyed.</summary>
        // This does NOT change the ready state of the connection, so it can safely be used while changing scenes.
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, ReplacePlayerOptions replacePlayerOptions)
        {
            if (!player.TryGetComponent(out NetworkIdentity identity))
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
                identity.isOwned = true;
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

            switch (replacePlayerOptions)
            {
                case ReplacePlayerOptions.KeepAuthority:
                    // This needs to be sent to clear isLocalPlayer on
                    // client while keeping hasAuthority true
                    SendChangeOwnerMessage(previousPlayer, conn);
                    break;
                case ReplacePlayerOptions.KeepActive:
                    // This clears both isLocalPlayer and hasAuthority on client
                    previousPlayer.RemoveClientAuthority();
                    break;
                case ReplacePlayerOptions.Unspawn:
                    UnSpawn(previousPlayer.gameObject);
                    break;
                case ReplacePlayerOptions.Destroy:
                    Destroy(previousPlayer.gameObject);
                    break;
            }

            return true;
        }

        /// <summary>Removes the player object from the connection</summary>
        // destroyServerObject: Indicates whether the server object should be destroyed
        // Deprecated 2024-06-06
        [Obsolete("Use RemovePlayerForConnection(NetworkConnectionToClient conn, RemovePlayerOptions removeOptions) instead")]
        public static void RemovePlayerForConnection(NetworkConnectionToClient conn, bool destroyServerObject)
        {
            if (destroyServerObject)
                RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
            else
                RemovePlayerForConnection(conn, RemovePlayerOptions.Unspawn);
        }

        /// <summary>Removes player object for the connection. Options to keep the object in play, unspawn it, or destroy it.</summary>
        public static void RemovePlayerForConnection(NetworkConnectionToClient conn, RemovePlayerOptions removeOptions = RemovePlayerOptions.KeepActive)
        {
            if (conn.identity == null) return;

            switch (removeOptions)
            {
                case RemovePlayerOptions.KeepActive:
                    conn.identity.connectionToClient = null;
                    conn.owned.Remove(conn.identity);
                    SendChangeOwnerMessage(conn.identity, conn);
                    break;
                case RemovePlayerOptions.Unspawn:
                    UnSpawn(conn.identity.gameObject);
                    break;
                case RemovePlayerOptions.Destroy:
                    Destroy(conn.identity.gameObject);
                    break;
            }

            conn.identity = null;
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
                    if (identity.visibility == Visibility.ForceShown)
                    {
                        identity.AddObserver(conn);
                    }
                    // ForceHidden: don't show no matter what
                    else if (identity.visibility == Visibility.ForceHidden)
                    {
                        // do nothing
                    }
                    // default: legacy system / new system / no system support
                    else if (identity.visibility == Visibility.Default)
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

        // spawning ////////////////////////////////////////////////////////////
        internal static void SendSpawnMessage(NetworkIdentity identity, NetworkConnection conn)
        {
            if (identity.serverOnly) return;

            //Debug.Log($"Server SendSpawnMessage: name:{identity.name} sceneId:{identity.sceneId:X} netid:{identity.netId}");

            // one writer for owner, one for observers
            using (NetworkWriterPooled ownerWriter = NetworkWriterPool.Get(), observersWriter = NetworkWriterPool.Get())
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

        static ArraySegment<byte> CreateSpawnMessagePayload(bool isOwner, NetworkIdentity identity, NetworkWriterPooled ownerWriter, NetworkWriterPooled observersWriter)
        {
            // Only call SerializeAll if there are NetworkBehaviours
            if (identity.NetworkBehaviours.Length == 0)
            {
                return default;
            }

            // serialize all components with initialState = true
            // (can be null if has none)
            identity.SerializeServer(true, ownerWriter, observersWriter);

            // convert to ArraySegment to avoid reader allocations
            // if nothing was written, .ToArraySegment returns an empty segment.
            ArraySegment<byte> ownerSegment = ownerWriter.ToArraySegment();
            ArraySegment<byte> observersSegment = observersWriter.ToArraySegment();

            // use owner segment if 'conn' owns this identity, otherwise
            // use observers segment
            ArraySegment<byte> payload = isOwner ? ownerSegment : observersSegment;

            return payload;
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
                isLocalPlayer = (conn.identity == identity && identity.connectionToClient == conn)
            });
        }

        // check NetworkIdentity parent before spawning it.
        // - without parent, they are spawned
        // - with parent, only if the parent is active in hierarchy
        //
        // note that active parents may have inactive parents of their own.
        // we need to check .activeInHierarchy.
        //
        // fixes: https://github.com/MirrorNetworking/Mirror/issues/3330
        //        https://github.com/vis2k/Mirror/issues/2778
        static bool ValidParent(NetworkIdentity identity) =>
            identity.transform.parent == null ||
            identity.transform.parent.gameObject.activeInHierarchy;

        /// <summary>Spawns NetworkIdentities in the scene on the server.</summary>
        // NetworkIdentity objects in a scene are disabled by default. Calling
        // SpawnObjects() causes these scene objects to be enabled and spawned.
        // It is like calling NetworkServer.Spawn() for each of them.
        public static bool SpawnObjects()
        {
            // only if server active
            if (!active)
                return false;

            // find all NetworkIdentities in the scene.
            // all of them are disabled because of NetworkScenePostProcess.
            NetworkIdentity[] identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();

            // first pass: activate all scene objects
            foreach (NetworkIdentity identity in identities)
            {
                // only spawn scene objects which haven't been spawned yet.
                // SpawnObjects may be called multiple times for additive scenes.
                // https://github.com/MirrorNetworking/Mirror/issues/3318
                //
                // note that we even activate objects under inactive parents.
                // while they are not spawned, they do need to be activated
                // in order to be spawned later. so here, we don't check parents.
                // https://github.com/MirrorNetworking/Mirror/issues/3330
                if (Utils.IsSceneObject(identity) && identity.netId == 0)
                {
                    // Debug.Log($"SpawnObjects sceneId:{identity.sceneId:X} name:{identity.gameObject.name}");
                    identity.gameObject.SetActive(true);
                }
            }

            // second pass: spawn all scene objects
            foreach (NetworkIdentity identity in identities)
            {
                // scene objects may be children of inactive parents.
                // users would put them under disabled parents to 'deactivate' them.
                // those should not be used by Mirror at all.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3330
                //        https://github.com/vis2k/Mirror/issues/2778
                if (Utils.IsSceneObject(identity) && identity.netId == 0 && ValidParent(identity))
                {
                    // pass connection so that authority is not lost when server loads a scene
                    // https://github.com/vis2k/Mirror/pull/2987
                    Spawn(identity.gameObject, identity.connectionToClient);
                }
            }

            return true;
        }

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        // This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.
        public static void Spawn(GameObject obj, GameObject ownerPlayer)
        {
            if (!ownerPlayer.TryGetComponent(out NetworkIdentity identity))
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

        /// <summary>Spawn the given game object on all clients which are ready.</summary>
        // This will cause a new object to be instantiated from the registered
        // prefab, or from a custom spawn function.
        public static void Spawn(GameObject obj, NetworkConnection ownerConnection = null)
        {
            SpawnObject(obj, ownerConnection);
        }

        /// <summary>Spawns an object and also assigns Client Authority to the specified client.</summary>
        // This is the same as calling NetworkIdentity.AssignClientAuthority on the spawned object.
        public static void Spawn(GameObject obj, uint assetId, NetworkConnection ownerConnection = null)
        {
            if (GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                identity.assetId = assetId;
            }
            SpawnObject(obj, ownerConnection);
        }

        static void SpawnObject(GameObject obj, NetworkConnection ownerConnection)
        {
            // verify if we can spawn this
            if (Utils.IsPrefab(obj))
            {
                Debug.LogError($"GameObject {obj.name} is a prefab, it can't be spawned. Instantiate it first.", obj);
                return;
            }

            if (!active)
            {
                Debug.LogError($"SpawnObject for {obj}, NetworkServer is not active. Cannot spawn objects without an active server.", obj);
                return;
            }

            if (!obj.TryGetComponent(out NetworkIdentity identity))
            {
                Debug.LogError($"SpawnObject {obj} has no NetworkIdentity. Please add a NetworkIdentity to {obj}", obj);
                return;
            }

            if (identity.SpawnedFromInstantiate)
            {
                // Using Instantiate on SceneObject is not allowed, so stop spawning here
                // NetworkIdentity.Awake already logs error, no need to log a second error here
                return;
            }

            // Spawn should only be called once per netId.
            // calling it twice would lead to undefined behaviour.
            // https://github.com/MirrorNetworking/Mirror/pull/3205
            if (spawned.ContainsKey(identity.netId))
            {
                Debug.LogWarning($"{identity.name} [netId={identity.netId}] was already spawned.", identity.gameObject);
                return;
            }

            identity.connectionToClient = (NetworkConnectionToClient)ownerConnection;

            // special case to make sure hasAuthority is set
            // on start server in host mode
            if (ownerConnection is LocalConnectionToClient)
                identity.isOwned = true;

            // NetworkServer.Unspawn sets object as inactive.
            // NetworkServer.Spawn needs to set them active again in case they were previously unspawned / inactive.
            identity.gameObject.SetActive(true);

            // only call OnStartServer if not spawned yet.
            // check used to be in NetworkIdentity. may not be necessary anymore.
            if (!identity.isServer && identity.netId == 0)
            {
                // configure NetworkIdentity
                // this may be called in host mode, so we need to initialize
                // isLocalPlayer/isClient flags too.
                identity.isLocalPlayer = NetworkClient.localPlayer == identity;
                identity.isClient = NetworkClient.active;
                identity.isServer = true;
                identity.netId = NetworkIdentity.GetNextNetworkId();

                // add to spawned (after assigning netId)
                spawned[identity.netId] = identity;

                // callback after all fields were set
                identity.OnStartServer();
            }

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

        // internal Unspawn function which has the 'resetState' parameter.
        // resetState calls .ResetState() on the object after unspawning.
        // this is necessary for scene objects, but not for prefabs since we
        // don't want to reset their isServer flags etc.
        // fixes: https://github.com/MirrorNetworking/Mirror/issues/3832
        static void UnSpawnInternal(GameObject obj, bool resetState)
        {
            // Debug.Log($"DestroyObject instance:{identity.netId}");

            // NetworkServer.Unspawn should only be called on server or host.
            // on client, show a warning to explain what it does.
            if (!active)
            {
                Debug.LogWarning("NetworkServer.Unspawn() called without an active server. Servers can only destroy while active, clients can only ask the server to destroy (for example, with a [Command]), after which the server may decide to destroy the object and broadcast the change to all clients.");
                return;
            }

            if (obj == null)
            {
                Debug.Log("NetworkServer.Unspawn(): object is null");
                return;
            }

            if (!GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                return;
            }

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
            SendToObservers(identity, new ObjectDestroyMessage
            {
                netId = identity.netId
            });
            identity.ClearObservers();

            // in host mode, call OnStopClient/OnStopLocalPlayer manually
            if (NetworkClient.active && activeHost)
            {
                if (identity.isLocalPlayer)
                    identity.OnStopLocalPlayer();

                identity.OnStopClient();
                // The object may have been spawned with host client ownership,
                // e.g. a pet so we need to clear hasAuthority and call
                // NotifyAuthority which invokes OnStopAuthority if hasAuthority.
                identity.isOwned = false;
                identity.NotifyAuthority();

                // remove from NetworkClient dictionary
                NetworkClient.connection.owned.Remove(identity);
                NetworkClient.spawned.Remove(identity.netId);
            }

            // we are on the server. call OnStopServer.
            identity.OnStopServer();

            // finally reset the state and deactivate it
            if (resetState)
            {
                identity.ResetState();
                identity.gameObject.SetActive(false);
            }
        }

        /// <summary>This takes an object that has been spawned and un-spawns it.</summary>
        // The object will be removed from clients that it was spawned on, or
        // the custom spawn handler function on the client will be called for
        // the object.
        // Unlike when calling NetworkServer.Destroy(), on the server the object
        // will NOT be destroyed. This allows the server to re-use the object,
        // even spawn it again later.
        public static void UnSpawn(GameObject obj) => UnSpawnInternal(obj, resetState: true);

        // destroy /////////////////////////////////////////////////////////////
        /// <summary>Destroys this object and corresponding objects on all clients.</summary>
        // In some cases it is useful to remove an object but not delete it on
        // the server. For that, use NetworkServer.UnSpawn() instead of
        // NetworkServer.Destroy().
        public static void Destroy(GameObject obj)
        {
            // NetworkServer.Destroy should only be called on server or host.
            // on client, show a warning to explain what it does.
            if (!active)
            {
                Debug.LogWarning("NetworkServer.Destroy() called without an active server. Servers can only destroy while active, clients can only ask the server to destroy (for example, with a [Command]), after which the server may decide to destroy the object and broadcast the change to all clients.");
                return;
            }

            if (obj == null)
            {
                Debug.Log("NetworkServer.Destroy(): object is null");
                return;
            }

            // get the NetworkIdentity component first
            if (!GetNetworkIdentity(obj, out NetworkIdentity identity))
            {
                Debug.LogWarning($"NetworkServer.Destroy() called on {obj.name} which doesn't have a NetworkIdentity component.");
                return;
            }

            // is this a scene object?
            // then we simply unspawn & reset it so it can still be spawned again.
            // we never destroy scene objects on server or on client, since once
            // they are gone, they are gone forever and can't be instantiate again.
            // for example, server may Destroy() a scene object and once a match
            // restarts, the scene objects would be gone from the new match.
            if (identity.sceneId != 0)
            {
                UnSpawnInternal(obj, resetState: true);
            }
            // is this a prefab?
            // then we destroy it completely.
            else
            {
                // unspawn without calling ResetState.
                // otherwise isServer/isClient flags might be reset in OnDestroy.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3832
                UnSpawnInternal(obj, resetState: false);
                identity.destroyCalled = true;

                // Destroy if application is running
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(obj);
                }
                // Destroy can't be used in Editor during tests. use DestroyImmediate.
                else
                {
                    GameObject.DestroyImmediate(obj);
                }
            }
        }

        // interest management /////////////////////////////////////////////////
        // Helper function to add all server connections as observers.
        // This is used if none of the components provides their own
        // OnRebuildObservers function.
        // rebuild observers default method (no AOI) - adds all connections
        static void RebuildObserversDefault(NetworkIdentity identity, bool initialize)
        {
            // only add all connections when rebuilding the first time.
            // second time we just keep them without rebuilding anything.
            if (initialize)
            {
                // not force hidden?
                if (identity.visibility != Visibility.ForceHidden)
                {
                    AddAllReadyServerConnectionsToObservers(identity);
                }
                else if (identity.connectionToClient != null)
                {
                    // force hidden, but add owner connection
                    identity.AddObserver(identity.connectionToClient);
                }
            }
        }

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
            // if there is no interest management system,
            // or if 'force shown' then add all connections
            if (aoi == null || identity.visibility == Visibility.ForceShown)
            {
                RebuildObserversDefault(identity, initialize);
            }
            // otherwise let interest management system rebuild
            else
            {
                aoi.Rebuild(identity, initialize);
            }
        }


        // broadcasting ////////////////////////////////////////////////////////
        // helper function to get the right serialization for a connection
        static NetworkWriter SerializeForConnection(NetworkIdentity identity, NetworkConnectionToClient connection)
        {
            // get serialization for this entity (cached)
            // IMPORTANT: int tick avoids floating point inaccuracy over days/weeks
            NetworkIdentitySerialization serialization = identity.GetServerSerializationAtTick(Time.frameCount);

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
            bool hasNull = false;
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
                    NetworkWriter serialization = SerializeForConnection(identity, connection);
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
                else
                {
                    hasNull = true;
                    Debug.LogWarning($"Found 'null' entry in observing list for connectionId={connection.connectionId}. Please call NetworkServer.Destroy to destroy networked objects. Don't use GameObject.Destroy.");
                }
            }

            // recover from null entries.
            // otherwise every broadcast will spam the warning and slow down performance until restart.
            if (hasNull) connection.observing.RemoveWhere(identity => identity == null);
        }

        // helper function to check a connection for inactivity and disconnect if necessary
        // returns true if disconnected
        static bool DisconnectIfInactive(NetworkConnectionToClient connection)
        {
            // check for inactivity
            if (disconnectInactiveConnections &&
                !connection.IsAlive(disconnectInactiveTimeout))
            {
                Debug.LogWarning($"Disconnecting {connection} for inactivity!");
                connection.Disconnect();
                return true;
            }
            return false;
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
                // check for inactivity. disconnects if necessary.
                if (DisconnectIfInactive(connection))
                    continue;

                // has this connection joined the world yet?
                // for each READY connection:
                //   pull in UpdateVarsMessage for each entity it observes
                if (connection.isReady)
                {
                    // send time for snapshot interpolation every sendInterval.
                    // BroadcastToConnection() may not send if nothing is new.
                    //
                    // sent over unreliable.
                    // NetworkTime / Transform both use unreliable.
                    //
                    // make sure Broadcast() is only called every sendInterval,
                    // even if targetFrameRate isn't set in host mode (!)
                    // (done via AccurateInterval)
                    connection.Send(new TimeSnapshotMessage(), Channels.Unreliable);

                    // broadcast world state to this connection
                    BroadcastToConnection(connection);
                }

                // update connection to flush out batched messages
                connection.Update();
            }
        }

        // update //////////////////////////////////////////////////////////////
        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate()
        {
            // measure update time for profiling.
            if (active)
            {
                earlyUpdateDuration.Begin();
                fullUpdateDuration.Begin();
            }

            // process all incoming messages first before updating the world
            if (Transport.active != null)
                Transport.active.ServerEarlyUpdate();

            // step each connection's local time interpolation in early update.
            foreach (NetworkConnectionToClient connection in connections.Values)
                connection.UpdateTimeInterpolation();

            if (active) earlyUpdateDuration.End();
        }

        internal static void NetworkLateUpdate()
        {
            if (active)
            {
                // measure update time for profiling.
                lateUpdateDuration.Begin();

                // only broadcast world if active
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
                // Unity 2019 doesn't have Time.timeAsDouble yet
                bool sendIntervalElapsed = AccurateInterval.Elapsed(NetworkTime.localTime, sendInterval, ref lastSendTime);
                if (!Application.isPlaying || sendIntervalElapsed)
                    Broadcast();
            }

            // process all outgoing messages after updating the world
            // (even if not active. still want to process disconnects etc.)
            if (Transport.active != null)
                Transport.active.ServerLateUpdate();

            // measure actual tick rate every second.
            if (active)
            {
                ++actualTickRateCounter;

                // NetworkTime.localTime has defines for 2019 / 2020 compatibility
                if (NetworkTime.localTime >= actualTickRateStart + 1)
                {
                    // calculate avg by exact elapsed time.
                    // assuming 1s wouldn't be accurate, usually a few more ms passed.
                    float elapsed = (float)(NetworkTime.localTime - actualTickRateStart);
                    actualTickRate = Mathf.RoundToInt(actualTickRateCounter / elapsed);
                    actualTickRateStart = NetworkTime.localTime;
                    actualTickRateCounter = 0;
                }

                // measure total update time. including transport.
                // because in early update, transport update calls handlers.
                lateUpdateDuration.End();
                fullUpdateDuration.End();
            }
        }
    }
}
