using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Mirror.Examples.Chat
{
    public class ChatWindow : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(ChatWindow));

        [FormerlySerializedAs("client")]
        public NetworkClient Client;
        [FormerlySerializedAs("chatMessage")]
        public InputField ChatMessage;
        [FormerlySerializedAs("chatHistory")]
        public Text ChatHistory;
        [FormerlySerializedAs("scrollbar")]
        public Scrollbar Scrollbar;

        public void Awake()
        {
            Player.OnMessage += OnPlayerMessage;
        }

        void OnPlayerMessage(Player player, string message)
        {
            string prettyMessage = player.IsLocalPlayer ?
                $"<color=red>{player.playerName}: </color> {message}" :
                $"<color=blue>{player.playerName}: </color> {message}";
            AppendMessage(prettyMessage);

            logger.Log(message);
        }

        public void OnSend()
        {
            if (ChatMessage.text.Trim() == "")
                return;

            // get our player
            Player player = Client.Connection.Identity.GetComponent<Player>();

            // send a message
            player.CmdSend(ChatMessage.text.Trim());

            ChatMessage.text = "";
        }

        internal void AppendMessage(string message)
        {
            StartCoroutine(AppendAndScroll(message));
        }

        IEnumerator AppendAndScroll(string message)
        {
            ChatHistory.text += message + "\n";

            // it takes 2 frames for the UI to update ?!?!
            yield return null;
            yield return null;

            // slam the scrollbar down
            Scrollbar.value = 0;
        }
    }
}
