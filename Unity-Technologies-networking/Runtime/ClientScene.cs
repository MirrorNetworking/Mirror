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

            NetworkTransport.Shutdown();
            NetworkTransport.Init();
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
            if (s_ReadyConnection == null)
            {
                if (LogFilter.logWarn) { Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer"); }
            }
            else
            {
                s_ReadyConnection.SetPlayerController(newPlayer);
            }
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
            if (readyConn != null)
            {
                s_IsReady = true;
                s_ReadyConnection = readyConn;
            }

            if (!s_IsReady)
            {
                if (LogFilter.logError) { Debug.LogError("Must call AddPlayer() with a connection the first time to become ready."); }
                return false;
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

            var msg = new AddPlayerMessage();
            msg.playerControllerId = playerControllerId;
            if (extraMessage != null)
            {
                var writer = new NetworkWriter();
                extraMessage.Serialize(writer);
                msg.msgData = writer.ToArray();
            }
            s_ReadyConnection.Send((short)MsgType.AddPlayer, msg);
            return true;
        }

        public static bool RemovePlayer(short playerControllerId)
        {
            if (LogFilter.logDebug) { Debug.Log("ClientScene::RemovePlayer() for ID " + playerControllerId + " called with connection [" + s_ReadyConnection + "]"); }

            PlayerController playerController;
            if (s_ReadyConnection.GetPlayerController(playerControllerId, out playerController))
            {
                var msg = new RemovePlayerMessage();
                msg.playerControllerId = playerControllerId;
                s_ReadyConnection.Send((short)MsgType.RemovePlayer, msg);

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
                conn.Send((short)MsgType.Ready, msg);
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
            NetworkServer.ActivateLocalClientScene();
            newClient.InternalConnectLocalServer(true);
            return newClient;
        }

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
                // not spawned yet etc.?
                if (!uv.gameObject.activeSelf &&
                    uv.gameObject.hideFlags != HideFlags.NotEditable && uv.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                    !uv.sceneId.IsEmpty())
                {
                    s_SpawnableObjects[uv.sceneId] = uv;
                    if (LogFilter.logDebug) { Debug.Log("ClientScene::PrepareSpawnObjects sceneId:" + uv.sceneId); }
                }
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
                client.RegisterHandler((short)MsgType.ObjectDestroy, OnLocalClientObjectDestroy);
                client.RegisterHandler((short)MsgType.ObjectHide, OnLocalClientObjectHide);
                client.RegisterHandler((short)MsgType.SpawnPrefab, OnLocalClientSpawnPrefab);
                client.RegisterHandler((short)MsgType.SpawnSceneObject, OnLocalClientSpawnSceneObject);
                client.RegisterHandler((short)MsgType.LocalClientAuthority, OnClientAuthority);
            }
            else
            {
                // LocalClient shares the sim/scene with the server, no need for these events
                client.RegisterHandler((short)MsgType.SpawnPrefab, OnSpawnPrefab);
                client.RegisterHandler((short)MsgType.SpawnSceneObject, OnSpawnSceneObject);
                client.RegisterHandler((short)MsgType.SpawnFinished, OnObjectSpawnFinished);
                client.RegisterHandler((short)MsgType.ObjectDestroy, OnObjectDestroy);
                client.RegisterHandler((short)MsgType.ObjectHide, OnObjectDestroy);
                client.RegisterHandler((short)MsgType.UpdateVars, OnUpdateVarsMessage);
                client.RegisterHandler((short)MsgType.Owner, OnOwnerMessage);
                client.RegisterHandler((short)MsgType.SyncList, OnSyncListMessage);
                client.RegisterHandler((short)MsgType.Animation, NetworkAnimator.OnAnimationClientMessage);
                client.RegisterHandler((short)MsgType.AnimationParameters, NetworkAnimator.OnAnimationParametersClientMessage);
                client.RegisterHandler((short)MsgType.LocalClientAuthority, OnClientAuthority);
            }

            client.RegisterHandler((short)MsgType.Rpc, OnRPCMessage);
            client.RegisterHandler((short)MsgType.SyncEvent, OnSyncEventMessage);
            client.RegisterHandler((short)MsgType.AnimationTrigger, NetworkAnimator.OnAnimationTriggerClientMessage);
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

        static void OnSpawnPrefab(NetworkMessage netMsg)
        {
            SpawnPrefabMessage msg = new SpawnPrefabMessage();
            netMsg.ReadMessage(msg);

            if (!msg.assetId.IsValid())
            {
                if (LogFilter.logError) { Debug.LogError("OnObjSpawn netId: " + msg.netId + " has invalid asset Id"); }
                return;
            }
            if (LogFilter.logDebug) { Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + "]"); }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                (short)MsgType.SpawnPrefab, GetStringForAssetId(msg.assetId), 1);
