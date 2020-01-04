using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkRoom
{
    public class OfflineGUI : MonoBehaviour
    {
        [Scene]
        public string RoomScene;

        void Start()
        {
            // Ensure main camera is enabled because it will be disabled by PlayerController
            Camera.main.enabled = true;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 130));

            GUILayout.Box("OFFLINE  SCENE");
            GUILayout.Box("WASDQE keys to move & turn\nTouch the spheres for points\nLighter colors score higher");

            if (GUILayout.Button("Join Game"))
                SceneManager.LoadScene(RoomScene);

            GUILayout.EndArea();
        }
    }
}
