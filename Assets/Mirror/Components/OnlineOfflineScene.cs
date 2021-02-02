using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Mirror
{
    public class OnlineOfflineScene : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(OnlineOfflineScene));

        [FormerlySerializedAs("client")]
        public NetworkClient Client;
        [FormerlySerializedAs("server")]
        public NetworkServer Server;

        [Scene]
        [Tooltip("Assign the OnlineScene to load for this zone")]
        public string OnlineScene;

        [Scene]
        [Tooltip("Assign the OfflineScene to load for this zone")]
        public string OfflineScene;

        // Start is called before the first frame update
        void Start()
        {
            if (string.IsNullOrEmpty(OnlineScene))
                throw new MissingReferenceException("OnlineScene missing. Please assign to OnlineOfflineScene component.");

            if (string.IsNullOrEmpty(OfflineScene))
                throw new MissingReferenceException("OfflineScene missing. Please assign to OnlineOfflineScene component.");

            if (Client != null)
            {
                Client.Disconnected.AddListener(OnClientDisconnected);
            }
            if (Server != null)
            {
                Server.Started.AddListener(OnServerStarted);
                Server.Stopped.AddListener(OnServerStopped);
            }
        }

        void OnClientDisconnected()
        {
            SceneManager.LoadSceneAsync(OfflineScene);
        }

        void OnServerStarted()
        {
            SceneManager.LoadSceneAsync(OnlineScene);
        }

        void OnServerStopped()
        {
            SceneManager.LoadSceneAsync(OfflineScene);
        }
    }
}
