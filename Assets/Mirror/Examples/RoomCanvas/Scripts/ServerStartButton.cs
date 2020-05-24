using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.NetworkRoomCanvas
{
    [RequireComponent(typeof(Button))]
    public class ServerStartButton : MonoBehaviour
    {
        Button button;
        NetworkRoomManagerExample manager;

        void OnEnable()
        {
            button = GetComponent<Button>();
            if (NetworkServer.active)
            {
                button.onClick.AddListener(OnClick);

                manager = NetworkManager.singleton as NetworkRoomManagerExample;
                manager.onServerAllPlayersReady += Manager_onServerAllPlayersReady;

                // call now incase event was missed
                Manager_onServerAllPlayersReady(manager.allPlayersReady);
            }
            else
            {
                // button only needs to be active for server
                gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            if (manager != null)
            {
                manager.onServerAllPlayersReady -= Manager_onServerAllPlayersReady;
            }
        }

        void Manager_onServerAllPlayersReady(bool allReady)
        {
            button.interactable = allReady;
        }

        void OnClick()
        {
            manager.ServerChangeScene(manager.GameplayScene);
        }
    }
}
