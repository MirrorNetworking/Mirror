#if ENABLE_UNET
using System;
using System.Collections.Generic;
using UnityEngine.Networking.NetworkSystem;

namespace UnityEngine.Networking
{
    public class ClientScene
    {
        static List<PlayerController> s_LocalPlayers = new List<PlayerController>();
        static NetworkConnection s_ReadyConnection;
        static Dictionary<NetworkSceneId, NetworkIdentity> s_SpawnableObjects;

        static bool s_IsReady;
        static bool s_IsSpawnFinished;
        static NetworkScene s_NetworkScene = new NetworkScene();

        // static message objects to avoid runtime-allocations
        static ObjectSpawnSceneMessage s_ObjectSpawnSceneMessage = new ObjectSpawnSceneMessage();
        static ObjectSpawnFinishedMessage s_ObjectSpawnFinishedMessage = new ObjectSpawnFinishedMessage();
        static ObjectDestroyMessage s_ObjectDestroyMessage = new ObjectDestroyMessage();
        static ObjectSpawnMessage s_ObjectSpawnMessage = new ObjectSpawnMessage();
        static OwnerMessage s_OwnerMessage = new OwnerMessage();
        static ClientAuthorityMessage s_ClientAuthorityMessage = new ClientAuthorityMessage();

#if ENABLE_UNET_HOST_MIGRATION

        public const int ReconnectIdInvalid = -1;
        public const int ReconnectIdHost = 0;
        static int s_ReconnectId = ReconnectIdInvalid;
        static PeerInfoMessage[] s_Peers;
        static bool hasMigrationPending() { return s_ReconnectId != ReconnectIdInvalid; }

        static public void SetReconnectId(int newReconnectId, PeerInfoMessage[] peers)
        {
            s_ReconnectId = newReconnectId;
            s_Peers = peers;

            if (LogFilter.logDebug) { Debug.Log("ClientScene::SetReconnectId: " + newReconnectId); }
        }

#endif

        static internal void SetNotReady()
        {
            s_IsReady = false;
        }

        struct PendingOwner
        {
            public NetworkInstanceId netId;
            public short playerControllerId;
        }
        static List<PendingOwner> s_PendingOwnerIds = new List<PendingOwner>();

        public static List<PlayerController> localPlayers { get { return s_LocalPlayers; } }
        public static bool ready { get { return s_IsReady; } }
        public static NetworkConnection readyConnection { get { return s_ReadyConnection; }}

#if ENABLE_UNET_HOST_MIGRATION
        public static int reconnectId { get { return s_ReconnectId; }}
#endif

        //NOTE: spawn handlers, prefabs and local objects now live in NetworkScene
        public static Dictionary<NetworkInstanceId, NetworkIdentity> objects { get { return s_NetworkScene.localObjects; } }
        public static Dictionary<NetworkHash128, GameObject> prefabs { get { return NetworkScene.guidToPrefab; } }
        public static Dictionary<NetworkSceneId, NetworkIdentity> spawnableObjects { get { return s_SpawnableObjects; } }

        internal static void Shutdown()
        {
            s_NetworkScene.Shutdown();
            s_LocalPlayers = new List<PlayerController>();
            s_PendingOwnerIds = new List<PendingOwner>();
            s_SpawnableObjects = null;
            s_ReadyConnection = null;
            s_IsReady = false;
            s_IsSpawnFinished = false;
#if ENABLE_UNET_HOST_MIGRATION
            s_ReconnectId = ReconnectIdInvalid;
#endif
            NetworkTransport.Shutdown();
            NetworkTransport.Init();
        }

        internal static bool GetPlayerController(short playerControllerId, out PlayerController player)
        {
            player = null;
            if (playerControllerId >= localPlayers.Count)
            {
                if (LogFilter.logWarn) { Debug.Log("ClientScene::GetPlayer: no local player found for: " + playerControllerId); }
                return false;
            }

            if (localPlayers[playerControllerId] == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientScene::GetPlayer: local player is null for: " + playerControllerId); }
                return false;
            }
            player = localPlayers[playerControllerId];
            return player.gameObject != null;
        }

