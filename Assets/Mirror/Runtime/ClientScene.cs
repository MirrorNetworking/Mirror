using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Guid = System.Guid;
using Object = UnityEngine.Object;

namespace Mirror
{
    /// <summary>
    /// A client manager which contains static client information and functions.
    /// <para>This manager contains references to tracked static local objects such as spawner registrations. It also has the default message handlers used by clients when they registered none themselves. The manager handles adding/removing player objects to the game after a client connection has been set as ready.</para>
    /// <para>The ClientScene is a singleton, and it has static convenience methods such as ClientScene.Ready().</para>
    /// <para>The ClientScene is used by the NetworkManager, but it can be used by itself.</para>
    /// <para>As the ClientScene manages player objects on the client, it is where clients request to add players. The NetworkManager does this via the ClientScene automatically when auto-add-players is set, but it can be done through code using the function ClientScene.AddPlayer(). This sends an AddPlayer message to the server and will cause a player object to be created for this client.</para>
    /// <para>Like NetworkServer, the ClientScene understands the concept of the local client. The function ClientScene.ConnectLocalServer() is used to become a host by starting a local client (when a server is already running).</para>
    /// </summary>
    public static class ClientScene
    {
        static bool isSpawnFinished;

        /// <summary>
        /// NetworkIdentity of the localPlayer
        /// </summary>
        public static NetworkIdentity localPlayer { get; private set; }

        /// <summary>
        /// Returns true when a client's connection has been set to ready.
        /// <para>A client that is ready recieves state updates from the server, while a client that is not ready does not. This useful when the state of the game is not normal, such as a scene change or end-of-game.</para>
        /// <para>This is read-only. To change the ready state of a client, use ClientScene.Ready(). The server is able to set the ready state of clients using NetworkServer.SetClientReady(), NetworkServer.SetClientNotReady() and NetworkServer.SetAllClientsNotReady().</para>
        /// <para>This is done when changing scenes so that clients don't receive state update messages during scene loading.</para>
        /// </summary>
        public static bool ready { get; internal set; }

        /// <summary>
        /// The NetworkConnection object that is currently "ready". This is the connection to the server where objects are spawned from.
        /// <para>This connection can be used to send messages to the server. There can only be one ready connection at a time. There can be multiple NetworkClient instances in existence, each with their own NetworkConnections, but there is only one ClientScene instance and corresponding ready connection.</para>
        /// </summary>
        public static NetworkConnection readyConnection { get; private set; }

        /// <summary>
        /// This is a dictionary of the prefabs that are registered on the client with ClientScene.RegisterPrefab().
        /// <para>The key to the dictionary is the prefab asset Id.</para>
        /// </summary>
        public static Dictionary<Guid, GameObject> prefabs = new Dictionary<Guid, GameObject>();

        /// <summary>
        /// This is dictionary of the disabled NetworkIdentity objects in the scene that could be spawned by messages from the server.
        /// <para>The key to the dictionary is the NetworkIdentity sceneId.</para>
        /// </summary>
        public static Dictionary<ulong, NetworkIdentity> spawnableObjects;

        // spawn handlers
        static readonly Dictionary<Guid, SpawnDelegate> spawnHandlers = new Dictionary<Guid, SpawnDelegate>();
        static readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers = new Dictionary<Guid, UnSpawnDelegate>();

        // this is never called, and if we do call it in NetworkClient.Shutdown
        // then the client's player object won't be removed after disconnecting!
        internal static void Shutdown()
        {
            ClearSpawners();
            spawnableObjects = null;
            readyConnection = null;
            ready = false;
            isSpawnFinished = false;
            DestroyAllClientObjects();
        }

        // this is called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity identity)
        {
            if (LogFilter.Debug) Debug.LogWarning("ClientScene.InternalAddPlayer");

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            localPlayer = identity;
            if (readyConnection != null)
            {
                readyConnection.identity = identity;
            }
            else
            {
                Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
            }
        }

        /// <summary>
        /// This adds a player GameObject for this client.
        /// <para>This causes an AddPlayer message to be sent to the server, and NetworkManager.OnServerAddPlayer is called.</para>
        /// </summary>
        /// <returns>True if player was added.</returns>
        public static bool AddPlayer() => AddPlayer(null);

