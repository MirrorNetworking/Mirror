using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkRoom
{
    [AddComponentMenu("")]
    public class NetworkRoomPlayerExt : NetworkRoomPlayer
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkRoomPlayerExt));

        public override void OnStartClient()
        {
            if (LogFilter.Debug) logger.LogFormat(LogType.Log, "OnStartClient {0}", SceneManager.GetActiveScene().path);

            base.OnStartClient();
        }

        public override void OnClientEnterRoom()
        {
            if (LogFilter.Debug) logger.LogFormat(LogType.Log, "OnClientEnterRoom {0}", SceneManager.GetActiveScene().path);
        }

        public override void OnClientExitRoom()
        {
            if (LogFilter.Debug) logger.LogFormat(LogType.Log, "OnClientExitRoom {0}", SceneManager.GetActiveScene().path);
        }
    }
}
