using System;
using System.Collections.Generic;

namespace Mirror.Examples.Chat
{
    public class Player : NetworkBehaviour
    {
        public static readonly HashSet<string> playerNames = new HashSet<string>();

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        static void ResetStatics()
        {
            playerNames.Clear();
        }

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