        /// <summary>
        /// This adds a player GameObject for this client. This causes an AddPlayer message to be sent to the server, and NetworkManager.OnServerAddPlayer is called. If an extra message was passed to AddPlayer, then OnServerAddPlayer will be called with a NetworkReader that contains the contents of the message.
        /// </summary>
        /// <param name="readyConn">The connection to become ready for this client.</param>
        /// <returns>True if player was added.</returns>
        public static bool AddPlayer(NetworkConnection readyConn) => AddPlayer(readyConn, null);

        /// <summary>
        /// This adds a player GameObject for this client. This causes an AddPlayer message to be sent to the server, and NetworkManager.OnServerAddPlayer is called. If an extra message was passed to AddPlayer, then OnServerAddPlayer will be called with a NetworkReader that contains the contents of the message.
        /// <para>extraMessage can contain character selection, etc.</para>
        /// </summary>
        /// <param name="readyConn">The connection to become ready for this client.</param>
        /// <param name="extraData">An extra message object that can be passed to the server for this player.</param>
        /// <returns>True if player was added.</returns>
        public static bool AddPlayer(NetworkConnection readyConn, byte[] extraData)
        {
            // ensure valid ready connection
            if (readyConn != null)
            {
                ready = true;
                readyConnection = readyConn;
            }

            if (!ready)
            {
                Debug.LogError("Must call AddPlayer() with a connection the first time to become ready.");
                return false;
            }

            if (readyConnection.identity != null)
            {
                Debug.LogError("ClientScene.AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
                return false;
            }

            if (LogFilter.Debug) Debug.Log("ClientScene.AddPlayer() called with connection [" + readyConnection + "]");

            AddPlayerMessage message = new AddPlayerMessage()
            {
#pragma warning disable CS0618 // Type or member is obsolete
                value = extraData
#pragma warning restore CS0618 // Type or member is obsolete
            };
            readyConnection.Send(message);
            return true;
        }

        /// <summary>
        /// Removes the player from the game.
        /// </summary>
        /// <returns>True if succcessful</returns>
        public static bool RemovePlayer()
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.RemovePlayer() called with connection [" + readyConnection + "]");

            if (readyConnection.identity != null)
            {
                readyConnection.Send(new RemovePlayerMessage());

                Object.Destroy(readyConnection.identity.gameObject);

                readyConnection.identity = null;
                localPlayer = null;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Signal that the client connection is ready to enter the game.
        /// <para>This could be for example when a client enters an ongoing game and has finished loading the current scene. The server should respond to the SYSTEM_READY event with an appropriate handler which instantiates the players object for example.</para>
        /// </summary>
        /// <param name="conn">The client connection which is ready.</param>
        /// <returns>True if succcessful</returns>
        public static bool Ready(NetworkConnection conn)
        {
            if (ready)
            {
                Debug.LogError("A connection has already been set as ready. There can only be one.");
                return false;
            }

            if (LogFilter.Debug) Debug.Log("ClientScene.Ready() called with connection [" + conn + "]");

            if (conn != null)
            {
                conn.Send(new ReadyMessage());
                ready = true;
                readyConnection = conn;
                readyConnection.isReady = true;
                return true;
            }
            Debug.LogError("Ready() called with invalid connection object: conn=null");
            return false;
        }

        internal static void HandleClientDisconnect(NetworkConnection conn)
        {
            if (readyConnection == conn && ready)
            {
                ready = false;
                readyConnection = null;
            }
        }

        static bool ConsiderForSpawning(NetworkIdentity identity)
        {
            // not spawned yet, not hidden, etc.?
            return !identity.gameObject.activeSelf &&
                   identity.gameObject.hideFlags != HideFlags.NotEditable &&
                   identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                   identity.sceneId != 0;
        }

        /// <summary>
        /// Call this after loading/unloading a scene in the client after connection to register the spawnable objects
        /// </summary>
        public static void PrepareToSpawnSceneObjects()
        {
            // add all unspawned NetworkIdentities to spawnable objects
            spawnableObjects = Resources.FindObjectsOfTypeAll<NetworkIdentity>()
                               .Where(ConsiderForSpawning)
                               .ToDictionary(identity => identity.sceneId, identity => identity);
        }

        static NetworkIdentity SpawnSceneObject(ulong sceneId)
        {
            if (spawnableObjects.TryGetValue(sceneId, out NetworkIdentity identity))
            {
                spawnableObjects.Remove(sceneId);
                return identity;
            }
            Debug.LogWarning("Could not find scene object with sceneid:" + sceneId.ToString("X"));
            return null;
        }

        /// <summary>
        /// Find the registered prefab for this asset id.
        /// Useful for debuggers
        /// </summary>
        /// <param name="assetId">asset id of the prefab</param>
        /// <param name="prefab">the prefab gameobject</param>
        /// <returns>true if prefab was registered</returns>
        public static bool GetPrefab(Guid assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != Guid.Empty &&
                   prefabs.TryGetValue(assetId, out prefab) && prefab != null;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this prefab. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity)
            {
                identity.assetId = newAssetId;

                if (LogFilter.Debug) Debug.Log("Registering prefab '" + prefab.name + "' as asset:" + identity.assetId);
                prefabs[identity.assetId] = prefab;
            }
            else
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        public static void RegisterPrefab(GameObject prefab)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity)
            {
                if (LogFilter.Debug) Debug.Log("Registering prefab '" + prefab.name + "' as asset:" + identity.assetId);
                prefabs[identity.assetId] = prefab;

                NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
                if (identities.Length > 1)
                {
                    Debug.LogWarning("The prefab '" + prefab.name +
                                     "' has multiple NetworkIdentity components. There can only be one NetworkIdentity on a prefab, and it must be on the root object.");
                }
            }
            else
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (spawnHandler == null || unspawnHandler == null)
            {
                Debug.LogError("RegisterPrefab custom spawn function null for " + identity.assetId);
                return;
            }

            if (identity.assetId == Guid.Empty)
            {
                Debug.LogError("RegisterPrefab game object " + prefab.name + " has no prefab. Use RegisterSpawnHandler() instead?");
                return;
            }

            if (LogFilter.Debug) Debug.Log("Registering custom prefab '" + prefab.name + "' as asset:" + identity.assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[identity.assetId] = spawnHandler;
            unspawnHandlers[identity.assetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.
        /// </summary>
        /// <param name="prefab">The prefab to be removed from registration.</param>
        public static void UnregisterPrefab(GameObject prefab)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }
            spawnHandlers.Remove(identity.assetId);
            unspawnHandlers.Remove(identity.assetId);
        }

        /// <summary>
        /// This is an advanced spawning function that registers a custom assetId with the UNET spawning system.
        /// <para>This can be used to register custom spawning methods for an assetId - instead of the usual method of registering spawning methods for a prefab. This should be used when no prefab exists for the spawned objects - such as when they are constructed dynamically at runtime from configuration data.</para>
        /// </summary>
        /// <param name="assetId">Custom assetId string.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (spawnHandler == null || unspawnHandler == null)
            {
                Debug.LogError("RegisterSpawnHandler custom spawn function null for " + assetId);
                return;
            }

            if (LogFilter.Debug) Debug.Log("RegisterSpawnHandler asset '" + assetId + "' " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn handler function that was registered with ClientScene.RegisterHandler().
        /// </summary>
        /// <param name="assetId">The assetId for the handler to be removed for.</param>
        public static void UnregisterSpawnHandler(Guid assetId)
        {
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        /// <summary>
        /// This clears the registered spawn prefabs and spawn handler functions for this client.
        /// </summary>
        public static void ClearSpawners()
        {
            prefabs.Clear();
            spawnHandlers.Clear();
            unspawnHandlers.Clear();
        }

        static bool InvokeUnSpawnHandler(Guid assetId, GameObject obj)
        {
            if (unspawnHandlers.TryGetValue(assetId, out UnSpawnDelegate handler) && handler != null)
            {
                handler(obj);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys all networked objects on the client.
        /// <para>This can be used to clean up when a network connection is closed.</para>
        /// </summary>
        public static void DestroyAllClientObjects()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                if (identity != null && identity.gameObject != null)
                {
                    if (!InvokeUnSpawnHandler(identity.assetId, identity.gameObject))
                    {
                        if (identity.sceneId == 0)
                        {
                            Object.Destroy(identity.gameObject);
                        }
                        else
                        {
                            identity.MarkForReset();
                            identity.gameObject.SetActive(false);
                        }
                    }
                }
            }
            NetworkIdentity.spawned.Clear();
        }

        /// <summary>
        /// Obsolete: Use <see cref="NetworkIdentity.spawned"/> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkIdentity.spawned[netId] instead.")]
        public static GameObject FindLocalObject(uint netId)
        {
            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity identity))
            {
                return identity.gameObject;
            }
            return null;
        }

        static void ApplySpawnPayload(NetworkIdentity identity, SpawnMessage msg)
        {
            identity.Reset();
            identity.pendingLocalPlayer = msg.isLocalPlayer;
            identity.assetId = msg.assetId;

            if (!identity.gameObject.activeSelf)
            {
                identity.gameObject.SetActive(true);
            }

            // apply local values for VR support
            identity.transform.localPosition = msg.position;
            identity.transform.localRotation = msg.rotation;
            identity.transform.localScale = msg.scale;

            // deserialize components if any payload
            // (Count is 0 if there were no components)
            if (msg.payload.Count > 0)
            {
                NetworkReader payloadReader = new NetworkReader(msg.payload);
                identity.OnUpdateVars(payloadReader, true);
            }

            identity.netId = msg.netId;
            NetworkIdentity.spawned[msg.netId] = identity;

            // objects spawned as part of initial state are started on a second pass
            if (isSpawnFinished)
            {
                identity.OnStartClient();
                CheckForLocalPlayer(identity);
            }
        }

        internal static void OnSpawn(NetworkConnection _, SpawnMessage msg)
        {
            if (msg.assetId == Guid.Empty && msg.sceneId == 0)
            {
                Debug.LogError("OnObjSpawn netId: " + msg.netId + " has invalid asset Id");
                return;
            }
            if (LogFilter.Debug) Debug.Log($"Client spawn handler instantiating netId={msg.netId} assetID={msg.assetId} sceneId={msg.sceneId} pos={msg.position}");

            // is this supposed to be the local player?
            if (msg.isLocalPlayer)
            {
                OnSpawnMessageForLocalPlayer(msg.netId);
            }

            // was the object already spawned?
            NetworkIdentity identity = GetExistingObject(msg.netId);

            if (identity == null)
            {
                identity = msg.sceneId == 0 ? SpawnPrefab(msg) : SpawnSceneObject(msg);
            }

            if (identity == null)
            {
                Debug.LogError($"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
                return;
            }

            ApplySpawnPayload(identity, msg);
        }

        static NetworkIdentity GetExistingObject(uint netid)
        {
            NetworkIdentity.spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        static NetworkIdentity SpawnPrefab(SpawnMessage msg)
        {
            if (GetPrefab(msg.assetId, out GameObject prefab))
            {
                GameObject obj = Object.Instantiate(prefab, msg.position, msg.rotation);
                if (LogFilter.Debug)
                {
                    Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                return obj.GetComponent<NetworkIdentity>();
            }
            if (spawnHandlers.TryGetValue(msg.assetId, out SpawnDelegate handler))
            {
                GameObject obj = handler(msg.position, msg.assetId);
                if (obj == null)
                {
                    Debug.LogWarning("Client spawn handler for " + msg.assetId + " returned null");
                    return null;
                }
                return obj.GetComponent<NetworkIdentity>();
            }
            Debug.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + msg.assetId + " netId=" + msg.netId);
            return null;
        }

        static NetworkIdentity SpawnSceneObject(SpawnMessage msg)
        {
            NetworkIdentity spawnedId = SpawnSceneObject(msg.sceneId);
            if (spawnedId == null)
            {
                Debug.LogError("Spawn scene object not found for " + msg.sceneId.ToString("X") + " SpawnableObjects.Count=" + spawnableObjects.Count);

                // dump the whole spawnable objects dict for easier debugging
                if (LogFilter.Debug)
                {
                    foreach (KeyValuePair<ulong, NetworkIdentity> kvp in spawnableObjects)
                        Debug.Log("Spawnable: SceneId=" + kvp.Key + " name=" + kvp.Value.name);
                }
            }

            if (LogFilter.Debug) Debug.Log("Client spawn for [netId:" + msg.netId + "] [sceneId:" + msg.sceneId + "] obj:" + spawnedId.gameObject.name);
            return spawnedId;
        }

        internal static void OnObjectSpawnStarted(NetworkConnection _, ObjectSpawnStartedMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("SpawnStarted");

            PrepareToSpawnSceneObjects();
            isSpawnFinished = false;
        }

        internal static void OnObjectSpawnFinished(NetworkConnection _, ObjectSpawnFinishedMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("SpawnFinished");

            // paul: Initialize the objects in the same order as they were initialized
            // in the server.   This is important if spawned objects
            // use data from scene objects
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values.OrderBy(uv => uv.netId))
            {
                if (!identity.isClient)
                {
                    identity.OnStartClient();
                    CheckForLocalPlayer(identity);
                }
            }
            isSpawnFinished = true;
        }

        internal static void OnObjectHide(NetworkConnection _, ObjectHideMessage msg)
        {
            DestroyObject(msg.netId);
        }

        internal static void OnObjectDestroy(NetworkConnection _, ObjectDestroyMessage msg)
        {
            DestroyObject(msg.netId);
        }

        static void DestroyObject(uint netId)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnObjDestroy netId:" + netId);

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnNetworkDestroy();

                if (!InvokeUnSpawnHandler(localObject.assetId, localObject.gameObject))
                {
                    // default handling
                    if (localObject.sceneId == 0)
                    {
                        Object.Destroy(localObject.gameObject);
                    }
                    else
                    {
                        // scene object.. disable it in scene instead of destroying
                        localObject.gameObject.SetActive(false);
                        spawnableObjects[localObject.sceneId] = localObject;
                    }
                }
                NetworkIdentity.spawned.Remove(netId);
                localObject.MarkForReset();
            }
            else
            {
                if (LogFilter.Debug) Debug.LogWarning("Did not find target for destroy message for " + netId);
            }
        }

