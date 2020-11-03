using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Base class for implementing component-based authentication during the Connect phase
    /// </summary>
    [HelpURL("https://mirrorng.github.io/MirrorNG/Articles/Components/Authenticators/index.html")]
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

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.Undo.RecordObject(this, "Assigned NetworkClient authenticator");
            // automatically assign NetworkClient field if we add this to NetworkClient
            NetworkClient client = GetComponent<NetworkClient>();
            if (client != null && client.authenticator == null)
            {
                client.authenticator = this;
            }

            NetworkServer server = GetComponent<NetworkServer>();
            if (server != null && server.authenticator == null)
            {
                server.authenticator = this;
            }
        }
#endif
    }
}
