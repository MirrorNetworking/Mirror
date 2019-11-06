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

        [Range(0, short.MaxValue), Tooltip("Timeout to auto-disconnect in seconds. Set to 0 for no timeout.")]
        public short timeout = 60;

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
            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
                conn.Disconnect();
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            authenticator.OnServerAuthenticate(conn);
            if (timeout > 0)
                StartCoroutine(BeginServerAuthentication(conn));
        }

        private IEnumerator BeginServerAuthentication(NetworkConnection conn)
        {
            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
                conn.Disconnect();
        }
    }
}
