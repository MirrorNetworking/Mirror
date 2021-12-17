using System;
using System.Collections.Generic;

namespace Mirror.Examples.Chat
{
    public class Player : NetworkBehaviour
    {
        public static readonly HashSet<string> playerNames = new HashSet<string>();

        public override void OnStartServer()
        {
            playerName = (string)connectionToClient.authenticationData;
        }

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName;

        void OnPlayerNameChanged(string _, string newName)
        {
            ChatUI.instance.localPlayerName = playerName;
        }
    }
}
