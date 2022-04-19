using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    [Serializable] public class UnityEventNetworkConnection : UnityEvent<NetworkConnectionToClient> {}

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
        public UnityEvent OnClientAuthenticated = new UnityEvent();

        /// <summary>Called when server starts, used to register message handlers if needed.</summary>
        public virtual void OnStartServer() {}

        /// <summary>Called when server stops, used to unregister message handlers if needed.</summary>
        public virtual void OnStopServer() {}

        /// <summary>Called on server from OnServerConnectInternal when a client needs to authenticate</summary>
        public virtual void OnServerAuthenticate(NetworkConnectionToClient conn) {}

        protected void ServerAccept(NetworkConnectionToClient conn)
        {
            OnServerAuthenticated.Invoke(conn);
        }

        protected void ServerReject(NetworkConnectionToClient conn)
        {
            conn.Disconnect();
        }

        /// <summary>Called when client starts, used to register message handlers if needed.</summary>
        public virtual void OnStartClient() {}

        /// <summary>Called when client stops, used to unregister message handlers if needed.</summary>
        public virtual void OnStopClient() {}

        /// <summary>Called on client from OnClientConnectInternal when a client needs to authenticate</summary>
        public virtual void OnClientAuthenticate() {}

        protected void ClientAccept()
        {
            OnClientAuthenticated.Invoke();
        }

        protected void ClientReject()
        {
            // Set this on the client for local reference
            NetworkClient.connection.isAuthenticated = false;

            // disconnect the client
            NetworkClient.connection.Disconnect();
        }
        
        // Reset() instead of OnValidate():
        // Any NetworkAuthenticator assigns itself to the NetworkManager, this is fine on first adding it, 
        // but if someone intentionally sets Authenticator to null on the NetworkManager again then the 
        // Authenticator will reassign itself if a value in the inspector is changed.
        // My change switches OnValidate to Reset since Reset is only called when the component is first 
        // added (or reset is pressed).
        void Reset()
        {
#if UNITY_EDITOR
            // automatically assign authenticator field if we add this to NetworkManager
            NetworkManager manager = GetComponent<NetworkManager>();
            if (manager != null && manager.authenticator == null)
            {
                // undo has to be called before the change happens
                UnityEditor.Undo.RecordObject(manager, "Assigned NetworkManager authenticator");
                manager.authenticator = this;
            }
#endif
        }
    }
}
