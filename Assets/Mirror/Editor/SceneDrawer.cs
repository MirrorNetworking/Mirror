using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SceneAttribute))]
    [System.Obsolete("Use " + nameof(SceneField) + " Instead")]
    public class SceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            
            style.normal.textColor = Color.red; // warning color
            EditorGUI.LabelField(position, label.text, "[Obslete] Replace 'string' field with 'SceneField'", style);
        }
    }

    [CustomPropertyDrawer(typeof(SceneField))]
    public class SceneFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty pathProp = property.FindPropertyRelative("path");

            SceneAsset sceneObject = GetSceneObject(pathProp.stringValue);

            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                SceneAsset scene = (SceneAsset)EditorGUI.ObjectField(position, label, sceneObject, typeof(SceneAsset), true);

                if (scope.changed)
                {
                    // TODO: if not in build list, show dialog and ask user if we should add it.
                    if (scene == null || NotInBuildList(scene))
                    {
                        pathProp.stringValue = "";
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(scene);
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

        static SceneAsset GetSceneObject(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        }
    }
}
