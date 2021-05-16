// Bootstrap ServerWorld, ClientWorld just like in ECS.
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    // Server/Client worlds for easy access
    public const string ClientWorldName = "ClientWorld";
    public const string ServerWorldName = "ServerWorld";

    public static Scene ClientWorld;
    public static Scene ServerWorld;

    // original scene is not public. it will become the server scene after merge.
    static Scene originalScene;

    // this will always be the original scene's path.
    public static string originalScenePath;

    static bool initialized;

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
        //Debug.Log($"OnSceneLoaded: {scene.name} {scene.path}");

        // is this for the original scene?
        if (scene == originalScene)
        {
            // merge original into ServerWorld
            //Debug.Log($"original scene loaded");
            SceneManager.MergeScenes(scene, ServerWorld);
            Debug.Log($"Original scene merged into {ServerWorldName}!");
        }
        // not the same scene, but same path. so it's the duplicate.
        else if (scene.path == originalScenePath)
        {
            // merge duplicate into serverworld
            //Debug.Log($"duplicated scene loaded");
            SceneManager.MergeScenes(scene, ClientWorld);
            Debug.Log($"Original scene merged into {ClientWorldName}!");
        }
    }
}
