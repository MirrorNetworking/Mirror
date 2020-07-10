using UnityEngine;

namespace Mirror.Examples.SceneChange
{
    public class SceneSwitcherHud : MonoBehaviour
    {
        public NetworkManager networkManager;

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 400, 215, 9999));

            if (GUILayout.Button("Switch to Room1"))
            {
                networkManager.server.sceneManager.ChangeServerScene("Room1");
            }

            if (GUILayout.Button("Switch to Room2"))
            {
                networkManager.server.sceneManager.ChangeServerScene("Room2");
            }

            GUILayout.EndArea();
        }
    }
}