        // this is called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity view, short playerControllerId)
        {
            if (LogFilter.logDebug) { Debug.LogWarning("ClientScene::InternalAddPlayer: playerControllerId : " + playerControllerId); }

            if (playerControllerId >= s_LocalPlayers.Count)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientScene::InternalAddPlayer: playerControllerId higher than expected: " + playerControllerId); }
                while (playerControllerId >= s_LocalPlayers.Count)
                {
                    s_LocalPlayers.Add(new PlayerController());
                }
            }

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            var newPlayer = new PlayerController {gameObject = view.gameObject, playerControllerId = playerControllerId, unetView = view};
            s_LocalPlayers[playerControllerId] = newPlayer;
            s_ReadyConnection.SetPlayerController(newPlayer);
        }

        // use this if already ready
        public static bool AddPlayer(short playerControllerId)
        {
            return AddPlayer(null, playerControllerId);
        }

        // use this to implicitly become ready
        public static bool AddPlayer(NetworkConnection readyConn, short playerControllerId)
        {
            return AddPlayer(readyConn, playerControllerId, null);
        }

        // use this to implicitly become ready
        public static bool AddPlayer(NetworkConnection readyConn, short playerControllerId, MessageBase extraMessage)
        {
            if (playerControllerId < 0)
            {
                if (LogFilter.logError) { Debug.LogError("ClientScene::AddPlayer: playerControllerId of " + playerControllerId + " is negative"); }
                return false;
            }
            if (playerControllerId > PlayerController.MaxPlayersPerClient)
            {
                if (LogFilter.logError) { Debug.LogError("ClientScene::AddPlayer: playerControllerId of " + playerControllerId + " is too high, max is " + PlayerController.MaxPlayersPerClient); }
                return false;
            }
            if (playerControllerId > PlayerController.MaxPlayersPerClient / 2)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("ClientScene::AddPlayer: playerControllerId of " + playerControllerId + " is unusually high"); }
            }

            // fill out local players array
            while (playerControllerId >= s_LocalPlayers.Count)
            {
                s_LocalPlayers.Add(new PlayerController());
            }

            // ensure valid ready connection
            if (readyConn == null)
            {
                if (!s_IsReady)
                {
                    if (LogFilter.logError) { Debug.LogError("Must call AddPlayer() with a connection the first time to become ready."); }
                    return false;
                }
            }
            else
            {
                s_IsReady = true;
                s_ReadyConnection = readyConn;
            }

            PlayerController existingPlayerController;
            if (s_ReadyConnection.GetPlayerController(playerControllerId, out existingPlayerController))
            {
                if (existingPlayerController.IsValid && existingPlayerController.gameObject != null)
                {
                    if (LogFilter.logError) { Debug.LogError("ClientScene::AddPlayer: playerControllerId of " + playerControllerId + " already in use."); }
                    return false;
                }
            }

            if (LogFilter.logDebug) { Debug.Log("ClientScene::AddPlayer() for ID " + playerControllerId + " called with connection [" + s_ReadyConnection + "]"); }

#if ENABLE_UNET_HOST_MIGRATION
            if (!hasMigrationPending())
            {
#endif
            var msg = new AddPlayerMessage();
            msg.playerControllerId = playerControllerId;
            if (extraMessage != null)
            {
                var writer = new NetworkWriter();
                extraMessage.Serialize(writer);
                msg.msgData = writer.ToArray();
                msg.msgSize = writer.Position;
            }
            s_ReadyConnection.Send(MsgType.AddPlayer, msg);
#if ENABLE_UNET_HOST_MIGRATION
        }

        else
        {
            return SendReconnectMessage(extraMessage);
        }
#endif
            return true;
        }

#if ENABLE_UNET_HOST_MIGRATION
        public static bool SendReconnectMessage(MessageBase extraMessage)
        {
            if (!hasMigrationPending())
                return false;

            if (LogFilter.logDebug) { Debug.Log("ClientScene::AddPlayer reconnect " + s_ReconnectId);           }

            if (s_Peers == null)
            {
                SetReconnectId(ReconnectIdInvalid, null);
                if (LogFilter.logError)
                {
                    Debug.LogError("ClientScene::AddPlayer: reconnecting, but no peers.");
                }
                return false;
            }

            // reconnect all the players
            for (int i = 0; i < s_Peers.Length; i++)
            {
                var peer = s_Peers[i];
                if (peer.playerIds == null)
                {
                    // this could be empty if this peer had no players
                    continue;
                }
                if (peer.connectionId == s_ReconnectId)
                {
                    for (int pid = 0; pid < peer.playerIds.Length; pid++)
                    {
                        var msg = new ReconnectMessage();
                        msg.oldConnectionId = s_ReconnectId;
                        msg.netId = peer.playerIds[pid].netId;
                        msg.playerControllerId = peer.playerIds[pid].playerControllerId;
                        if (extraMessage != null)
                        {
                            var writer = new NetworkWriter();
                            extraMessage.Serialize(writer);
                            msg.msgData = writer.ToArray();
                            msg.msgSize = writer.Position;
                        }

                        s_ReadyConnection.Send(MsgType.ReconnectPlayer, msg);
                    }
                }
            }
            // this should only be done once.
            SetReconnectId(ReconnectIdInvalid, null);
            return true;
        }

