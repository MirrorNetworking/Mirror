using UnityEngine;

namespace Mirror.Examples.Pong
{
    /// <summary>
    /// Simular to NetworkManagerHUD but just quit buttons
    /// </summary>
    public class QuitButtonHUD : MonoBehaviour
    {
        void OnGUI()
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
                return;

            if (manager.mode == NetworkManagerMode.ServerOnly)
            {
                if (GUILayout.Button("Stop Server"))
                {
                    manager.StopServer();
                }
            }
            else if (manager.mode == NetworkManagerMode.Host)
            {
                if (GUILayout.Button("Stop Host"))
                {
                    manager.StopHost();
                }
            }
            else if (manager.mode == NetworkManagerMode.ClientOnly)
            {
                if (GUILayout.Button("Stop Client"))
                {
                    manager.StopClient();
                }
            }
        }
    }
}
