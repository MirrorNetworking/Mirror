using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Guid = System.Guid;

namespace Mirror
{
    public static class ClientScene
    {
        static NetworkIdentity s_LocalPlayer;
        static NetworkConnection s_ReadyConnection;
        // scene id to NetworkIdentity
        static Dictionary<uint, NetworkIdentity> s_SpawnableObjects;

        static bool s_IsReady;
        static bool s_IsSpawnFinished;
        static NetworkScene s_NetworkScene = new NetworkScene();

        internal static void SetNotReady()
        {
            s_IsReady = false;
        }

        static List<uint> s_PendingOwnerNetIds = new List<uint>();

        public static NetworkIdentity localPlayer { get { return s_LocalPlayer; } }
        public static bool ready { get { return s_IsReady; } }
        public static NetworkConnection readyConnection { get { return s_ReadyConnection; }}

        //NOTE: spawn handlers, prefabs and local objects now live in NetworkScene
        // objects by net id
        public static Dictionary<uint, NetworkIdentity> objects { get { return s_NetworkScene.localObjects; } }
        public static Dictionary<Guid, GameObject> prefabs { get { return NetworkScene.guidToPrefab; } }
        // scene id to NetworkIdentity
        public static Dictionary<uint, NetworkIdentity> spawnableObjects { get { return s_SpawnableObjects; } }

        internal static void Shutdown()
        {
            s_NetworkScene.Shutdown();
            s_PendingOwnerNetIds = new List<uint>();
            s_SpawnableObjects = null;
            s_ReadyConnection = null;
            s_IsReady = false;
            s_IsSpawnFinished = false;

            Transport.layer.ClientDisconnect();
        }

        // this is called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity view)
        {
            if (LogFilter.Debug) { Debug.LogWarning("ClientScene::InternalAddPlayer"); }

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            s_LocalPlayer = view;
            if (s_ReadyConnection == null)
            {
                Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
            }
            else
            {
                s_ReadyConnection.SetPlayerController(view);
            }
        }

        // use this if already ready
        public static bool AddPlayer()
        {
            return AddPlayer(null);
        }

        // use this to implicitly become ready
        public static bool AddPlayer(NetworkConnection readyConn)
        {
            return AddPlayer(readyConn, null);
        }

