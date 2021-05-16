using UnityEngine;
using UnityEngine.SceneManagement;

public class NetMan : MonoBehaviour
{
    // Server/Client worlds for easy access
    public const string ClientWorldName = "ClientWorld";
    public const string ServerWorldName = "ServerWorld";
    public Scene ClientWorld;
    public Scene ServerWorld;
    Scene originalScene;

    // helper function to LoadScene with a custom name
    // -> LoadScene additive can duplicate but not change the name
    // -> we need to Create & Merge for that
    void LoadSceneCustomName(string scenePath, LoadSceneParameters parameters, string customName)
    {
        SceneManager.CreateScene(customName);
        SceneManager.LoadScene(gameObject.scene.path, LoadSceneMode.Additive);
        //SceneManager.MergeScenes();
    }

    void Awake()
    {
        // remember the original scene
        originalScene = SceneManager.GetActiveScene();

        // let's create a ServerWorld and ClientWorld like in DOTSNET
        // so that even if we connect client 1 hour after starting server,
        // the client still starts with a fresh client scene like everyone else.
        SceneManager.CreateScene(ClientWorldName);
        SceneManager.CreateScene(ServerWorldName);
        ClientWorld = SceneManager.GetSceneByName(ClientWorldName);
        ServerWorld = SceneManager.GetSceneByName(ServerWorldName);

        // can't merge any scenes yet because not loaded in Awake() yet.
        // setup OnSceneLoaded callback and continue there.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // is this for the original scene?
        if (scene == originalScene)
        {
            SceneManager.MergeScenes(originalScene, ClientWorld);
            Debug.Log($"Original scene merged into {ClientWorldName}!");
        }
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(5, 5, 100, 25), "Start Server"))
        {
            //
        }

        if (GUI.Button(new Rect(5, 30, 100, 25), "Start Client"))
        {
        }
    }
}
