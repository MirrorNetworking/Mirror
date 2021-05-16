using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject prefab;

    void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 100, 25), "Spawn"))
        {
            GameObject spawned = Instantiate(prefab);
        }
    }
}
