using UnityEngine;

namespace Mirror.Examples.SceneChange
{
    public class SceneSwitcherHud : MonoBehaviour
    {
        public NetworkManager networkManager;
        NetworkSceneManager sceneManager;
        bool additiveLoaded;

        private void Start()
        {
            sceneManager = GetComponent<NetworkSceneManager>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 400, 215, 9999));

            if (GUILayout.Button("Switch to Room1"))
            {
                sceneManager.ChangeServerScene("Room1");
                additiveLoaded = false;
            }

            if (GUILayout.Button("Switch to Room2"))
            {
                sceneManager.ChangeServerScene("Room2");
                additiveLoaded = false;
            }

            if(additiveLoaded)
            {
                if (GUILayout.Button("Addive Unload"))
                {
                    additiveLoaded = false;
                    sceneManager.ChangeServerScene("Additive", SceneOperation.UnloadAdditive);
                }
            }
            else
            {
                if (GUILayout.Button("Additive Load"))
                {
                    additiveLoaded = true;
                    sceneManager.ChangeServerScene("Additive", SceneOperation.LoadAdditive);
                }
            }

            GUILayout.EndArea();
        }
    }
}
