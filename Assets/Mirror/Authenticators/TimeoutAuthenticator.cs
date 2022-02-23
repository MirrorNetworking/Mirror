using System.Collections;
using UnityEngine;

namespace Mirror.Authenticators
{
    /// <summary>
    /// An authenticator that disconnects connections if they don't
    /// authenticate within a specified time limit.
    /// </summary>
    [AddComponentMenu("Network/ Authenticators/Timeout Authenticator")]
    public class TimeoutAuthenticator : NetworkAuthenticator
    {
        public NetworkAuthenticator authenticator;

        [Range(0, 600), Tooltip("Timeout to auto-disconnect in seconds. Set to 0 for no timeout.")]
        public float timeout = 60;

        public void Awake()
        {
            authenticator.OnServerAuthenticated.AddListener(connection => OnServerAuthenticated.Invoke(connection));
            authenticator.OnClientAuthenticated.AddListener(OnClientAuthenticated.Invoke);
        }

        public override void OnStartServer()
        {
            authenticator.OnStartServer();
        }

        public override void OnStopServer()
        {
            authenticator.OnStopServer();
        }

        public override void OnStartClient()
        {
            authenticator.OnStartClient();
        }

        public override void OnStopClient()
        {
            authenticator.OnStopClient();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            authenticator.OnServerAuthenticate(conn);
            if (timeout > 0)
                StartCoroutine(BeginAuthentication(conn));
        }

        public override void OnClientAuthenticate()
        {
            authenticator.OnClientAuthenticate();
            if (timeout > 0)
                StartCoroutine(BeginAuthentication(NetworkClient.connection));
        }

        IEnumerator BeginAuthentication(NetworkConnection conn)
        {
            //Debug.Log($"Authentication countdown started {conn} {timeout}");
            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
            {
                Debug.LogError($"Authentication Timeout - Disconnecting {conn}");
                conn.Disconnect();
            }
        }
    }
}
