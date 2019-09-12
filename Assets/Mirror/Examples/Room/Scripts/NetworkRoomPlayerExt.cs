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
