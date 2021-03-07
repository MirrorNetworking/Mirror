// moved into NetworkClient on 2021-03-07
using System;
using System.Collections.Generic;
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

        /// <summary> NetworkIdentity of the localPlayer </summary>
        public static NetworkIdentity localPlayer { get; private set; }

        /// <summary>True if client is ready (= joined world).</summary>
        public static bool ready;

        /// <summary>The NetworkConnection object that is currently "ready".</summary>
        // This connection can be used to send messages to the server. There can
        // only be one ClientScene and ready connection at a time.
        // TODO ready ? NetworkClient.connection : null??????
        public static NetworkConnection readyConnection { get; private set; }

        [Obsolete("ClientScene.prefabs was moved to NetworkClient.prefabs")]
        public static Dictionary<Guid, GameObject> prefabs => NetworkClient.prefabs;

        /// <summary>Disabled scene objects that can be spawned again, by sceneId.</summary>
        internal static readonly Dictionary<ulong, NetworkIdentity> spawnableObjects =
            new Dictionary<ulong, NetworkIdentity>();

        // add player //////////////////////////////////////////////////////////
        // called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity identity)
        {
            //Debug.Log("ClientScene.InternalAddPlayer");

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            localPlayer = identity;

            // NOTE: we DONT need to set isClient=true here, because OnStartClient
            // is called before OnStartLocalPlayer, hence it's already set.
            // localPlayer.isClient = true;

            if (readyConnection != null)
            {
                readyConnection.identity = identity;
            }
            else Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
        }

        // Sets localPlayer to null. Should be called when the local player
        // object is destroyed.
        internal static void ClearLocalPlayer()
        {
            //Debug.Log("ClientScene.ClearLocalPlayer");
            localPlayer = null;
        }

        /// <summary>Sends AddPlayer message to the server, indicating that we want to join the world.</summary>
        public static bool AddPlayer(NetworkConnection readyConn)
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

            // Debug.Log("ClientScene.AddPlayer() called with connection [" + readyConnection + "]");
            readyConnection.Send(new AddPlayerMessage());
            return true;
        }

        // ready ///////////////////////////////////////////////////////////////
        /// <summary>Sends Ready message to server, indicating that we loaded the scene, ready to enter the game.</summary>
        // This could be for example when a client enters an ongoing game and
        // has finished loading the current scene. The server should respond to
        // the SYSTEM_READY event with an appropriate handler which instantiates
        // the players object for example.
        public static bool Ready(NetworkConnection conn)
        {
            if (ready)
            {
                Debug.LogError("A connection has already been set as ready. There can only be one.");
                return false;
            }

            // Debug.Log("ClientScene.Ready() called with connection [" + conn + "]");

            if (conn != null)
            {
                // Set these before sending the ReadyMessage, otherwise host client
                // will fail in InternalAddPlayer with null readyConnection.
                ready = true;
                readyConnection = conn;
                readyConnection.isReady = true;

                // Tell server we're ready to have a player object spawned
                conn.Send(new ReadyMessage());
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

        // spawnable prefabs ///////////////////////////////////////////////////
        [Obsolete("ClientScene.GetPrefab was moved to NetworkClient.GetPrefab")]
        public static bool GetPrefab(Guid assetId, out GameObject prefab) => NetworkClient.GetPrefab(assetId, out prefab);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId) => NetworkClient.RegisterPrefab(prefab, newAssetId);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab) => NetworkClient.RegisterPrefab(prefab);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.UnregisterPrefab was moved to NetworkClient.UnregisterPrefab")]
        public static void UnregisterPrefab(GameObject prefab) => NetworkClient.UnregisterPrefab(prefab);

        // spawn handlers //////////////////////////////////////////////////////
        [Obsolete("ClientScene.RegisterSpawnHandler was moved to NetworkClient.RegisterSpawnHandler")]
        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterSpawnHandler was moved to NetworkClient.RegisterSpawnHandler")]
        public static void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.UnregisterSpawnHandler was moved to NetworkClient.UnregisterSpawnHandler")]
        public static void UnregisterSpawnHandler(Guid assetId) => NetworkClient.UnregisterSpawnHandler(assetId);

        [Obsolete("ClientScene.ClearSpawners was moved to NetworkClient.ClearSpawners")]
        public static void ClearSpawners() => NetworkClient.ClearSpawners();

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
                NetworkClient.CheckForLocalPlayer(identity);
            }
        }

        internal static void OnSpawn(SpawnMessage msg)
        {
            // Debug.Log($"Client spawn handler instantiating netId={msg.netId} assetID={msg.assetId} sceneId={msg.sceneId:X} pos={msg.position}");
            if (FindOrSpawnObject(msg, out NetworkIdentity identity))
            {
                ApplySpawnPayload(identity, msg);
            }
        }

        // Finds Existing Object with NetId or spawns a new one using AssetId or sceneId
        internal static bool FindOrSpawnObject(SpawnMessage msg, out NetworkIdentity identity)
        {
            // was the object already spawned?
            identity = GetExistingObject(msg.netId);

            // if found, return early
            if (identity != null)
            {
                return true;
            }

            if (msg.assetId == Guid.Empty && msg.sceneId == 0)
            {
                Debug.LogError($"OnSpawn message with netId '{msg.netId}' has no AssetId or sceneId");
                return false;
            }

            identity = msg.sceneId == 0 ? SpawnPrefab(msg) : SpawnSceneObject(msg);

            if (identity == null)
            {
                Debug.LogError($"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
                return false;
            }

            return true;
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
                //Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                return obj.GetComponent<NetworkIdentity>();
            }
            if (NetworkClient.spawnHandlers.TryGetValue(msg.assetId, out SpawnHandlerDelegate handler))
            {
                GameObject obj = handler(msg);
                if (obj == null)
                {
                    Debug.LogError($"Spawn Handler returned null, Handler assetId '{msg.assetId}'");
                    return null;
                }
                NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    Debug.LogError($"Object Spawned by handler did not have a NetworkIdentity, Handler assetId '{msg.assetId}'");
                    return null;
                }
                return identity;
            }
            Debug.LogError($"Failed to spawn server object, did you forget to add it to the NetworkManager? assetId={msg.assetId} netId={msg.netId}");
            return null;
        }

        static NetworkIdentity SpawnSceneObject(SpawnMessage msg)
        {
            NetworkIdentity identity = GetAndRemoveSceneObject(msg.sceneId);
            if (identity == null)
            {
                Debug.LogError($"Spawn scene object not found for {msg.sceneId:X} SpawnableObjects.Count={spawnableObjects.Count}");

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
                NetworkClient.CheckForLocalPlayer(identity);
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
                        bool wasUnspawned = NetworkClient.InvokeUnSpawnHandler(identity.assetId, identity.gameObject);
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

        // shutdown ////////////////////////////////////////////////////////////
        internal static void Shutdown()
        {
            ClearSpawners();
            spawnableObjects.Clear();
            readyConnection = null;
            ready = false;
            isSpawnFinished = false;
            DestroyAllClientObjects();
        }
    }
}
