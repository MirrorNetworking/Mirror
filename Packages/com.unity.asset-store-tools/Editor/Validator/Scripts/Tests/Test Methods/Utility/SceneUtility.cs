using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssetStoreTools.Validator.TestMethods.Utility
{ 
    internal static class SceneUtility
    {
        public static string CurrentScenePath => SceneManager.GetActiveScene().path;

        public static Scene OpenScene(string scenePath)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            else
                return EditorSceneManager.OpenScene(scenePath);
        }

        public static GameObject[] GetRootGameObjects()
        {
            return SceneManager.GetSceneByPath(CurrentScenePath).GetRootGameObjects();
        }
    }
}