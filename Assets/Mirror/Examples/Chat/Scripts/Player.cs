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
            RpcReceive(message);
        }

        public override void OnStartLocalPlayer()
        {
            chatWindow.gameObject.SetActive(true);
        }

        [ClientRpc]
        public void RpcReceive(string message)
        {
            string prettyMessage = isLocalPlayer ?
                $"<color=red>{Name}: </color> {message}" :
                $"<color=blue>{Name}: </color> {message}";
            
            chatWindow.AppendMessage(prettyMessage);

            Debug.Log(message);
        }
    }
}