        // use this to implicitly become ready
        public static bool AddPlayer(NetworkConnection readyConn, MessageBase extraMessage)
        {
            // ensure valid ready connection
            if (readyConn != null)
            {
                s_IsReady = true;
                s_ReadyConnection = readyConn;
            }

            if (!s_IsReady)
            {
                Debug.LogError("Must call AddPlayer() with a connection the first time to become ready.");
                return false;
            }

            if (s_ReadyConnection.playerController != null)
            {
                Debug.LogError("ClientScene::AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
                return false;
            }

            if (LogFilter.Debug) { Debug.Log("ClientScene::AddPlayer() called with connection [" + s_ReadyConnection + "]"); }

            AddPlayerMessage msg = new AddPlayerMessage();
            if (extraMessage != null)
            {
                NetworkWriter writer = new NetworkWriter();
                extraMessage.Serialize(writer);
                msg.msgData = writer.ToArray();
            }
            s_ReadyConnection.Send((short)MsgType.AddPlayer, msg);
            return true;
        }

        public static bool RemovePlayer()
        {
            if (LogFilter.Debug) { Debug.Log("ClientScene::RemovePlayer() called with connection [" + s_ReadyConnection + "]"); }

            if (s_ReadyConnection.playerController != null)
            {
                RemovePlayerMessage msg = new RemovePlayerMessage();
                s_ReadyConnection.Send((short)MsgType.RemovePlayer, msg);

                s_ReadyConnection.RemovePlayerController();
                s_LocalPlayer = null;

                Object.Destroy(s_ReadyConnection.playerController.gameObject);
                return true;
            }
            return false;
        }

        public static bool Ready(NetworkConnection conn)
        {
            if (s_IsReady)
            {
                Debug.LogError("A connection has already been set as ready. There can only be one.");
                return false;
            }

            if (LogFilter.Debug) { Debug.Log("ClientScene::Ready() called with connection [" + conn + "]"); }

            if (conn != null)
            {
                var msg = new ReadyMessage();
                conn.Send((short)MsgType.Ready, msg);
                s_IsReady = true;
                s_ReadyConnection = conn;
                s_ReadyConnection.isReady = true;
                return true;
            }
            Debug.LogError("Ready() called with invalid connection object: conn=null");
            return false;
        }

        public static NetworkClient ConnectLocalServer()
        {
            var newClient = new LocalClient();
            NetworkServer.ActivateLocalClientScene();
            newClient.InternalConnectLocalServer(true);
            return newClient;
        }

        internal static void HandleClientDisconnect(NetworkConnection conn)
        {
            if (s_ReadyConnection == conn && s_IsReady)
            {
                s_IsReady = false;
                s_ReadyConnection = null;
            }
        }

        internal static bool ConsiderForSpawning(NetworkIdentity networkIdentity)
        {
            // not spawned yet, not hidden, etc.?
            return !networkIdentity.gameObject.activeSelf &&
                   networkIdentity.gameObject.hideFlags != HideFlags.NotEditable &&
                   networkIdentity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                   networkIdentity.sceneId != 0;
        }

        internal static void PrepareToSpawnSceneObjects()
        {
            // add all unspawned NetworkIdentities to spawnable objects
            s_SpawnableObjects = Resources.FindObjectsOfTypeAll<NetworkIdentity>()
                                 .Where(ConsiderForSpawning)
                                 .ToDictionary(uv => uv.sceneId, uv => uv);
        }

        internal static NetworkIdentity SpawnSceneObject(uint sceneId)
        {
            if (s_SpawnableObjects.ContainsKey(sceneId))
            {
                NetworkIdentity foundId = s_SpawnableObjects[sceneId];
                s_SpawnableObjects.Remove(sceneId);
                return foundId;
            }
            else
            {
                Debug.LogWarning("Could not find scene object with sceneid:" + sceneId);
            }
            return null;
        }

        internal static void RegisterSystemHandlers(NetworkClient client, bool localClient)
        {
            if (localClient)
            {
                client.RegisterHandler(MsgType.ObjectDestroy, OnLocalClientObjectDestroy);
                client.RegisterHandler(MsgType.ObjectHide, OnLocalClientObjectHide);
                client.RegisterHandler(MsgType.SpawnPrefab, OnLocalClientSpawnPrefab);
                client.RegisterHandler(MsgType.SpawnSceneObject, OnLocalClientSpawnSceneObject);
                client.RegisterHandler(MsgType.LocalClientAuthority, OnClientAuthority);
            }
            else
            {
                // LocalClient shares the sim/scene with the server, no need for these events
                client.RegisterHandler(MsgType.SpawnPrefab, OnSpawnPrefab);
                client.RegisterHandler(MsgType.SpawnSceneObject, OnSpawnSceneObject);
                client.RegisterHandler(MsgType.SpawnFinished, OnObjectSpawnFinished);
                client.RegisterHandler(MsgType.ObjectDestroy, OnObjectDestroy);
                client.RegisterHandler(MsgType.ObjectHide, OnObjectDestroy);
                client.RegisterHandler(MsgType.UpdateVars, OnUpdateVarsMessage);
                client.RegisterHandler(MsgType.Owner, OnOwnerMessage);
                client.RegisterHandler(MsgType.Animation, NetworkAnimator.OnAnimationClientMessage);
                client.RegisterHandler(MsgType.AnimationParameters, NetworkAnimator.OnAnimationParametersClientMessage);
                client.RegisterHandler(MsgType.LocalClientAuthority, OnClientAuthority);
                client.RegisterHandler(MsgType.Pong, NetworkTime.OnClientPong);
            }

            client.RegisterHandler(MsgType.Rpc, OnRPCMessage);
            client.RegisterHandler(MsgType.SyncEvent, OnSyncEventMessage);
            client.RegisterHandler(MsgType.AnimationTrigger, NetworkAnimator.OnAnimationTriggerClientMessage);
        }

        // ------------------------ NetworkScene pass-throughs ---------------------
        // this assigns the newAssetId to the prefab. This is for registering dynamically created game objects for already know assetIds.
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            NetworkScene.RegisterPrefab(prefab, newAssetId);
        }

        public static void RegisterPrefab(GameObject prefab)
        {
            NetworkScene.RegisterPrefab(prefab);
        }

        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkScene.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
        }

