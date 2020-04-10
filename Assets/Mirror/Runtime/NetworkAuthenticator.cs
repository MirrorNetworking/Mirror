using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Base class for implementing component-based authentication during the Connect phase
    /// </summary>
    [HelpURL("https://mirror-networking.com/docs/Guides/Authentication.html")]
    public abstract class NetworkAuthenticator : MonoBehaviour
    {
        /// <summary>
        /// Notify subscribers on the server when a client is authenticated
        /// </summary>
        public event Action<INetworkConnection> OnServerAuthenticated;

        /// <summary>
        /// Notify subscribers on the client when the client is authenticated
        /// </summary>
        public event Action<INetworkConnection> OnClientAuthenticated;

        #region server

        // This will get more code in the near future
        internal void OnServerAuthenticateInternal(INetworkConnection conn)
        {
            OnServerAuthenticate(conn);
        }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public virtual void OnServerAuthenticate(INetworkConnection conn)
        {
            OnServerAuthenticated?.Invoke(conn);
        }

        #endregion

        #region client

        // This will get more code in the near future
        internal void OnClientAuthenticateInternal(INetworkConnection conn)
        {
            OnClientAuthenticate(conn);
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection of the client.</param>
        public virtual void OnClientAuthenticate(INetworkConnection conn)
        {
            OnClientAuthenticated?.Invoke(conn);
        }

        #endregion

        void OnValidate()
        {
#if UNITY_EDITOR
            // automatically assign NetworkManager field if we add this to NetworkManager
            NetworkClient client = GetComponent<NetworkClient>();
            if (client != null && client.authenticator == null)
            {
                client.authenticator = this;
                UnityEditor.Undo.RecordObject(gameObject, "Assigned NetworkClient authenticator");
            }

            NetworkServer server = GetComponent<NetworkServer>();
            if (server != null && server.authenticator == null)
            {
                server.authenticator = this;
                UnityEditor.Undo.RecordObject(gameObject, "Assigned NetworkServer authenticator");
            }
#endif
        }
    }
}