#endif

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localNetworkIdentity))
            {
                // this object already exists (was in the scene), just apply the update to existing object
                localNetworkIdentity.Reset();
                ApplySpawnPayload(localNetworkIdentity, msg.position, msg.payload, msg.netId, null);
                return;
            }

            GameObject prefab;
            SpawnDelegate handler;
            if (NetworkScene.GetPrefab(msg.assetId, out prefab))
            {
                var obj = (GameObject)Object.Instantiate(prefab, msg.position, msg.rotation);
                if (LogFilter.logDebug)
                {
                    Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    if (LogFilter.logError) { Debug.LogError("Client object spawned for " + msg.assetId + " does not have a NetworkIdentity"); }
                    return;
                }
                localNetworkIdentity.Reset();
                ApplySpawnPayload(localNetworkIdentity, msg.position, msg.payload, msg.netId, obj);
            }
            // lookup registered factory for type:
            else if (NetworkScene.GetSpawnHandler(msg.assetId, out handler))
            {
                GameObject obj = handler(msg.position, msg.assetId);
                if (obj == null)
                {
                    if (LogFilter.logWarn) { Debug.LogWarning("Client spawn handler for " + msg.assetId + " returned null"); }
                    return;
                }
                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    if (LogFilter.logError) { Debug.LogError("Client object spawned for " + msg.assetId + " does not have a network identity"); }
                    return;
                }
                localNetworkIdentity.Reset();
                localNetworkIdentity.SetDynamicAssetId(msg.assetId);
                ApplySpawnPayload(localNetworkIdentity, msg.position, msg.payload, msg.netId, obj);
            }
            else
            {
                if (LogFilter.logError) { Debug.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + msg.assetId + " netId=" + msg.netId); }
            }
        }

        static void OnSpawnSceneObject(NetworkMessage netMsg)
        {
            SpawnSceneObjectMessage msg = new SpawnSceneObjectMessage();
            netMsg.ReadMessage(msg);

            if (LogFilter.logDebug) { Debug.Log("Client spawn scene handler instantiating [netId:" + msg.netId + " sceneId:" + msg.sceneId + " pos:" + msg.position); }


#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                (short)MsgType.SpawnSceneObject, "sceneId", 1);
#endif

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localNetworkIdentity))
            {
                // this object already exists (was in the scene)
                localNetworkIdentity.Reset();
                ApplySpawnPayload(localNetworkIdentity, msg.position, msg.payload, msg.netId, localNetworkIdentity.gameObject);
                return;
            }

            NetworkIdentity spawnedId = SpawnSceneObject(msg.sceneId);
            if (spawnedId == null)
            {
                if (LogFilter.logError)
                {
                    Debug.LogError("Spawn scene object not found for " + msg.sceneId + " SpawnableObjects.Count=" + s_SpawnableObjects.Count);
                    // dump the whole spawnable objects dict for easier debugging
                    foreach (var kvp in s_SpawnableObjects)
                        Debug.Log("Spawnable: SceneId=" + kvp.Key + " name=" + kvp.Value.name);
                }
                return;
            }

            if (LogFilter.logDebug) { Debug.Log("Client spawn for [netId:" + msg.netId + "] [sceneId:" + msg.sceneId + "] obj:" + spawnedId.gameObject.name); }
            spawnedId.Reset();
            ApplySpawnPayload(spawnedId, msg.position, msg.payload, msg.netId, spawnedId.gameObject);
        }

        static void OnObjectSpawnFinished(NetworkMessage netMsg)
        {
            ObjectSpawnFinishedMessage msg = new ObjectSpawnFinishedMessage();
            netMsg.ReadMessage(msg);
            if (LogFilter.logDebug) { Debug.Log("SpawnFinished:" + msg.state); }

            if (msg.state == 0)
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
            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            netMsg.ReadMessage(msg);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnObjDestroy netId:" + msg.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
#if UNITY_EDITOR
                UnityEditor.NetworkDetailStats.IncrementStat(
                    UnityEditor.NetworkDetailStats.NetworkDirection.Incoming,
                    (short)MsgType.ObjectDestroy, GetStringForAssetId(localObject.assetId), 1);
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
                s_NetworkScene.RemoveLocalObject(msg.netId);
                localObject.MarkForReset();
            }
            else
            {
                if (LogFilter.logDebug) { Debug.LogWarning("Did not find target for destroy message for " + msg.netId); }
            }
        }

        static void OnLocalClientObjectDestroy(NetworkMessage netMsg)
        {
            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            netMsg.ReadMessage(msg);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnLocalObjectObjDestroy netId:" + msg.netId); }

            s_NetworkScene.RemoveLocalObject(msg.netId);
        }

        static void OnLocalClientObjectHide(NetworkMessage netMsg)
        {
            ObjectDestroyMessage msg = new ObjectDestroyMessage();
            netMsg.ReadMessage(msg);
            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(false);
            }
        }

        static void OnLocalClientSpawnPrefab(NetworkMessage netMsg)
        {
            SpawnPrefabMessage msg = new SpawnPrefabMessage();
            netMsg.ReadMessage(msg);

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnLocalClientSpawnSceneObject(NetworkMessage netMsg)
        {
            SpawnSceneObjectMessage msg = new SpawnSceneObjectMessage();
            netMsg.ReadMessage(msg);

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnUpdateVarsMessage(NetworkMessage netMsg)
        {
            UpdateVarsMessage message = netMsg.ReadMessage<UpdateVarsMessage>();

            if (LogFilter.logDev) { Debug.Log("ClientScene::OnUpdateVarsMessage " + message.netId + " channel:" + netMsg.channelId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out localObject))
            {
                localObject.OnUpdateVars(new NetworkReader(message.payload), false);
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for sync message for " + message.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message."); }
            }
        }

        static void OnRPCMessage(NetworkMessage netMsg)
        {
            RpcMessage message = netMsg.ReadMessage<RpcMessage>();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnRPCMessage hash:" + message.rpcHash + " netId:" + message.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out uv))
            {
                uv.HandleRPC(message.rpcHash, new NetworkReader(message.payload));
            }
            else
            {
                if (LogFilter.logWarn)
                {
                    string errorRpcName = NetworkBehaviour.GetCmdHashHandlerName(message.rpcHash);
                    Debug.LogWarningFormat("Could not find target object with netId:{0} for RPC call {1}", message.netId, errorRpcName);
                }
            }
        }

        static void OnSyncEventMessage(NetworkMessage netMsg)
        {
            SyncEventMessage message = netMsg.ReadMessage<SyncEventMessage>();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnSyncEventMessage " + message.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out uv))
            {
                uv.HandleSyncEvent(message.eventHash, new NetworkReader(message.payload));
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for SyncEvent message for " + message.netId); }
            }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                (short)MsgType.SyncEvent, NetworkBehaviour.GetCmdHashHandlerName(message.eventHash), 1);
