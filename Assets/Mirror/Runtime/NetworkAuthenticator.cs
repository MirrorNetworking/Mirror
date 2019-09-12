using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    /// <summary>
    /// Unity Event for the NetworkConnection
    /// </summary>
    [Serializable] public class UnityEventConnection : UnityEvent<NetworkConnection> { }

    /// <summary>
    /// Abstract class for implementing component-based authentication during the Connect phase
    /// </summary>
    [AddComponentMenu("Network/NetworkAuthenticator")]
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Concepts/Authentication.html")]
    public class NetworkAuthenticator : MonoBehaviour
    {
        [Header("Event Listeners (optional)")]

        /// <summary>
        /// Notify subscribers on the server when a client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventConnection OnServerAuthenticated = new UnityEventConnection();

        /// <summary>
        /// Notify subscribers on the client when the client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventConnection OnClientAuthenticated = new UnityEventConnection();

        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public virtual void ServerInitialize() { }

        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// </summary>
        public virtual void ClientInitialize() { }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void ServerAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnServerAuthenticated.Invoke(conn);
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public virtual void ClientAuthenticate(NetworkConnection conn)
        {
            // setting NetworkConnection.isAuthenticated = true is Required
            conn.isAuthenticated = true;

            // invoking the event is Required
            OnClientAuthenticated.Invoke(conn);
        }
    }
}
