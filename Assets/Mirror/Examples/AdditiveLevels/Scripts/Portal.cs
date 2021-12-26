using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.AdditiveLevels
{
    public class Portal : NetworkBehaviour
    {
        [Scene, Tooltip("Which scene to send player from here")]
        public string destinationScene;

        [Tooltip("Where to spawn player in Destination Scene")]
        public Vector3 startPosition;

        [Tooltip("Reference to child TMP label")]
        public TMPro.TextMeshPro label;

        [SyncVar(hook = nameof(OnLabelTextChanged))]
        public string labelText;

        public void OnLabelTextChanged(string _, string newValue)
        {
            label.text = labelText;
        }

        // This is approximately the fade time
        WaitForSeconds waitForFade = new WaitForSeconds(2f);

        public override void OnStartServer()
        {
            labelText = Path.GetFileNameWithoutExtension(destinationScene);

            // Simple Regex to insert spaces before capitals, numbers
            labelText = Regex.Replace(labelText, @"\B[A-Z0-9]+", " $0");
        }

        // Note that I have created layers called Player(8) and Portal(9) and set them
        // up in the Physics collision matrix so only Player collides with Portal.
        void OnTriggerEnter(Collider other)
        {
            // tag check in case you didn't set up the layers and matrix as noted above
            if (!other.CompareTag("Player")) return;

            //Debug.Log($"{System.DateTime.Now:HH:mm:ss:fff} Portal::OnTriggerEnter {gameObject.name} in {gameObject.scene.name}");

            // applies to host client on server and remote clients
            if (other.TryGetComponent<PlayerController>(out PlayerController playerController))
                playerController.enabled = false;

            if (isServer)
                StartCoroutine(SendPlayerToNewScene(other.gameObject));
        }

        [ServerCallback]
        IEnumerator SendPlayerToNewScene(GameObject player)
        {
            if (player.TryGetComponent<NetworkIdentity>(out NetworkIdentity identity))
            {
                NetworkConnectionToClient conn = identity.connectionToClient;
                if (conn == null) yield break;

                // Tell client to unload previous subscene. No custom handling for this.
                conn.Send(new SceneMessage { sceneName = gameObject.scene.path, sceneOperation = SceneOperation.UnloadAdditive, customHandling = true });

                yield return waitForFade;

                //Debug.Log($"SendPlayerToNewScene RemovePlayerForConnection {conn} netId:{conn.identity.netId}");
                NetworkServer.RemovePlayerForConnection(conn, false);

                // reposition player on server and client
                player.transform.position = startPosition;
                player.transform.LookAt(Vector3.up);

                // Move player to new subscene.
                SceneManager.MoveGameObjectToScene(player, SceneManager.GetSceneByPath(destinationScene));

                // Tell client to load the new subscene with custom handling (see NetworkManager::OnClientChangeScene).
                conn.Send(new SceneMessage { sceneName = destinationScene, sceneOperation = SceneOperation.LoadAdditive, customHandling = true });

                //Debug.Log($"SendPlayerToNewScene AddPlayerForConnection {conn} netId:{conn.identity.netId}");
                NetworkServer.AddPlayerForConnection(conn, player);

                // host client would have been disabled by OnTriggerEnter above
                if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent<PlayerController>(out PlayerController playerController))
                    playerController.enabled = true;
            }
        }
    }
}
