using System;
using UnityEngine;

namespace Mirror.Authenticators
{
    /// <summary>
    /// An authenticator that identifies the user by their device.
    /// <para>A GUID is used as a fallback when the platform doesn't support SystemInfo.deviceUniqueIdentifier.</para>
    /// <para>Note: deviceUniqueIdentifier can be spoofed, so security is not guaranteed.</para>
    /// <para>See https://docs.unity3d.com/ScriptReference/SystemInfo-deviceUniqueIdentifier.html for details.</para>
    /// </summary>
    [AddComponentMenu("Network/ Authenticators/Device Authenticator")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-authenticators/device-authenticator")]
    public class DeviceAuthenticator : NetworkAuthenticator
    {
        #region Messages

        public struct AuthRequestMessage : NetworkMessage
        {
            public string clientDeviceID;
        }

        public struct AuthResponseMessage : NetworkMessage { }

        #endregion

        #region Server

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
            // do nothing, wait for client to send his id
        }

        void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            Debug.Log($"connection {conn.connectionId} authenticated with id {msg.clientDeviceID}");

            // Store the device id for later reference, e.g. when spawning the player
            conn.authenticationData = msg.clientDeviceID;

            // Send a response to client telling it to proceed as authenticated
            conn.Send(new AuthResponseMessage());

            // Accept the successful authentication
            ServerAccept(conn);
        }

        #endregion

        #region Client

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
            string deviceUniqueIdentifier = SystemInfo.deviceUniqueIdentifier;

            // Not all platforms support this, so we use a GUID instead
            if (deviceUniqueIdentifier == SystemInfo.unsupportedIdentifier)
            {
                // Get the value from PlayerPrefs if it exists, new GUID if it doesn't
                deviceUniqueIdentifier = PlayerPrefs.GetString("deviceUniqueIdentifier", Guid.NewGuid().ToString());

                // Store the deviceUniqueIdentifier to PlayerPrefs (in case we just made a new GUID)
                PlayerPrefs.SetString("deviceUniqueIdentifier", deviceUniqueIdentifier);
            }

            // send the deviceUniqueIdentifier to the server
            NetworkClient.connection.Send(new AuthRequestMessage { clientDeviceID = deviceUniqueIdentifier } );
        }

        /// <summary>
        /// Called on client when the server's AuthResponseMessage arrives
        /// </summary>
        /// <param name="msg">The message payload</param>
        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            Debug.Log("Authentication Success");
            ClientAccept();
        }

        #endregion
    }
}
