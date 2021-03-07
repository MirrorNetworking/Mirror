// moved into NetworkClient on 2021-03-07
using System;
using System.Collections.Generic;
using UnityEngine;
using Guid = System.Guid;

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
        /// <summary> NetworkIdentity of the localPlayer </summary>
        public static NetworkIdentity localPlayer { get; private set; }

        /// <summary>True if client is ready (= joined world).</summary>
        public static bool ready;

        /// <summary>The NetworkConnection object that is currently "ready".</summary>
        // This connection can be used to send messages to the server. There can
        // only be one ClientScene and ready connection at a time.
        // TODO ready ? NetworkClient.connection : null??????
        public static NetworkConnection readyConnection { get; internal set; }

        [Obsolete("ClientScene.prefabs was moved to NetworkClient.prefabs")]
        public static Dictionary<Guid, GameObject> prefabs => NetworkClient.prefabs;

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

        [Obsolete("ClientScene.PrepareToSpawnSceneObjects was moved to NetworkClient.PrepareToSpawnSceneObjects")]
        public static void PrepareToSpawnSceneObjects() => NetworkClient.PrepareToSpawnSceneObjects();

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

        [Obsolete("ClientScene.DestroyAllClientObjects was moved to NetworkClient.DestroyAllClientObjects")]
        public static void DestroyAllClientObjects() => NetworkClient.DestroyAllClientObjects();
    }
}
