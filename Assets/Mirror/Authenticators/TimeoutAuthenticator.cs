using System.Collections;
using System.Collections.Generic;
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
        public NetworkAuthenticator Authenticator;

        [Range(0, 600), Tooltip("Timeout to auto-disconnect in seconds. Set to 0 for no timeout.")]
        public float Timeout = 60;

        public void Awake()
        {
            Authenticator.OnClientAuthenticated += HandleClientAuthenticated;
            Authenticator.OnServerAuthenticated += HandleServerAuthenticated;
        }

        private readonly HashSet<NetworkConnection> pendingAuthentication = new HashSet<NetworkConnection>();

        private void HandleServerAuthenticated(NetworkConnection connection)
        {
            pendingAuthentication.Remove(connection);
            base.OnClientAuthenticate(connection);
        }

        private void HandleClientAuthenticated(NetworkConnection connection)
        {
            pendingAuthentication.Remove(connection);
            base.OnServerAuthenticate(connection);
        }

        public override void OnClientAuthenticate(NetworkConnection conn)
        {
            pendingAuthentication.Add(conn);
            Authenticator.OnClientAuthenticate(conn);
            
            if (Timeout > 0)
                StartCoroutine(BeginAuthentication(conn));
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            pendingAuthentication.Add(conn);
            Authenticator.OnServerAuthenticate(conn);
            if (Timeout > 0)
                StartCoroutine(BeginAuthentication(conn));
        }

        IEnumerator BeginAuthentication(NetworkConnection conn)
        {
            if (LogFilter.Debug) Debug.Log($"Authentication countdown started {conn} {Timeout}");

            yield return new WaitForSecondsRealtime(Timeout);

            if (pendingAuthentication.Contains(conn))
            {
                if (LogFilter.Debug) Debug.Log($"Authentication Timeout {conn}");

                pendingAuthentication.Remove(conn);
                conn.Disconnect();
            }
        }
    }
}
