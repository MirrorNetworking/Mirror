using System;
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
    public abstract class NetworkAuthenticator : MonoBehaviour
    {
        [Header("Event Listeners (optional)")]

        /// <summary>
        /// Notify subscribers on the server when a client is authenticated.
        /// Call this from OnServerAuthenticate when successfully authenticated.
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventNetworkConnection OnServerAuthenticated = new UnityEventNetworkConnection();

        /// <summary>
        /// Notify subscribers on the client when the client is authenticated
        /// Call this from OnServerAuthenticate when successfully authenticated.
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

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate.
        ///
        /// Make sure to call OnServerAuthenticated.Invoke() after a successful authentication!
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public abstract void OnServerAuthenticate(NetworkConnection conn);

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        ///
        /// Make sure to call OnClientAuthenticated.Invoke() after a successful authentication!
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public abstract void OnClientAuthenticate(NetworkConnection conn);
    }
}
