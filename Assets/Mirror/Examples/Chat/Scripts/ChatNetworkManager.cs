using UnityEngine;

namespace Mirror.Examples.Chat
{
    [AddComponentMenu("")]
    public class ChatNetworkManager : NetworkManager
    {
        // Called by UI element NetworkAddressInput.OnValueChanged
        public void SetHostname(string hostname)
        {
            networkAddress = hostname;
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // remove player name from the HashSet
            if (conn.authenticationData != null)
                ChatAuthenticator.playerNames.Remove((string)conn.authenticationData);

            // remove connection from Dictionary of conn > names
            ChatUI.connNames.Remove(conn);

            base.OnServerDisconnect(conn);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            LoginUI.instance.gameObject.SetActive(true);
            LoginUI.instance.usernameInput.text = "";
            LoginUI.instance.usernameInput.ActivateInputField();
        }
    }
}
