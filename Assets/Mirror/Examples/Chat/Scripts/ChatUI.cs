using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Chat
{
    public class ChatUI : NetworkBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] Text chatHistory;
        [SerializeField] Scrollbar scrollbar;
        [SerializeField] InputField chatMessage;
        [SerializeField] Button sendButton;

        // This is only set on client to the name of the local player
        internal static string localPlayerName;

        // Server name, set from clicking Start Server on login screen
        internal static string serverPlayerName;

        // Reference to local players script for back n forth communication
        public Player localPlayerScript;

        // Server-only cross-reference of connections to player names
        internal static readonly Dictionary<NetworkConnectionToClient, string> connNames = new Dictionary<NetworkConnectionToClient, string>();

        public override void OnStartServer()
        {
            connNames.Clear();
        }

        public override void OnStartClient()
        {
            chatHistory.text = "";
        }

        // legacy alternative shortcut way  to send commands without using authorised player connection
        /*
        [Command(requiresAuthority = false)]
        void CmdSend(string message, NetworkConnectionToClient sender = null)
        {
            if (!connNames.ContainsKey(sender))
                connNames.Add(sender, sender.identity.GetComponent<Player>().playerName);

            if (!string.IsNullOrWhiteSpace(message))
                RpcReceive(connNames[sender], message.Trim());
        }
        */
        [ClientRpc]
        public void RpcReceive(string playerName, string message)
        {
            Receive(playerName, message);
        }

        // Originally inside RpcReceive, splitting it allows Server Only Mode to also call the function
        public void Receive(string playerName, string message)
        {
            string prettyMessage;

            if (isServerOnly && serverPlayerName == playerName)
            {
                prettyMessage = $"<color=magenta>{playerName}:</color> {message}";
            }
            else
            {
                prettyMessage = playerName == localPlayerName ?
                $"<color=red>{playerName}:</color> {message}" :
                $"<color=blue>{playerName}:</color> {message}";
            }
            AppendMessage(prettyMessage);
        }

        void AppendMessage(string message)
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

        // Called by UI element ExitButton.OnClick
        public void ExitButtonOnClick()
        {
            // StopHost calls both StopClient and StopServer
            // StopServer does nothing on remote clients
            NetworkManager.singleton.StopHost();
        }

        // Called by UI element MessageField.OnValueChanged
        public void ToggleButton(string input)
        {
            sendButton.interactable = !string.IsNullOrWhiteSpace(input);
        }

        // Called by UI element MessageField.OnEndEdit
        public void OnEndEdit(string input)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetButtonDown("Submit"))
                SendMessage();
        }

        // Called by OnEndEdit above and UI element SendButton.OnClick
        public void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(chatMessage.text))
            {
                if (isServerOnly)
                {
                    // server has no player object, and cant call cmd, so we do this:
                    RpcReceive(serverPlayerName, chatMessage.text.Trim());
                    Receive(serverPlayerName, chatMessage.text.Trim());
                }
                else
                {
                    localPlayerScript.CmdSend(chatMessage.text.Trim());
                }
                chatMessage.text = string.Empty;
                chatMessage.ActivateInputField();
            }
        }
    }
}
