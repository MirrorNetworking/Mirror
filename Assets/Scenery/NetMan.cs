using UnityEngine;

public class NetMan : MonoBehaviour
{
    void OnGUI()
    {
        if (GUI.Button(new Rect(5, 5, 100, 25), "Start Server"))
        {
        }

        if (GUI.Button(new Rect(5, 30, 100, 25), "Start Client"))
        {
        }
    }
}
