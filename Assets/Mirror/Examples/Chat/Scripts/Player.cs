using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Chat
{
    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public string playerName;

        public ChatWindow chatWindow => ((ChatNetworkManager)NetworkManager.singleton).chatWindow;

        [Command]
        public void CmdSend(string message)
        {
            if (message.Trim() != "")
                RpcReceive(message.Trim());
        }

        public override void OnStartLocalPlayer()
        {
            chatWindow.gameObject.SetActive(true);
        }

        [ClientRpc]
        public void RpcReceive(string message)
        {
            string prettyMessage = isLocalPlayer ?
                $"<color=red>{playerName}: </color> {message}" :
                $"<color=blue>{playerName}: </color> {message}";

            chatWindow.AppendMessage(prettyMessage);

            Debug.Log(message);
        }
    }
}