#endif

        public static bool RemovePlayer(short playerControllerId)
        {
            if (LogFilter.logDebug) { Debug.Log("ClientScene::RemovePlayer() for ID " + playerControllerId + " called with connection [" + s_ReadyConnection + "]"); }

            PlayerController playerController;
            if (s_ReadyConnection.GetPlayerController(playerControllerId, out playerController))
            {
                var msg = new RemovePlayerMessage();
                msg.playerControllerId = playerControllerId;
                s_ReadyConnection.Send(MsgType.RemovePlayer, msg);

                s_ReadyConnection.RemovePlayerController(playerControllerId);
                s_LocalPlayers[playerControllerId] = new PlayerController();

                Object.Destroy(playerController.gameObject);
                return true;
            }
            if (LogFilter.logError) { Debug.LogError("Failed to find player ID " + playerControllerId); }
            return false;
        }

        public static bool Ready(NetworkConnection conn)
        {
            if (s_IsReady)
            {
                if (LogFilter.logError) { Debug.LogError("A connection has already been set as ready. There can only be one."); }
                return false;
            }

            if (LogFilter.logDebug) { Debug.Log("ClientScene::Ready() called with connection [" + conn + "]"); }

            if (conn != null)
            {
                var msg = new ReadyMessage();
                conn.Send(MsgType.Ready, msg);
                s_IsReady = true;
                s_ReadyConnection = conn;
                s_ReadyConnection.isReady = true;
                return true;
            }
            if (LogFilter.logError) { Debug.LogError("Ready() called with invalid connection object: conn=null"); }
            return false;
        }

        static public NetworkClient ConnectLocalServer()
        {
            var newClient = new LocalClient();
            NetworkServer.instance.ActivateLocalClientScene();
            newClient.InternalConnectLocalServer(true);
            return newClient;
        }

#if ENABLE_UNET_HOST_MIGRATION
        static internal NetworkClient ReconnectLocalServer()
        {
            LocalClient newClient = new LocalClient();
            NetworkServer.instance.ActivateLocalClientScene();
            newClient.InternalConnectLocalServer(false);
            return newClient;
        }

        static internal void ClearLocalPlayers()
        {
            s_LocalPlayers.Clear();
        }

