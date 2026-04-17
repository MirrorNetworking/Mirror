using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.AdditiveLevels
{
    [RequireComponent(typeof(CapsuleCollider))]
    public class Portal : NetworkBehaviour
    {
        [Scene, Tooltip("Which scene to send player from here")]
        public string destinationScene;

        [Tooltip("Where to spawn player in Destination Scene")]
        public Vector3 startPosition;

        [Tooltip("Reference to child TextMesh label")]
        public TextMesh label; // don't depend on TMPro. 2019 errors.

        [SyncVar(hook = nameof(OnLabelTextChanged))]
        public string labelText;

        public void OnLabelTextChanged(string _, string newValue)
        {
            label.text = labelText;
        }

        protected override void OnValidate()
        {
            if (Application.isPlaying) return;
            base.OnValidate();
            Reset();
        }

        void Reset()
        {
            // Setup the trigger volume to be smaller than the visibile portal
            // so players actually get inside it before triggering the scene change.
            if (TryGetComponent(out CapsuleCollider capsuleCollider))
            {
                capsuleCollider.isTrigger = true;
                capsuleCollider.height = 1f;
                capsuleCollider.radius = 0.2f;
            }
        }

        public override void OnStartServer()
        {
            labelText = Path.GetFileNameWithoutExtension(destinationScene).Replace("MirrorAdditiveLevels", "");

            // Simple Regex to insert spaces before capitals, numbers
            labelText = Regex.Replace(labelText, @"\B[A-Z0-9]+", " $0");

            // Make the trigger volume a bit larger on the server to ensure players
            // reliably trigger it before stopping movement on client.
            if (TryGetComponent(out CapsuleCollider capsuleCollider))
                capsuleCollider.radius += 0.05f;
        }

        public override void OnStartClient()
        {
            if (label.TryGetComponent(out LookAtMainCamera lookAtMainCamera))
                lookAtMainCamera.enabled = true;
        }

        // Note that I have created layers called Player(6) and Portal(7) and set them
        // up in the Physics collision matrix so only Player collides with Portal.
        void OnTriggerEnter(Collider other)
        {
            // Older Unity doesn't like `is not` syntax.
            if (!(other is CapsuleCollider)) return; // ignore CharacterController colliders

            //Debug.Log($"Portal.OnTriggerEnter {other}");
            // tag check in case you didn't set up the layers and matrix as noted above
            if (!other.CompareTag("Player")) return;

            // applies to host client on server and remote clients
            // controller will be re-enabled when player is respawned in new scene.
            if (other.TryGetComponent(out Common.Controllers.Player.PlayerControllerBase playerController))
                playerController.enabled = false;

            if (isServer)
                StartCoroutine(SendPlayerToNewScene(other.gameObject));
        }

        [ServerCallback]
        IEnumerator SendPlayerToNewScene(GameObject player)
        {
            if (!player.TryGetComponent(out NetworkIdentity identity)) yield break;

            NetworkConnectionToClient conn = identity.connectionToClient;
            if (conn == null) yield break;

            // Tell client to unload previous subscene with custom handling (see NetworkManager::OnClientChangeScene).
            conn.Send(new SceneMessage { sceneName = gameObject.scene.path, sceneOperation = SceneOperation.UnloadAdditive, customHandling = true });

            // wait for fader to complete.
            yield return new WaitForSeconds(AdditiveLevelsNetworkManager.singleton.fadeInOut.GetFadeInTime());

            // Remove player after fader has completed. Unspawn keeps it active on server so we can move it.
            NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Unspawn);

            // yield a frame allowing interest management to update
            // and all spawned objects to be destroyed on client
            yield return null;

            // reposition player on server
            player.transform.position = startPosition;

            // Rotate player to face center of scene
            // Player is 2m tall with pivot at 0,1,0 so we need to look at
            // 1m height to not tilt the player down to look at origin
            player.transform.LookAt(Vector3.up);

            // Move player to new subscene.
            SceneManager.MoveGameObjectToScene(player, SceneManager.GetSceneByPath(destinationScene));

            // Tell client to load the new subscene with custom handling (see NetworkManager::OnClientChangeScene).
            conn.Send(new SceneMessage { sceneName = destinationScene, sceneOperation = SceneOperation.LoadAdditive, customHandling = true });

            // Player will be spawned after destination scene is loaded and before fader completes its reveal.
            NetworkServer.AddPlayerForConnection(conn, player);
        }
    }
}
