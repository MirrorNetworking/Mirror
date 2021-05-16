// Bootstrap ServerWorld, ClientWorld just like in ECS.
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    // Server/Client worlds for easy access
    public static Scene ClientWorld;
    public static Scene ServerWorld;

    static bool initialized;

    void Awake()
    {
        // we only do world initialization ONCE
        // duplicating a scene would call Awake here again.
        if (initialized) return;
        initialized = true;

        // original scene has to become the ClientWorld and it has to keep the
        // same name. renaming it to ClientWorld would not load lighting data,
        // and everything look pretty dark:
        // https://forum.unity.com/threads/scenemanager-mergescenes-leaves-lighting-data.949203/
        // TODO figure out how to load light data for modified scene name
        ClientWorld = SceneManager.GetActiveScene();

        // create a scene with [ServerWorld] suffix
        string serverWorldName = ClientWorld.name + " [ServerWorld]";
        SceneManager.CreateScene(serverWorldName);
        ServerWorld = SceneManager.GetSceneByName(serverWorldName);

        // can't merge any scenes yet because not loaded in Awake() yet.
        // setup OnSceneLoaded callback and continue there.
        SceneManager.sceneLoaded += OnSceneLoaded;

        // duplicate original scene. we'll move it into ClientWorld
        // -> additive, otherwise we would just reload it
        SceneManager.LoadScene(ClientWorld.path, LoadSceneMode.Additive);
    }

    // helper function to remove all components in a scene
    static void RemoveAll<T>(Scene scene) where T : Component
    {
        foreach (T component in FindObjectsOfType<T>())
            if (component.gameObject.scene == scene)
                Destroy(component);
    }

    // remove audio listener and main camera from server world
    static void Strip(Scene scene)
    {
        Debug.Log($"Bootstrap: stripping {scene.name}");

        // remove all audio listeners in that scene
        RemoveAll<AudioListener>(scene);
        RemoveAll<Camera>(scene);
        RemoveAll<Light>(scene);
        RemoveAll<Renderer>(scene);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"OnSceneLoaded: {scene.name} {scene.path}");

        // is this the original scene?
        if (scene == ClientWorld)
        {
            Debug.Log($"original scene loaded");
        }
        // additive & not the same scene, but same path so it's the duplicate.
        else if (mode == LoadSceneMode.Additive && scene.path == ClientWorld.path)
        {
            Debug.Log($"duplicated scene loaded");

            // merge the duplicate into it
            SceneManager.MergeScenes(scene, ServerWorld);
            Debug.Log($"Bootstrap: duplicate scene merged into {ServerWorld.name}!");

            // strip it
            Strip(ServerWorld);
        }
    }
}
