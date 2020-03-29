using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkRoom
{
    [AddComponentMenu("")]
    public class NetworkRoomPlayerExt : NetworkRoomPlayer
    {
        public override void OnStartClient()
        {
            if (LogFilter.Debug) MirrorLog.DebugLog("OnStartClient {0}", SceneManager.GetActiveScene().path);

            base.OnStartClient();
        }

        public override void OnClientEnterRoom()
        {
            if (LogFilter.Debug) MirrorLog.DebugLog("OnClientEnterRoom {0}", SceneManager.GetActiveScene().path);
        }

        public override void OnClientExitRoom()
        {
            if (LogFilter.Debug) MirrorLog.DebugLog("OnClientExitRoom {0}", SceneManager.GetActiveScene().path);
        }
    }
}
