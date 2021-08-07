using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomPropertyDrawer(typeof(SceneAttribute))]
    public class SceneDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                SceneAsset sceneObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(property.stringValue);

                if (sceneObject == null && !string.IsNullOrEmpty(property.stringValue))
                {
                    // try to load it from the build settings for legacy compatibility
                    sceneObject = GetBuildSettingsSceneObject(property.stringValue);
                }
                if (sceneObject == null && !string.IsNullOrEmpty(property.stringValue))
                {
                    Debug.LogError($"Could not find scene {property.stringValue} in {property.propertyPath}, assign the proper scenes in your NetworkManager");
                }
                SceneAsset scene = (SceneAsset)EditorGUI.ObjectField(position, label, sceneObject, typeof(SceneAsset), true);

                property.stringValue = AssetDatabase.GetAssetPath(scene);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use [Scene] with strings.");
            }
        }

        protected SceneAsset GetBuildSettingsSceneObject(string sceneName)
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path);
                if (sceneAsset!= null && sceneAsset.name == sceneName)
                {
                    return sceneAsset;
                }
            }
            return null;
        }
    }
}
