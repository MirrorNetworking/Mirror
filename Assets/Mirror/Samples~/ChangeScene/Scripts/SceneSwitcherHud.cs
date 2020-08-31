using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.SceneChange
{
    public class SceneSwitcherHud : MonoBehaviour
    {
        public NetworkSceneManager sceneManager;
        public Text AdditiveButtonText;
        bool additiveLoaded;

        public void Update()
        {
            if(additiveLoaded)
            {
                AdditiveButtonText.text = "Additive Unload";
            }
            else
            {
                AdditiveButtonText.text = "Additive Load";
            }
        }

        public void Room1ButtonHandler()
        {
            sceneManager.ChangeServerScene("Room1");
            additiveLoaded = false;
        }

        public void Room2ButtonHandler()
        {
            sceneManager.ChangeServerScene("Room2");
            additiveLoaded = false;
        }

        public void AdditiveButtonHandler()
        {
            if (additiveLoaded)
            {
                additiveLoaded = false;
                sceneManager.ChangeServerScene("Additive", SceneOperation.UnloadAdditive);
            }
            else
            {
                additiveLoaded = true;
                sceneManager.ChangeServerScene("Additive", SceneOperation.LoadAdditive);
            }
        }
    }
}
