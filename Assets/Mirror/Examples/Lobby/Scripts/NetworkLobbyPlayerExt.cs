using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkLobby
{
    public class NetworkLobbyPlayerExt : NetworkLobbyPlayer
    {
        public string Name;
        public override void OnStartClient()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnStartClient {0}", SceneManager.GetActiveScene().name);

            base.OnStartClient();
            NetworkLobbyManager lobby = NetworkManager.singleton as NetworkLobbyManager;

            /*
                This demonstrates how to set the parent of the LobbyPlayerPrefab to an arbitrary scene object
                A similar technique would be used if a full canvas layout UI existed and we wanted to show
                something more visual for each player in that layout, such as a name, avatar, etc.

                Note: LobbyPlayer prefab will be marked DontDestroyOnLoad and carried forward to the game scene.
                      Because of this, NetworkLobbyManager must automatically set the parent to null
                      in ServerChangeScene and OnClientChangeScene.
            */

            if (lobby != null && SceneManager.GetActiveScene().name == lobby.LobbyScene)
                gameObject.transform.SetParent(GameObject.Find("Players").transform);
        }

        public override void OnClientEnterLobby()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnClientEnterLobby {0}", SceneManager.GetActiveScene().name);
        }

        public override void OnClientExitLobby()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnClientExitLobby {0}", SceneManager.GetActiveScene().name);
        }
    }
}
