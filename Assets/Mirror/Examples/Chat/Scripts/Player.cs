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
            // Restrict to ASCII printable characters by stripping anything outside the range from space through tilde
            string cleanMessage = System.Text.RegularExpressions.Regex.Replace(message.Trim(), @"![ -~]", "");

            if (cleanMessage == "") return;

            RpcReceive(cleanMessage);
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
