using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Chat
{

    public class ChatWindow : MonoBehaviour
    {
        public string message { get; set; }

        public Text chatHistory;

        public void OnSend()
        {
            // get our player
            Player player = NetworkClient.connection.identity.GetComponent<Player>();

            // send a message
            player.CmdSend(message);
        }

        internal void AppendMessage(string message)
        {
            chatHistory.text += message + "\n";
        }
    }
}