#endif
        }

        static void OnSyncListMessage(NetworkMessage netMsg)
        {
            SyncListMessage message = netMsg.ReadMessage<SyncListMessage>();

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnSyncListMessage " + message.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out uv))
            {
                uv.HandleSyncList(message.syncListHash, new NetworkReader(message.payload));
            }
            else
            {
                if (LogFilter.logWarn) { Debug.LogWarning("Did not find target for SyncList message for " + message.netId); }
            }

#if UNITY_EDITOR
            UnityEditor.NetworkDetailStats.IncrementStat(
                UnityEditor.NetworkDetailStats.NetworkDirection.Outgoing,
                (short)MsgType.SyncList, NetworkBehaviour.GetCmdHashHandlerName(message.syncListHash), 1);
#endif
        }

        static void OnClientAuthority(NetworkMessage netMsg)
        {
            ClientAuthorityMessage msg = new ClientAuthorityMessage();
            netMsg.ReadMessage(msg);

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnClientAuthority for  connectionId=" + netMsg.conn.connectionId + " netId: " + msg.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out uv))
            {
                uv.HandleClientAuthority(msg.authority);
            }
        }

        // OnClientAddedPlayer?
        static void OnOwnerMessage(NetworkMessage netMsg)
        {
            OwnerMessage msg = new OwnerMessage();
            netMsg.ReadMessage(msg);

            if (LogFilter.logDebug) { Debug.Log("ClientScene::OnOwnerMessage - connectionId=" + netMsg.conn.connectionId + " netId: " + msg.netId); }


            // is there already an owner that is a different object??
            PlayerController oldOwner;
            if (netMsg.conn.GetPlayerController(msg.playerControllerId, out oldOwner))
            {
                oldOwner.unetView.SetNotLocalPlayer();
            }

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localNetworkIdentity))
            {
                // this object already exists
                localNetworkIdentity.SetConnectionToServer(netMsg.conn);
                localNetworkIdentity.SetLocalPlayer(msg.playerControllerId);
                InternalAddPlayer(localNetworkIdentity, msg.playerControllerId);
            }
            else
            {
                var pendingOwner = new PendingOwner { netId = msg.netId, playerControllerId = msg.playerControllerId };
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
