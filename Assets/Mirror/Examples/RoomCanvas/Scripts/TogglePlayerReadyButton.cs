using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.NetworkRoomCanvas
{

    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Graphic))]
    public class TogglePlayerReadyButton : MonoBehaviour
    {
        [Header("State Colors")]
        public Color readyColor = Color.green;
        public Color notReadyColor = Color.red;

        NetworkRoomPlayerExample localPlayer;
        private Button button;
        private Graphic graphic;

        void OnEnable()
        {
            button = GetComponent<Button>();

            if (NetworkClient.active)
            {
                button.onClick.AddListener(OnClick);

                graphic = GetComponent<Graphic>();

                ClientScene.onLocalPlayerChanged += ClientScene_onLocalPlayerChanged;

                // call now incase event was missed
                ClientScene_onLocalPlayerChanged(null, ClientScene.localPlayer);
            }
            else
            {
                // button only needs to exists on host/client
                gameObject.SetActive(false);
            }
        }
        void OnDisable()
        {
            ClientScene.onLocalPlayerChanged -= ClientScene_onLocalPlayerChanged;
        }

        private void ClientScene_onLocalPlayerChanged(NetworkIdentity _, NetworkIdentity newPlayer)
        {
            if (newPlayer != null)
            {
                localPlayer = newPlayer.GetComponent<NetworkRoomPlayerExample>();
                graphic.color = localPlayer.readyToBegin ? readyColor : notReadyColor;
            }
            else
            {
                localPlayer = null;
            }
        }

        private void OnClick()
        {
            if (localPlayer != null)
            {
                //toggle ready
                bool ready = !localPlayer.readyToBegin;

                graphic.color = ready ? readyColor : notReadyColor;
                localPlayer.CmdChangeReadyState(ready);
            }
        }
    }
}
