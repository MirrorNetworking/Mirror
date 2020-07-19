using UnityEngine;

namespace Mirror.Examples.SceneChange
{
    public class SceneSwitcherHud : MonoBehaviour
    {
        public NetworkManager networkManager;

        bool additiveLoaded;

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

            if(additiveLoaded)
            {
                if (GUILayout.Button("Addive Unload"))
                {
                    additiveLoaded = false;
                    networkManager.server.sceneManager.ChangeServerScene("Additive", SceneOperation.UnloadAdditive);
                }
            }
            else
            {
                if (GUILayout.Button("Additive Load"))
                {
                    additiveLoaded = true;
                    networkManager.server.sceneManager.ChangeServerScene("Additive", SceneOperation.LoadAdditive);
                }
            }

            GUILayout.EndArea();
        }
    }
}