#endif
        static internal void HandleClientDisconnect(NetworkConnection conn)
        {
            if (s_ReadyConnection == conn && s_IsReady)
            {
                s_IsReady = false;
                s_ReadyConnection = null;
            }
        }

        internal static void PrepareToSpawnSceneObjects()
        {
            //NOTE: what is there are already objects in this dict?! should we merge with them?
            s_SpawnableObjects = new Dictionary<NetworkSceneId, NetworkIdentity>();
            var uvs = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            for (int i = 0; i < uvs.Length; i++)
            {
                var uv = uvs[i];
                if (uv.gameObject.activeSelf)
                {
                    // already active, cannot spawn it
                    continue;
                }

                if (uv.gameObject.hideFlags == HideFlags.NotEditable || uv.gameObject.hideFlags == HideFlags.HideAndDontSave)
                    continue;

                if (uv.sceneId.IsEmpty())
                    continue;

                s_SpawnableObjects[uv.sceneId] = uv;

                if (LogFilter.logDebug) { Debug.Log("ClientScene::PrepareSpawnObjects sceneId:" + uv.sceneId); }
            }
        }

        internal static NetworkIdentity SpawnSceneObject(NetworkSceneId sceneId)
        {
            if (s_SpawnableObjects.ContainsKey(sceneId))
            {
                NetworkIdentity foundId = s_SpawnableObjects[sceneId];
                s_SpawnableObjects.Remove(sceneId);
                return foundId;
            }
            return null;
        }

        static internal void RegisterSystemHandlers(NetworkClient client, bool localClient)
        {
            if (localClient)
            {
                client.RegisterHandlerSafe(MsgType.ObjectDestroy, OnLocalClientObjectDestroy);
                client.RegisterHandlerSafe(MsgType.ObjectHide, OnLocalClientObjectHide);
                client.RegisterHandlerSafe(MsgType.ObjectSpawn, OnLocalClientObjectSpawn);
                client.RegisterHandlerSafe(MsgType.ObjectSpawnScene, OnLocalClientObjectSpawnScene);
                client.RegisterHandlerSafe(MsgType.LocalClientAuthority, OnClientAuthority);
            }
            else
            {
                // LocalClient shares the sim/scene with the server, no need for these events
                client.RegisterHandlerSafe(MsgType.ObjectSpawn, OnObjectSpawn);
                client.RegisterHandlerSafe(MsgType.ObjectSpawnScene, OnObjectSpawnScene);
                client.RegisterHandlerSafe(MsgType.SpawnFinished, OnObjectSpawnFinished);
                client.RegisterHandlerSafe(MsgType.ObjectDestroy, OnObjectDestroy);
                client.RegisterHandlerSafe(MsgType.ObjectHide, OnObjectDestroy);
                client.RegisterHandlerSafe(MsgType.UpdateVars, OnUpdateVarsMessage);
                client.RegisterHandlerSafe(MsgType.Owner, OnOwnerMessage);
                client.RegisterHandlerSafe(MsgType.SyncList, OnSyncListMessage);
                client.RegisterHandlerSafe(MsgType.Animation, NetworkAnimator.OnAnimationClientMessage);
                client.RegisterHandlerSafe(MsgType.AnimationParameters, NetworkAnimator.OnAnimationParametersClientMessage);
                client.RegisterHandlerSafe(MsgType.LocalClientAuthority, OnClientAuthority);
            }

            client.RegisterHandlerSafe(MsgType.Rpc, OnRPCMessage);
            client.RegisterHandlerSafe(MsgType.SyncEvent, OnSyncEventMessage);
            client.RegisterHandlerSafe(MsgType.AnimationTrigger, NetworkAnimator.OnAnimationTriggerClientMessage);
        }

        // ------------------------ NetworkScene pass-throughs ---------------------

        static internal string GetStringForAssetId(NetworkHash128 assetId)
        {
            GameObject prefab;
            if (NetworkScene.GetPrefab(assetId, out prefab))
            {
                return prefab.name;
            }

            SpawnDelegate handler;
            if (NetworkScene.GetSpawnHandler(assetId, out handler))
            {
                return handler.GetMethodName();
            }

            return "unknown";
        }

        // this assigns the newAssetId to the prefab. This is for registering dynamically created game objects for already know assetIds.
        static public void RegisterPrefab(GameObject prefab, NetworkHash128 newAssetId)
        {
            NetworkScene.RegisterPrefab(prefab, newAssetId);
        }

        static public void RegisterPrefab(GameObject prefab)
        {
            NetworkScene.RegisterPrefab(prefab);
        }

        static public void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkScene.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
        }

        static public void UnregisterPrefab(GameObject prefab)
        {
            NetworkScene.UnregisterPrefab(prefab);
        }

        static public void RegisterSpawnHandler(NetworkHash128 assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkScene.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        static public void UnregisterSpawnHandler(NetworkHash128 assetId)
        {
            NetworkScene.UnregisterSpawnHandler(assetId);
        }

        static public void ClearSpawners()
        {
            NetworkScene.ClearSpawners();
        }

        static public void DestroyAllClientObjects()
        {
            s_NetworkScene.DestroyAllClientObjects();
        }

        static public void SetLocalObject(NetworkInstanceId netId, GameObject obj)
        {
            // if still receiving initial state, dont set isClient
            s_NetworkScene.SetLocalObject(netId, obj, s_IsSpawnFinished, false);
        }

        static public GameObject FindLocalObject(NetworkInstanceId netId)
        {
            return s_NetworkScene.FindLocalObject(netId);
        }

        static void ApplySpawnPayload(NetworkIdentity uv, Vector3 position, byte[] payload, NetworkInstanceId netId, GameObject newGameObject)
        {
            if (!uv.gameObject.activeSelf)
            {
                uv.gameObject.SetActive(true);
            }
            uv.transform.position = position;
            if (payload != null && payload.Length > 0)
            {
                var payloadReader = new NetworkReader(payload);
                uv.OnUpdateVars(payloadReader, true);
            }
            if (newGameObject == null)
            {
                return;
            }

            newGameObject.SetActive(true);
            uv.SetNetworkInstanceId(netId);
            SetLocalObject(netId, newGameObject);

            // objects spawned as part of initial state are started on a second pass
            if (s_IsSpawnFinished)
            {
                uv.OnStartClient();
                CheckForOwner(uv);
            }
        }

        static void OnObjectSpawn(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectSpawnMessage);

            if (!s_ObjectSpawnMessage.assetId.IsValid())
            {
                if (LogFilter.logError) { Debug.LogError("OnObjSpawn netId: " + s_ObjectSpawnMessage.netId + " has invalid asset Id"); }
                return;
            }
            if (LogFilter.logDebug) { Debug.Log("Client spawn handler instantiating [netId:" + s_ObjectSpawnMessage.netId + " asset ID:" + s_ObjectSpawnMessage.assetId + " pos:" + s_ObjectSpawnMessage.position + "]"); }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                MsgType.ObjectSpawn, GetStringForAssetId(s_ObjectSpawnMessage.assetId), 1);
