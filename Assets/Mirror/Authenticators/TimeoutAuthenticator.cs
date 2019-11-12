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
                StartCoroutine(BeginClientAuthentication(conn));
        }

        IEnumerator BeginClientAuthentication(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log($"Authentication countdown started {conn.connectionId} {timeout}");

            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
            {
                if (LogFilter.Debug) Debug.Log($"Authentication Timeout {conn.connectionId}");
                
                conn.Disconnect();
            }
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            authenticator.OnServerAuthenticate(conn);
            if (timeout > 0)
                StartCoroutine(BeginServerAuthentication(conn));
        }

        IEnumerator BeginServerAuthentication(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log($"Authentication countdown started {conn.connectionId} {timeout}");

            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
            {
                if (LogFilter.Debug) Debug.Log($"Authentication Timeout {conn.connectionId}");
                
                conn.Disconnect();
            }
        }
    }
}
