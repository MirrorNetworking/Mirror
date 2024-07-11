using System.Collections;
using UnityEngine;

namespace Mirror.Examples.Common
{
    public class Respawn
    {
        public static void RespawnPlayer(bool respawn, byte respawnTime, NetworkConnectionToClient conn)
        {
            // Use the NetworkManager static singleton to start a coroutine
            NetworkManager.singleton.StartCoroutine(DoRespawn(respawn, respawnTime, conn));
        }

        public static IEnumerator DoRespawn(bool respawn, byte respawnTime, NetworkConnectionToClient conn)
        {
            //Debug.Log("DoRespawn started");

            // Wait for SyncVars to Update
            yield return null;

            // Remove Player
            if (!respawn)
            {
                NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
                //Debug.Log("Player destroyed");
                yield break;
            }

            GameObject playerObject = conn.identity.gameObject;
            NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Unspawn);
            //Debug.Log("Player unspawned");

            // Wait for respawn Time
            yield return new WaitForSeconds(respawnTime);

            // Respawn Player - fallback to Vector3.up * 5f to avoid spawning on another player.
            Transform spawnPoint = NetworkManager.singleton.GetStartPosition();
            Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.up * 5f;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            playerObject.transform.SetPositionAndRotation(position, rotation);

            NetworkServer.AddPlayerForConnection(conn, playerObject);
            //Debug.Log("Player respawned");
        }
    }
}