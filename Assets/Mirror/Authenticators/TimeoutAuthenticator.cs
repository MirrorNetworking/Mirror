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
        
        public int timeout = 60;

        public void Awake()
        {
            authenticator.OnClientAuthenticated.AddListener(connection => OnClientAuthenticated.Invoke(connection));
            authenticator.OnServerAuthenticated.AddListener(connection => OnServerAuthenticated.Invoke(connection));
        }

        public override void OnClientAuthenticate(NetworkConnection conn)
        {
            StartCoroutine(BeginClientAuthentication(conn));
        }

        private IEnumerator BeginClientAuthentication(NetworkConnection conn)
        {
            authenticator.OnClientAuthenticate(conn);

            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
                conn.Disconnect();
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            StartCoroutine(BeginServerAuthentication(conn));
        }

        private IEnumerator BeginServerAuthentication(NetworkConnection conn)
        {
            authenticator.OnServerAuthenticate(conn);

            yield return new WaitForSecondsRealtime(timeout);

            if (!conn.isAuthenticated)
                conn.Disconnect();
        }
    }
}
