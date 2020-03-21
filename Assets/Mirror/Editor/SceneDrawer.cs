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

                if (sceneObject == null)
                { 
                    // try to load it from the build settings for legacy compatibility
                    sceneObject = GetBuildSettingsSceneObject(property);
                }
                SceneAsset scene = (SceneAsset)EditorGUI.ObjectField(position, label, sceneObject, typeof(SceneAsset), true);
                property.stringValue = AssetDatabase.GetAssetPath(scene);
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use [Scene] with strings.");
            }
        }

        protected SceneAsset GetBuildSettingsSceneObject(SerializedProperty property)
        {
            string sceneObjectName = property.stringValue;

            if (string.IsNullOrEmpty(sceneObjectName))
            {
                return null;
            }

            foreach (EditorBuildSettingsScene editorScene in EditorBuildSettings.scenes)
            {
                // hack,  but retains backwards compatibility
                if (editorScene.path.IndexOf(sceneObjectName) != -1)
                {
                    return AssetDatabase.LoadAssetAtPath<SceneAsset>(editorScene.path);
                }
            }
            Debug.LogError($"Scene {sceneObjectName} not found in {property.propertyPath}");
            return null;
        }
    }
}
