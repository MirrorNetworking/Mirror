using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkRoom
{
    [AddComponentMenu("")]
    public class NetworkRoomPlayerExt : NetworkRoomPlayer
    {
        public override void OnStartClient()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnStartClient {0}", SceneManager.GetActiveScene().name);

            base.OnStartClient();
            NetworkRoomManager room = NetworkManager.singleton as NetworkRoomManager;

            /*
                This demonstrates how to set the parent of the RoomPlayerPrefab to an arbitrary scene object
                A similar technique would be used if a full canvas layout UI existed and we wanted to show
                something more visual for each player in that layout, such as a name, avatar, etc.

                Note: RoomPlayer prefab will be marked DontDestroyOnLoad and carried forward to the game scene.
                      Because of this, NetworkRoomManager must automatically set the parent to null
                      in ServerChangeScene and OnClientChangeScene.
            */

            if (room != null && SceneManager.GetActiveScene().name == room.RoomScene)
                gameObject.transform.SetParent(GameObject.Find("Players").transform);
        }

        public override void OnClientEnterRoom()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnClientEnterRoom {0}", SceneManager.GetActiveScene().name);
        }

        public override void OnClientExitRoom()
        {
            if (LogFilter.Debug) Debug.LogFormat("OnClientExitRoom {0}", SceneManager.GetActiveScene().name);
        }
    }
}