#endif

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectSpawnMessage.netId, out localNetworkIdentity))
            {
                // this object already exists (was in the scene), just apply the update to existing object
                ApplySpawnPayload(localNetworkIdentity, s_ObjectSpawnMessage.position, s_ObjectSpawnMessage.payload, s_ObjectSpawnMessage.netId, null);
                return;
            }

            GameObject prefab;
            SpawnDelegate handler;
            if (NetworkScene.GetPrefab(s_ObjectSpawnMessage.assetId, out prefab))
            {
                var obj = (GameObject)Object.Instantiate(prefab, s_ObjectSpawnMessage.position, s_ObjectSpawnMessage.rotation);
                if (LogFilter.logDebug)
                {
                    Debug.Log("Client spawn handler instantiating [netId:" + s_ObjectSpawnMessage.netId + " asset ID:" + s_ObjectSpawnMessage.assetId + " pos:" + s_ObjectSpawnMessage.position + " rotation: " + s_ObjectSpawnMessage.rotation + "]");
                }

                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    if (LogFilter.logError) { Debug.LogError("Client object spawned for " + s_ObjectSpawnMessage.assetId + " does not have a NetworkIdentity"); }
                    return;
                }
                localNetworkIdentity.Reset();
                ApplySpawnPayload(localNetworkIdentity, s_ObjectSpawnMessage.position, s_ObjectSpawnMessage.payload, s_ObjectSpawnMessage.netId, obj);
            }
            // lookup registered factory for type:
            else if (NetworkScene.GetSpawnHandler(s_ObjectSpawnMessage.assetId, out handler))
            {
                GameObject obj = handler(s_ObjectSpawnMessage.position, s_ObjectSpawnMessage.assetId);
                if (obj == null)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("Client spawn handler for " + s_ObjectSpawnMessage.assetId + " returned null"); }
                    return;
                }
                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    if (LogFilter.logError) { Debug.LogError("Client object spawned for " + s_ObjectSpawnMessage.assetId + " does not have a network identity"); }
                    return;
                }
                localNetworkIdentity.Reset();
                localNetworkIdentity.SetDynamicAssetId(s_ObjectSpawnMessage.assetId);
                ApplySpawnPayload(localNetworkIdentity, s_ObjectSpawnMessage.position, s_ObjectSpawnMessage.payload, s_ObjectSpawnMessage.netId, obj);
            }
            else
            {
                if (LogFilter.logError) { Debug.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + s_ObjectSpawnMessage.assetId + " netId=" + s_ObjectSpawnMessage.netId); }
            }
        }

        static void OnObjectSpawnScene(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectSpawnSceneMessage);

            if (LogFilter.logDebug) { Debug.Log("Client spawn scene handler instantiating [netId:" + s_ObjectSpawnSceneMessage.netId + " sceneId:" + s_ObjectSpawnSceneMessage.sceneId + " pos:" + s_ObjectSpawnSceneMessage.position); }


