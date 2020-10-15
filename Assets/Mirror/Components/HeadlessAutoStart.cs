using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mirror
{
    public class HeadlessAutoStart : MonoBehaviour
    {
        public NetworkServer server;

        /// <summary>
        /// Automatically invoke StartServer()
        /// <para>If the application is a Server Build or run with the -batchMode ServerRpc line arguement, StartServer is automatically invoked.</para>
        /// </summary>
        [Tooltip("Should the server auto-start when the game is started in a headless build?")]
        public bool startOnHeadless = true;

        void Start()
        {
            // headless mode? then start the server
            // can't do this in Awake because Awake is for initialization.
            // some transports might not be ready until Start.
            if (server && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null && startOnHeadless)
            {
                server.ListenAsync().Forget();
            }
        }
    }
}
