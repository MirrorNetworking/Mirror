using UnityEngine;
using UnityEngine.SceneManagement;

public class Spawner : MonoBehaviour
{
    public GameObject prefab;

    void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 100, 25), "Spawn on Client"))
        {
            SceneManager.SetActiveScene(Bootstrap.ClientWorld);
            GameObject spawned = Instantiate(prefab);
        }

        if (GUI.Button(new Rect(0, 30, 100, 25), "Spawn on Server"))
        {
            SceneManager.SetActiveScene(Bootstrap.ServerWorld);
            GameObject spawned = Instantiate(prefab);
        }
    }
}
