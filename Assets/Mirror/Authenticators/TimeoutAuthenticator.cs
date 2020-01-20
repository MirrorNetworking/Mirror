using System.Collections;
using UnityEngine;

namespace Mirror.Authenticators
{
    /// <summary>
    /// An authenticator that disconnects connections if they don't
    /// authenticate within a specified time limit.
    /// </summary>
    [AddComponentMenu("Network/Authenticators/TimeoutAuthenticator")]
    public class TimeoutAuthenticator : NetworkAuthenticator
    {
        public NetworkAuthenticator authenticator;

        [Range(0, 600), Tooltip("Timeout to auto-disconnect in seconds. Set to 0 for no timeout.")]
        public float timeout = 60;

        public void Awake()
        {
            authenticator.OnClientAuthenticated.AddListener(connection => OnClientAuthenticated.Invoke(connection));
            authenticator.OnServerAuthenticated.AddListener(connection => OnServerAuthenticated.Invoke(connection));
        }

        public override void OnClientAuthenticate(NetworkConnection conn)
        {
            authenticator.OnClientAuthenticate(conn);
            if (timeout > 0)
                StartCoroutine(BeginAuthentication(conn));
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            authenticator.OnServerAuthenticate(conn);
            if (timeout > 0)
                StartCoroutine(BeginAuthentication(conn));
        }

        IEnumerator BeginAuthentication(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log($"Authentication countdown started {conn} {timeout}");

            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
            {
                if (LogFilter.Debug) Debug.Log($"Authentication Timeout {conn}");

                conn.Disconnect();
            }
        }
    }
}
