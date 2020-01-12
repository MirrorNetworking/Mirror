using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.Chat
{
    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public string playerName;

        public static event Action<Player, string> OnMessage;

        [Command]
        public void CmdSend(string message)
        {
            if (message.Trim() != "")
                RpcReceive(message.Trim());
        }

        [ClientRpc]
        public void RpcReceive(string message)
        {
            OnMessage?.Invoke(this, message);
        }
    }
}