#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                MsgType.ObjectSpawnScene, "sceneId", 1);
#endif

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectSpawnSceneMessage.netId, out localNetworkIdentity))
            {
                // this object already exists (was in the scene)
                ApplySpawnPayload(localNetworkIdentity, s_ObjectSpawnSceneMessage.position, s_ObjectSpawnSceneMessage.payload, s_ObjectSpawnSceneMessage.netId, localNetworkIdentity.gameObject);
                return;
            }

            NetworkIdentity spawnedId = SpawnSceneObject(s_ObjectSpawnSceneMessage.sceneId);
            if (spawnedId == null)
            {
                if (LogFilter.logError)
                {
                    Debug.LogError("Spawn scene object not found for " + s_ObjectSpawnSceneMessage.sceneId + " SpawnableObjects.Count=" + s_SpawnableObjects.Count);
                    // dump the whole spawnable objects dict for easier debugging
                    foreach (var kvp in s_SpawnableObjects)
                        Debug.Log("Spawnable: SceneId=" + kvp.Key + " name=" + kvp.Value.name);
                }
                return;
            }

            if (LogFilter.logDebug) { Debug.Log("Client spawn for [netId:" + s_ObjectSpawnSceneMessage.netId + "] [sceneId:" + s_ObjectSpawnSceneMessage.sceneId + "] obj:" + spawnedId.gameObject.name); }
            ApplySpawnPayload(spawnedId, s_ObjectSpawnSceneMessage.position, s_ObjectSpawnSceneMessage.payload, s_ObjectSpawnSceneMessage.netId, spawnedId.gameObject);
        }

        static void OnObjectSpawnFinished(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectSpawnFinishedMessage);
            if (LogFilter.logDebug) { Debug.Log("SpawnFinished:" + s_ObjectSpawnFinishedMessage.state); }

            if (s_ObjectSpawnFinishedMessage.state == 0)
            {
                PrepareToSpawnSceneObjects();
                s_IsSpawnFinished = false;
                return;
            }

            foreach (var uv in objects.Values)
            {
                if (!uv.isClient)
                {
                    uv.OnStartClient();
                    CheckForOwner(uv);
                }
            }
            s_IsSpawnFinished = true;
        }

        static void OnObjectDestroy(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectDestroyMessage);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnObjDestroy netId:" + s_ObjectDestroyMessage.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectDestroyMessage.netId, out localObject))
            {
#if UNITY_EDITOR
                UnityEditor.NetworkDetailStats.IncrementStat(
                    UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                    MsgType.ObjectDestroy, GetStringForAssetId(localObject.assetId), 1);
#endif
                localObject.OnNetworkDestroy();

                if (!NetworkScene.InvokeUnSpawnHandler(localObject.assetId, localObject.gameObject))
                {
                    // default handling
                    if (localObject.sceneId.IsEmpty())
                    {
                        Object.Destroy(localObject.gameObject);
                    }
                    else
                    {
                        // scene object.. disable it in scene instead of destroying
                        localObject.gameObject.SetActive(false);
                        s_SpawnableObjects[localObject.sceneId] = localObject;
                    }
                }
                s_NetworkScene.RemoveLocalObject(s_ObjectDestroyMessage.netId);
                localObject.MarkForReset();
            }
            else
            {
                if (LogFilter.logDebug) { Debug.LogWarning("Did not find target for destroy message for " + s_ObjectDestroyMessage.netId); }
            }
        }

        static void OnLocalClientObjectDestroy(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectDestroyMessage);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnLocalObjectObjDestroy netId:" + s_ObjectDestroyMessage.netId); }

            s_NetworkScene.RemoveLocalObject(s_ObjectDestroyMessage.netId);
        }

        static void OnLocalClientObjectHide(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectDestroyMessage);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + s_ObjectDestroyMessage.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectDestroyMessage.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(false);
            }
        }

        static void OnLocalClientObjectSpawn(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectSpawnMessage);
            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectSpawnMessage.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnLocalClientObjectSpawnScene(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ObjectSpawnSceneMessage);
            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(s_ObjectSpawnSceneMessage.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnUpdateVarsMessage(NetworkMessage netMsg)
        {
            NetworkInstanceId netId = netMsg.reader.ReadNetworkId();
            if (LogFilter.logDev) { Debug.Log("ClientScene::OnUpdateVarsMessage " + netId + " channel:" + netMsg.channelId); }


            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(netId, out localObject))
            {
                localObject.OnUpdateVars(netMsg.reader, false);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for sync message for " + netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message."); }
            }
        }

        static void OnRPCMessage(NetworkMessage netMsg)
        {
            var cmdHash = (int)netMsg.reader.ReadPackedUInt32();
            var netId = netMsg.reader.ReadNetworkId();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnRPCMessage hash:" + cmdHash + " netId:" + netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(netId, out uv))
            {
                uv.HandleRPC(cmdHash, netMsg.reader);
            }
            else
            {
                if (LogFilter.logWarn)
                {
                    string errorCmdName = NetworkBehaviour.GetCmdHashHandlerName(cmdHash);
                    Debug.LogWarningFormat("Could not find target object with netId:{0} for RPC call {1}", netId, errorCmdName);
                }
            }
        }

        static void OnSyncEventMessage(NetworkMessage netMsg)
        {
            var cmdHash = (int)netMsg.reader.ReadPackedUInt32();
            var netId = netMsg.reader.ReadNetworkId();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnSyncEventMessage " + netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(netId, out uv))
            {
                uv.HandleSyncEvent(cmdHash, netMsg.reader);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for SyncEvent message for " + netId); }
            }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.SyncEvent, NetworkBehaviour.GetCmdHashHandlerName(cmdHash), 1);