        internal static void OnLocalClientObjectDestroy(NetworkConnection _, ObjectDestroyMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnLocalObjectObjDestroy netId:" + msg.netId);

            NetworkIdentity.spawned.Remove(msg.netId);
        }

        internal static void OnLocalClientObjectHide(NetworkConnection _, ObjectHideMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnSetLocalVisibility(false);
            }
        }

        internal static void OnLocalClientSpawn(NetworkConnection _, SpawnMessage msg)
        {
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        internal static void OnUpdateVarsMessage(NetworkConnection _, UpdateVarsMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnUpdateVarsMessage " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnUpdateVars(new NetworkReader(msg.payload), false);
            }
            else
            {
                Debug.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        internal static void OnRPCMessage(NetworkConnection _, RpcMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                identity.HandleRPC(msg.componentIndex, msg.functionHash, new NetworkReader(msg.payload));
            }
        }

        internal static void OnSyncEventMessage(NetworkConnection _, SyncEventMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnSyncEventMessage " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                identity.HandleSyncEvent(msg.componentIndex, msg.functionHash, new NetworkReader(msg.payload));
            }
            else
            {
                Debug.LogWarning("Did not find target for SyncEvent message for " + msg.netId);
            }
        }

        internal static void OnClientAuthority(NetworkConnection _, ClientAuthorityMessage msg)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnClientAuthority for netId: " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                identity.ForceAuthority(msg.authority);
            }
        }

        // called for the one object in the spawn message which is the local player!
        internal static void OnSpawnMessageForLocalPlayer(uint netId)
        {
            if (LogFilter.Debug) Debug.Log("ClientScene.OnSpawnMessageForLocalPlayer - " + readyConnection + " netId: " + netId);

            // is there already an owner that is a different object??
            if (readyConnection.identity != null)
            {
                readyConnection.identity.SetNotLocalPlayer();
            }

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                // this object already exists
                localObject.connectionToServer = readyConnection;
                localObject.SetLocalPlayer();
                InternalAddPlayer(localObject);
            }
        }

        static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity.pendingLocalPlayer)
            {
                // supposed to be local player, so make it the local player!

                // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = readyConnection;
                identity.SetLocalPlayer();

                if (LogFilter.Debug) Debug.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
                InternalAddPlayer(identity);

                identity.pendingLocalPlayer = false;
            }
        }
    }
}
