using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.NetworkLobby
{
    public class OfflineGUI : MonoBehaviour
    {
        void Start()
        {
            Application.targetFrameRate = 10;
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10, 10, 200, 130), "OFFLINE  SCENE");

            GUI.Box(new Rect(10, 40, 200, 40), "WASDQE keys to move & turn\nTouch the spheres for points\nLighter colors score higher");

            if (GUI.Button(new Rect(60, 100, 100, 30), "Join Game"))
            {
                SceneManager.LoadScene("Lobby");
            }
        }
    }
}
