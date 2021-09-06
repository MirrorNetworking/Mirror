using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    [Serializable] public class UnityEventNetworkConnection : UnityEvent<NetworkConnection> {}

    /// <summary>Base class for implementing component-based authentication during the Connect phase</summary>
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-authenticators")]
    public abstract class NetworkAuthenticator : MonoBehaviour
    {
        /// <summary>Notify subscribers on the server when a client is authenticated</summary>
        [Header("Event Listeners (optional)")]
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventNetworkConnection OnServerAuthenticated = new UnityEventNetworkConnection();

        /// <summary>Notify subscribers on the client when the client is authenticated</summary>
        [Tooltip("Mirror has an internal subscriber to this event. You can add your own here.")]
        public UnityEventNetworkConnection OnClientAuthenticated = new UnityEventNetworkConnection();

        /// <summary>Called when server starts, used to register message handlers if needed.</summary>
        public virtual void OnStartServer() {}

        /// <summary>Called when server stops, used to unregister message handlers if needed.</summary>
        public virtual void OnStopServer() {}

        /// <summary>Called on server from OnServerAuthenticateInternal when a client needs to authenticate</summary>
        public abstract void OnServerAuthenticate(NetworkConnection conn);

        protected void ServerAccept(NetworkConnection conn)
        {
            OnServerAuthenticated.Invoke(conn);
        }

        protected void ServerReject(NetworkConnection conn)
        {
            conn.Disconnect();
        }

        /// <summary>Called when client starts, used to register message handlers if needed.</summary>
        public virtual void OnStartClient() {}

        /// <summary>Called when client stops, used to unregister message handlers if needed.</summary>
        public virtual void OnStopClient() {}

        /// <summary>Called on client from OnClientAuthenticateInternal when a client needs to authenticate</summary>
        public abstract void OnClientAuthenticate();

        protected void ClientAccept()
        {
            OnClientAuthenticated.Invoke(NetworkClient.connection);
        }

        protected void ClientReject()
        {
            // Set this on the client for local reference
            NetworkClient.connection.isAuthenticated = false;

            // disconnect the client
            NetworkClient.connection.Disconnect();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            // automatically assign authenticator field if we add this to NetworkManager
            NetworkManager manager = GetComponent<NetworkManager>();
            if (manager != null && manager.authenticator == null)
            {
                manager.authenticator = this;
                UnityEditor.Undo.RecordObject(gameObject, "Assigned NetworkManager authenticator");
            }
#endif
        }
    }
}
