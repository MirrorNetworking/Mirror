using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Chat
{
    public class ChatWindow : MonoBehaviour
    {
        public InputField chatMessage;
        public Text chatHistory;
        public Scrollbar scrollbar;

        public void Awake()
        {
            Player.OnMessage += OnPlayerMessage;
            chatMessage.onEndEdit.AddListener(OnEndEdit);
        }

        void OnEndEdit(string input)
        {
            if (Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetButtonDown("Submit"))
            {
                //Debug.Log($"OnEndEdit {input}");
                SendMessage();
                chatMessage.text = string.Empty;
                chatMessage.ActivateInputField();
            }
        }

        void OnPlayerMessage(Player player, string message)
        {
            string prettyMessage = player.isLocalPlayer ?
                $"<color=red>{player.playerName}:</color> {message}" :
                $"<color=blue>{player.playerName}:</color> {message}";
            AppendMessage(prettyMessage);

            Debug.Log(message);
        }

        // Called by UI element SendButton.OnClick
        public void SendMessage()
        {
            if (chatMessage.text.Trim() == string.Empty)
                return;

            // get our player
            Player player = NetworkClient.connection.identity.GetComponent<Player>();

            // send a message
            player.CmdSend(chatMessage.text.Trim());
        }

        internal void AppendMessage(string message)
        {
            StartCoroutine(AppendAndScroll(message));
        }

        IEnumerator AppendAndScroll(string message)
        {
            chatHistory.text += message + "\n";

            // it takes 2 frames for the UI to update ?!?!
            yield return null;
            yield return null;

            // slam the scrollbar down
            scrollbar.value = 0;
        }
    }
}
