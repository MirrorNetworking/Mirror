using System.Diagnostics;

namespace Mirror.Examples.Chat
{
    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public string playerName;

        // Reference to scene script, can be public and manually set, to skip the FindObjectOfType call
        private ChatUI chatUI;

        public override void OnStartServer()
        {
            playerName = (string)connectionToClient.authenticationData;

            FindUIReference();
        }

        public override void OnStartLocalPlayer()
        {
            FindUIReference();

            chatUI.localPlayerScript = this;

            ChatUI.localPlayerName = playerName;
        }

        [Command]
        public void CmdSend(string message)
        {
            if (!ChatUI.connNames.ContainsKey(connectionToClient))
                ChatUI.connNames.Add(connectionToClient, playerName);

            if (!string.IsNullOrWhiteSpace(message))
                chatUI.RpcReceive(ChatUI.connNames[connectionToClient], message.Trim());

            // Server only would not run the contents inside the rpc, which is where the chat gets added, so we call this
            if (isServerOnly)
            {
                chatUI.Receive(ChatUI.connNames[connectionToClient], message.Trim());
            }
        }

        // Server and local client need the reference
        private void FindUIReference()
        {
            if (chatUI == null)
            {
                chatUI = FindObjectOfType<ChatUI>();
            }
        }
    }
}