#endif
        }

        static void OnSyncListMessage(NetworkMessage netMsg)
        {
            var netId = netMsg.reader.ReadNetworkId();
            var cmdHash = (int)netMsg.reader.ReadPackedUInt32();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnSyncListMessage " + netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(netId, out uv))
            {
                uv.HandleSyncList(cmdHash, netMsg.reader);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for SyncList message for " + netId); }
            }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                MsgType.SyncList, NetworkBehaviour.GetCmdHashHandlerName(cmdHash), 1);
#endif
        }

        static void OnClientAuthority(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_ClientAuthorityMessage);

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnClientAuthority for  connectionId=" + netMsg.conn.connectionId + " netId: " + s_ClientAuthorityMessage.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(s_ClientAuthorityMessage.netId, out uv))
            {
                uv.HandleClientAuthority(s_ClientAuthorityMessage.authority);
            }
        }

        // OnClientAddedPlayer?
        static void OnOwnerMessage(NetworkMessage netMsg)
        {
            netMsg.ReadMessage(s_OwnerMessage);

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnOwnerMessage - connectionId=" + netMsg.conn.connectionId + " netId: " + s_OwnerMessage.netId); }


            // is there already an owner that is a different object??
            PlayerController oldOwner;
            if (netMsg.conn.GetPlayerController(s_OwnerMessage.playerControllerId, out oldOwner))
            {
                oldOwner.unetView.SetNotLocalPlayer();
            }

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(s_OwnerMessage.netId, out localNetworkIdentity))
            {
                // this object already exists
                localNetworkIdentity.SetConnectionToServer(netMsg.conn);
                localNetworkIdentity.SetLocalPlayer(s_OwnerMessage.playerControllerId);
                InternalAddPlayer(localNetworkIdentity, s_OwnerMessage.playerControllerId);
            }
            else
            {
                var pendingOwner = new PendingOwner { netId = s_OwnerMessage.netId, playerControllerId = s_OwnerMessage.playerControllerId };
                s_PendingOwnerIds.Add(pendingOwner);
            }
        }

        static void CheckForOwner(NetworkIdentity uv)
        {
            for (int i = 0; i < s_PendingOwnerIds.Count; i++)
            {
                var pendingOwner = s_PendingOwnerIds[i];

                if (pendingOwner.netId == uv.netId)
                {
                    // found owner, turn into a local player

                    // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                    uv.SetConnectionToServer(s_ReadyConnection);
                    uv.SetLocalPlayer(pendingOwner.playerControllerId);

                    if (LogFilter.logDev) { Debug.Log("ClientScene::OnOwnerMessage - player=" + uv.gameObject.name); }
                    if (s_ReadyConnection.connectionId < 0)
                    {
                        if (LogFilter.logError) { Debug.LogError("Owner message received on a local client."); }
                        return;
                    }
                    InternalAddPlayer(uv, pendingOwner.playerControllerId);

                    s_PendingOwnerIds.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
#endif //ENABLE_UNET
