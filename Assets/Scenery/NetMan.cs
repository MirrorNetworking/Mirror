using UnityEngine;
using UnityEngine.SceneManagement;

public class NetMan : MonoBehaviour
{
    // Server/Client worlds for easy access
    public const string ClientWorldName = "ClientWorld";
    public const string ServerWorldName = "ServerWorld";
    public static Scene ClientWorld;
    public static Scene ServerWorld;
    static Scene originalScene;
    static string originalScenePath;
    static bool initialized = false;

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
        // we only do world initialization ONCE
        // duplicating a scene would call Awake here again.
        if (initialized) return;
        initialized = true;

        // remember the original scene
        originalScene = SceneManager.GetActiveScene();
        originalScenePath = originalScene.path;

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

        // duplicate original scene. we'll move it into ClientWorld
        // -> additive, otherwise we would just reload it
        SceneManager.LoadScene(originalScene.path, LoadSceneMode.Additive);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"OnSceneLoaded: {scene.name} {scene.path}");

        // is this for the original scene?
        if (scene == originalScene)
        {
            Debug.Log($"original scene loaded");

            // merge original into ServerWorld
            SceneManager.MergeScenes(scene, ServerWorld);
            Debug.Log($"Original scene merged into {ServerWorldName}!");
        }
        // not the same scene, but same path. so it's the duplicate.
        else if (scene.path == originalScenePath)
        {
            Debug.Log($"duplicated scene loaded");

            // merge duplicate into serverworld
            SceneManager.MergeScenes(scene, ClientWorld);
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
