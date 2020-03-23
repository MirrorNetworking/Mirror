using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SceneField))]
    public class SceneFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty guidProp = property.FindPropertyRelative("assetGuid");
            SerializedProperty pathProp = property.FindPropertyRelative("path");

            SceneAsset sceneObject = GetSceneObject(guidProp.stringValue);

            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                SceneAsset scene = (SceneAsset)EditorGUI.ObjectField(position, label, sceneObject, typeof(SceneAsset), true);

                if (scope.changed)
                {
                    if (scene == null || NotInBuildList(scene))
                    {
                        guidProp.stringValue = "";
                        pathProp.stringValue = "";
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(scene);
                        guidProp.stringValue = AssetDatabase.AssetPathToGUID(path);
                        pathProp.stringValue = path;
                    }
                }
            }
        }

        static bool NotInBuildList(SceneAsset sceneAsset)
        {
            string path = AssetDatabase.GetAssetPath(sceneAsset);

            // can not use SceneManager as it only Gets scenes that are loaded
            bool isValid = EditorBuildSettings.scenes.Any(scene => path == scene.path);
            if (!isValid)
            {
                Debug.LogWarning("The scene " + sceneAsset.name + " cannot be used. To use this scene add it to the build settings for the project");
            }

            return !isValid;
        }

        static SceneAsset GetSceneObject(string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
}
