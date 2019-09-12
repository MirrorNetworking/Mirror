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
    [HelpURL("https://mirror-networking.com/xmldocs/articles/Concepts/Authentication.html")]
    public abstract class Authenticator : MonoBehaviour
    {
        [Header("Event Listeners (optional)")]

        /// <summary>
        /// Notify subscribers on the server when a client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventConnection OnServerAuthenticated = new UnityEventConnection();

        /// <summary>
        /// Notify subscribers on the client when a client is authenticated
        /// </summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventConnection OnClientAuthenticated = new UnityEventConnection();

        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// <para>Implementors should return true to have OnServerAuthenticated listener engaged.</para>
        /// </summary>
        /// <returns>True if initialization successful</returns>
        public abstract bool ServerInitialize();

        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// <para>Implementors should return true to have OnClientAuthenticated listener engaged.</para>
        /// </summary>
        /// <returns>True if initialization successful</returns>
        public abstract bool ClientInitialize();

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public abstract void ServerAuthenticate(NetworkConnection conn);

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public abstract void ClientAuthenticate(NetworkConnection conn);
    }
}
