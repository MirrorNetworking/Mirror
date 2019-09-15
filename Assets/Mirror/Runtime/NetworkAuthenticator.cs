using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    /// <summary>
    /// Unity Event for the NetworkConnection
    /// </summary>
    [Serializable] public class UnityEventNetworkConnection : UnityEvent<NetworkConnection> { }

    /// <summary>
    /// Base class for implementing component-based authentication during the Connect phase
    /// </summary>
    [AddComponentMenu("Network/NetworkAuthenticator")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Concepts/Authentication.html")]
    public class NetworkAuthenticator : MonoBehaviour
    {
        [Header("Configuration")]

        [Range(0, 255), Tooltip("Timeout to auto-disconnect in seconds. Set to 0 for no timeout.")]
        public byte timeout;

        [Header("Event Listeners (optional)")]

        /// <summary>
        /// Notify subscribers on the server when a client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventNetworkConnection OnServerAuthenticated = new UnityEventNetworkConnection();

        /// <summary>
        /// Notify subscribers on the client when the client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventNetworkConnection OnClientAuthenticated = new UnityEventNetworkConnection();

        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public virtual void OnStartServer() { }

        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// </summary>
        public virtual void OnStartClient() { }

        // This will get more code in the near future
        internal void OnServerAuthenticateInternal(NetworkConnection conn)
        {
            // Start the countdown for Authentication
            if (timeout > 0) StartCoroutine(AuthenticationTimer(conn, true));

            OnServerAuthenticate(conn);
        }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public virtual void OnServerAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnServerAuthenticated.Invoke(conn);
        }

        /// <summary>
        /// Called on client when the timeout expires without being authenticated
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public virtual void OnServerAuthenticationTimeout(NetworkConnection conn)
        {
            conn.Disconnect();
        }

        // This will get more code in the near future
        internal void OnClientAuthenticateInternal(NetworkConnection conn)
        {
            // Start the countdown for Authentication
            if (timeout > 0) StartCoroutine(AuthenticationTimer(conn, false));

            OnClientAuthenticate(conn);
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection of the client.</param>
        public virtual void OnClientAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnClientAuthenticated.Invoke(conn);
        }

        /// <summary>
        /// Called on client when the timeout expires without being authenticated
        /// </summary>
        /// <param name="conn">Connection of the client.</param>
        public virtual void OnClientAuthenticationTimeout(NetworkConnection conn)
        {
            conn.Disconnect();
        }

        /// <summary>
        /// This is called on both client and server if timeout > 0
        /// </summary>
        /// <param name="conn">Connection of the client.</param>
        public IEnumerator AuthenticationTimer(NetworkConnection conn, bool isServer)
        {
            if (LogFilter.Debug) Debug.LogFormat("Authentication countdown started {0} {1}", conn.connectionId, timeout);

            yield return new WaitForSecondsRealtime(timeout);

            if (conn != null && !conn.isAuthenticated)
            {
                if (LogFilter.Debug) Debug.LogFormat("Authentication Timeout {0}", conn.connectionId);

                if (isServer)
                    OnServerAuthenticationTimeout(conn);
                else
                    OnClientAuthenticationTimeout(conn);
            }
        }
    }
}
