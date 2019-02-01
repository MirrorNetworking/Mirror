using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.Lobby
{
    public class OfflineGUI : MonoBehaviour
    {
        void OnGUI()
        {
            Rect addRec = new Rect(300, 220, 120, 20);
            if (GUI.Button(addRec, "Join Game"))
            {
                SceneManager.LoadScene("Lobby");
            }
        }
    }
}
