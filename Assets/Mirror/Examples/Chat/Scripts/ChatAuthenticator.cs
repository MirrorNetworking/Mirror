using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    Documentation: https://mirror-networking.gitbook.io/docs/components/network-authenticators
    API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkAuthenticator.html
*/

namespace Mirror.Examples.Chat
{
    [AddComponentMenu("")]
    public class ChatAuthenticator : NetworkAuthenticator
    {
        readonly HashSet<NetworkConnectionToClient> connectionsPendingDisconnect = new HashSet<NetworkConnectionToClient>();
        internal static readonly HashSet<string> playerNames = new HashSet<string>();

        [Header("Client Username")]
        public string playerName;

        #region Messages

        public struct AuthRequestMessage : NetworkMessage
        {
            // use whatever credentials make sense for your game
            // for example, you might want to pass the accessToken if using oauth
            public string authUsername;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public byte code;
            public string message;
        }

        #endregion

        #region Server

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        static void ResetStatics()
        {
            playerNames.Clear();
        }

        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartServer()
        {
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        /// <summary>
        /// Called on server from StopServer to reset the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStopServer()
        {
            // unregister the handler for the authentication request
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        /// <summary>
        /// Called on server from OnServerConnectInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // do nothing...wait for AuthRequestMessage from client
        }

        /// <summary>
        /// Called on server when the client's AuthRequestMessage arrives
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        /// <param name="msg">The message payload</param>
        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            Debug.Log($"Authentication Request: {msg.authUsername}");

            if (connectionsPendingDisconnect.Contains(conn)) return;

            // check the credentials by calling your web server, database table, playfab api, or any method appropriate.
            if (!playerNames.Contains(msg.authUsername))
            {
                // Add the name to the HashSet
                playerNames.Add(msg.authUsername);

                // Store username in authenticationData
                // This will be read in Player.OnStartServer
                // to set the playerName SyncVar.
                conn.authenticationData = msg.authUsername;

                // create and send msg to client so it knows to proceed
                AuthResponseMessage authResponseMessage = new AuthResponseMessage
                {
                    code = 100,
                    message = "Success"
                };

                conn.Send(authResponseMessage);

                // Accept the successful authentication
                ServerAccept(conn);
            }
            else
            {
                connectionsPendingDisconnect.Add(conn);

                // create and send msg to client so it knows to disconnect
                AuthResponseMessage authResponseMessage = new AuthResponseMessage
                {
                    code = 200,
                    message = "Username already in use...try again"
                };

                conn.Send(authResponseMessage);

                // must set NetworkConnection isAuthenticated = false
                conn.isAuthenticated = false;

                // disconnect the client after 1 second so that response message gets delivered
                StartCoroutine(DelayedDisconnect(conn, 1f));
            }
        }

        IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            // Reject the unsuccessful authentication
            ServerReject(conn);

            yield return null;

            // remove conn from pending connections
            connectionsPendingDisconnect.Remove(conn);
        }

        #endregion

        #region Client

        // Called by UI element UsernameInput.OnValueChanged
        public void SetPlayername(string username)
        {
            playerName = username;
            LoginUI.instance.errorText.text = string.Empty;
            LoginUI.instance.errorText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartClient()
        {
            // register a handler for the authentication response we expect from server
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        /// <summary>
        /// Called on client from StopClient to reset the Authenticator
        /// <para>Client message handlers should be unregistered in this method.</para>
        /// </summary>
        public override void OnStopClient()
        {
            // unregister the handler for the authentication response
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        /// <summary>
        /// Called on client from OnClientConnectInternal when a client needs to authenticate
        /// </summary>
        public override void OnClientAuthenticate()
        {
            NetworkClient.Send(new AuthRequestMessage { authUsername = playerName });
        }

        /// <summary>
        /// Called on client when the server's AuthResponseMessage arrives
        /// </summary>
        /// <param name="msg">The message payload</param>
        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.code == 100)
            {
                Debug.Log($"Authentication Response: {msg.code} {msg.message}");

                // Authentication has been accepted
                ClientAccept();
            }
            else
            {
                Debug.LogError($"Authentication Response: {msg.code} {msg.message}");

                // Authentication has been rejected
                // StopHost works for both host client and remote clients
                NetworkManager.singleton.StopHost();

                // Do this AFTER StopHost so it doesn't get cleared / hidden by OnClientDisconnect
                LoginUI.instance.errorText.text = msg.message;
                LoginUI.instance.errorText.gameObject.SetActive(true);
            }
        }

        #endregion
    }
}