        public static void UnregisterPrefab(GameObject prefab)
        {
            NetworkScene.UnregisterPrefab(prefab);
        }

        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkScene.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);
        }

        public static void UnregisterSpawnHandler(Guid assetId)
        {
            NetworkScene.UnregisterSpawnHandler(assetId);
        }

        public static void ClearSpawners()
        {
            NetworkScene.ClearSpawners();
        }

        public static void DestroyAllClientObjects()
        {
            s_NetworkScene.DestroyAllClientObjects();
        }

        public static void SetLocalObject(uint netId, GameObject obj)
        {
            // if still receiving initial state, dont set isClient
            s_NetworkScene.SetLocalObject(netId, obj, s_IsSpawnFinished, false);
        }

        public static GameObject FindLocalObject(uint netId)
        {
            return s_NetworkScene.FindLocalObject(netId);
        }

        static void ApplySpawnPayload(NetworkIdentity uv, Vector3 position, byte[] payload, uint netId, GameObject newGameObject)
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
            SpawnPrefabMessage msg = netMsg.ReadMessage<SpawnPrefabMessage>();

            if (msg.assetId == Guid.Empty)
            {
                Debug.LogError("OnObjSpawn netId: " + msg.netId + " has invalid asset Id");
                return;
            }
            if (LogFilter.Debug) { Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + "]"); }

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
                GameObject obj = Object.Instantiate(prefab, msg.position, msg.rotation);
                if (LogFilter.Debug)
                {
                    Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    Debug.LogError("Client object spawned for " + msg.assetId + " does not have a NetworkIdentity");
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
                    Debug.LogWarning("Client spawn handler for " + msg.assetId + " returned null");
                    return;
                }
                localNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                if (localNetworkIdentity == null)
                {
                    Debug.LogError("Client object spawned for " + msg.assetId + " does not have a network identity");
                    return;
                }
                localNetworkIdentity.Reset();
                localNetworkIdentity.SetDynamicAssetId(msg.assetId);
                ApplySpawnPayload(localNetworkIdentity, msg.position, msg.payload, msg.netId, obj);
            }
            else
            {
                Debug.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + msg.assetId + " netId=" + msg.netId);
            }
        }

        static void OnSpawnSceneObject(NetworkMessage netMsg)
        {
            SpawnSceneObjectMessage msg = netMsg.ReadMessage<SpawnSceneObjectMessage>();

            if (LogFilter.Debug) { Debug.Log("Client spawn scene handler instantiating [netId:" + msg.netId + " sceneId:" + msg.sceneId + " pos:" + msg.position); }

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
                Debug.LogError("Spawn scene object not found for " + msg.sceneId + " SpawnableObjects.Count=" + s_SpawnableObjects.Count);
                // dump the whole spawnable objects dict for easier debugging
                foreach (var kvp in s_SpawnableObjects)
                    Debug.Log("Spawnable: SceneId=" + kvp.Key + " name=" + kvp.Value.name);
                return;
            }

            if (LogFilter.Debug) { Debug.Log("Client spawn for [netId:" + msg.netId + "] [sceneId:" + msg.sceneId + "] obj:" + spawnedId.gameObject.name); }
            spawnedId.Reset();
            ApplySpawnPayload(spawnedId, msg.position, msg.payload, msg.netId, spawnedId.gameObject);
        }

        static void OnObjectSpawnFinished(NetworkMessage netMsg)
        {
            ObjectSpawnFinishedMessage msg = netMsg.ReadMessage<ObjectSpawnFinishedMessage>();
            if (LogFilter.Debug) { Debug.Log("SpawnFinished:" + msg.state); }

            if (msg.state == 0)
            {
                PrepareToSpawnSceneObjects();
                s_IsSpawnFinished = false;
                return;
            }

            // paul: Initialize the objects in the same order as they were initialized
            // in the server.   This is important if spawned objects
            // use data from scene objects
            foreach (var uv in objects.Values.OrderBy(uv => uv.netId))
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
            ObjectDestroyMessage msg = netMsg.ReadMessage<ObjectDestroyMessage>();
            if (LogFilter.Debug) { Debug.Log("ClientScene::OnObjDestroy netId:" + msg.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnNetworkDestroy();

                if (!NetworkScene.InvokeUnSpawnHandler(localObject.assetId, localObject.gameObject))
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
                        s_SpawnableObjects[localObject.sceneId] = localObject;
                    }
                }
                s_NetworkScene.RemoveLocalObject(msg.netId);
                localObject.MarkForReset();
            }
            else
            {
                if (LogFilter.Debug) { Debug.LogWarning("Did not find target for destroy message for " + msg.netId); }
            }
        }

        static void OnLocalClientObjectDestroy(NetworkMessage netMsg)
        {
            ObjectDestroyMessage msg = netMsg.ReadMessage<ObjectDestroyMessage>();
            if (LogFilter.Debug) { Debug.Log("ClientScene::OnLocalObjectObjDestroy netId:" + msg.netId); }

            s_NetworkScene.RemoveLocalObject(msg.netId);
        }

        static void OnLocalClientObjectHide(NetworkMessage netMsg)
        {
            ObjectDestroyMessage msg = netMsg.ReadMessage<ObjectDestroyMessage>();
            if (LogFilter.Debug) { Debug.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(false);
            }
        }

        static void OnLocalClientSpawnPrefab(NetworkMessage netMsg)
        {
            SpawnPrefabMessage msg = netMsg.ReadMessage<SpawnPrefabMessage>();

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnLocalClientSpawnSceneObject(NetworkMessage netMsg)
        {
            SpawnSceneObjectMessage msg = netMsg.ReadMessage<SpawnSceneObjectMessage>();

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localObject))
            {
                localObject.OnSetLocalVisibility(true);
            }
        }

        static void OnUpdateVarsMessage(NetworkMessage netMsg)
        {
            UpdateVarsMessage message = netMsg.ReadMessage<UpdateVarsMessage>();

            if (LogFilter.Debug) { Debug.Log("ClientScene::OnUpdateVarsMessage " + message.netId); }

            NetworkIdentity localObject;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out localObject))
            {
                localObject.OnUpdateVars(new NetworkReader(message.payload), false);
            }
            else
            {
                Debug.LogWarning("Did not find target for sync message for " + message.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        static void OnRPCMessage(NetworkMessage netMsg)
        {
            RpcMessage message = netMsg.ReadMessage<RpcMessage>();

            if (LogFilter.Debug) { Debug.Log("ClientScene::OnRPCMessage hash:" + message.rpcHash + " netId:" + message.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out uv))
            {
                uv.HandleRPC(message.componentIndex, message.rpcHash, new NetworkReader(message.payload));
            }
            else
            {
                string errorRpcName = NetworkBehaviour.GetCmdHashHandlerName(message.rpcHash);
                Debug.LogWarningFormat("Could not find target object with netId:{0} for RPC call {1}", message.netId, errorRpcName);
            }
        }

        static void OnSyncEventMessage(NetworkMessage netMsg)
        {
            SyncEventMessage message = netMsg.ReadMessage<SyncEventMessage>();

            if (LogFilter.Debug) { Debug.Log("ClientScene::OnSyncEventMessage " + message.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(message.netId, out uv))
            {
                uv.HandleSyncEvent(message.componentIndex, message.eventHash, new NetworkReader(message.payload));
            }
            else
            {
                Debug.LogWarning("Did not find target for SyncEvent message for " + message.netId);
            }
        }

        static void OnClientAuthority(NetworkMessage netMsg)
        {
            ClientAuthorityMessage msg = netMsg.ReadMessage<ClientAuthorityMessage>();

            if (LogFilter.Debug) { Debug.Log("ClientScene::OnClientAuthority for  connectionId=" + netMsg.conn.connectionId + " netId: " + msg.netId); }

            NetworkIdentity uv;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out uv))
            {
                uv.HandleClientAuthority(msg.authority);
            }
        }

        // OnClientAddedPlayer?
        static void OnOwnerMessage(NetworkMessage netMsg)
        {
            OwnerMessage msg = netMsg.ReadMessage<OwnerMessage>();

            if (LogFilter.Debug) { Debug.Log("ClientScene::OnOwnerMessage - connectionId=" + netMsg.conn.connectionId + " netId: " + msg.netId); }

            // is there already an owner that is a different object??
            if (netMsg.conn.playerController != null)
            {
                netMsg.conn.playerController.SetNotLocalPlayer();
            }

            NetworkIdentity localNetworkIdentity;
            if (s_NetworkScene.GetNetworkIdentity(msg.netId, out localNetworkIdentity))
            {
                // this object already exists
                localNetworkIdentity.SetConnectionToServer(netMsg.conn);
                localNetworkIdentity.SetLocalPlayer();
                InternalAddPlayer(localNetworkIdentity);
            }
            else
            {
                s_PendingOwnerNetIds.Add(msg.netId);
            }
        }

        static void CheckForOwner(NetworkIdentity uv)
        {
            for (int i = 0; i < s_PendingOwnerNetIds.Count; i++)
            {
                uint pendingOwnerNetId = s_PendingOwnerNetIds[i];

                if (pendingOwnerNetId == uv.netId)
                {
                    // found owner, turn into a local player

                    // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                    uv.SetConnectionToServer(s_ReadyConnection);
                    uv.SetLocalPlayer();

                    if (LogFilter.Debug) { Debug.Log("ClientScene::OnOwnerMessage - player=" + uv.gameObject.name); }
                    if (s_ReadyConnection.connectionId < 0)
                    {
                        Debug.LogError("Owner message received on a local client.");
                        return;
                    }
                    InternalAddPlayer(uv);

                    s_PendingOwnerNetIds.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
