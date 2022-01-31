using System;
using UnityEngine;

namespace Mirror.Authenticators
{
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

        #region Logging

        void Log(string message, ConsoleColor consoleColor)
        {
#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(message);
            Console.ResetColor();
#else
            Debug.Log(message);
#endif
        }

        #endregion

        #region Server

        public override void OnStartServer()
        {
            Log($"DeviceAuthenticator::OnStartServer", ConsoleColor.DarkGreen);
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        public override void OnStopServer()
        {
            Log($"DeviceAuthenticator::OnStopServer", ConsoleColor.DarkYellow);
            // unregister the handler for the authentication request
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        public override void OnServerAuthenticate(NetworkConnection conn)
        {
            Log($"DeviceAuthenticator::OnServerAuthenticate {conn}", ConsoleColor.DarkGreen);
            // do nothing, wait for client to send his id
        }

        void OnAuthRequestMessage(NetworkConnection conn, AuthRequestMessage msg)
        {
            Log($"DeviceAuthenticator::OnAuthRequestMessage connection {conn.connectionId} authenticated with id {msg.clientDeviceID}", ConsoleColor.Green);

            // Store the device id for later reference, e.g. when spawning the player
            conn.authenticationData = msg.clientDeviceID;

            // Send a response to client telling it to proceed as authenticated
            conn.Send(new AuthResponseMessage());

            // Accept the successful authentication
            ServerAccept(conn);
        }

        #endregion

        #region Client

        public override void OnStartClient()
        {
            Log($"DeviceAuthenticator::OnStartClient", ConsoleColor.DarkGreen);
            // register a handler for the authentication response we expect from server
            NetworkClient.RegisterHandler<AuthResponseMessage>((Action<AuthResponseMessage>)OnAuthResponseMessage, false);
        }

        public override void OnStopClient()
        {
            Log($"DeviceAuthenticator::OnStopClient", ConsoleColor.DarkYellow);
            // unregister the handler for the authentication response
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        public override void OnClientAuthenticate()
        {
            Log($"DeviceAuthenticator::OnClientAuthenticate", ConsoleColor.DarkGreen);
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
            NetworkClient.connection.Send(new AuthRequestMessage { clientDeviceID = deviceUniqueIdentifier });
        }

        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            Log($"DeviceAuthenticator::OnAuthResponseMessage Authentication Success", ConsoleColor.DarkGreen);
            ClientAccept();
        }

        #endregion
    }
}
