using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Mirror
{
    [CustomEditor(typeof(NetworkManager), true)]
    [CanEditMultipleObjects]
    public class NetworkManagerEditor : Editor
    {
        protected SerializedProperty dontDestroyOnLoadProperty;
        protected SerializedProperty runInBackgroundProperty;
        SerializedProperty networkAddressProperty;

        protected SerializedProperty showDebugMessagesProperty;

        SerializedProperty playerPrefabProperty;
        SerializedProperty autoCreatePlayerProperty;
        SerializedProperty playerSpawnMethodProperty;
        SerializedProperty spawnListProperty;

        GUIContent showNetworkLabel;
        GUIContent showSpawnLabel;

        GUIContent offlineSceneLabel;
        GUIContent onlineSceneLabel;
        protected GUIContent dontDestroyOnLoadLabel;
        protected GUIContent runInBackgroundLabel;
        protected GUIContent showDebugMessagesLabel;

        GUIContent maxConnectionsLabel;
        GUIContent networkAddressLabel;

        GUIContent playerPrefabLabel;
        GUIContent autoCreatePlayerLabel;
        GUIContent playerSpawnMethodLabel;

        ReorderableList spawnList;

        protected bool initialized;

        protected NetworkManager networkManager;

        protected void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            networkManager = target as NetworkManager;

            showNetworkLabel = new GUIContent("Network Info", "Network host settings");
            showSpawnLabel = new GUIContent("Spawn Info", "Registered spawnable objects");
            offlineSceneLabel = new GUIContent("Offline Scene", "The scene loaded when the network goes offline (disconnected from server)");
            onlineSceneLabel = new GUIContent("Online Scene", "The scene loaded when the network comes online (connected to server)");
            dontDestroyOnLoadLabel = new GUIContent("Don't Destroy on Load", "Enable to persist the NetworkManager across scene changes.");
            runInBackgroundLabel = new GUIContent("Run in Background", "Enable to ensure that the application runs when it does not have focus.\n\nThis is required when testing multiple instances on a single machine, but not recommended for shipping on mobile platforms.");
            showDebugMessagesLabel = new GUIContent("Show Debug Messages", "Enable to show Debug log messages.");

            maxConnectionsLabel  = new GUIContent("Max Connections", "Maximum number of network connections");
            networkAddressLabel = new GUIContent("Network Address", "The network address currently in use.");
            playerPrefabLabel = new GUIContent("Player Prefab", "The default prefab to be used to create player objects on the server.");
            autoCreatePlayerLabel = new GUIContent("Auto Create Player", "Enable to automatically create player objects on connect and on Scene change.");
            playerSpawnMethodLabel = new GUIContent("Player Spawn Method", "How to determine which NetworkStartPosition to spawn players at, from all NetworkStartPositions in the Scene.\n\nRandom chooses a random NetworkStartPosition.\n\nRound Robin chooses the next NetworkStartPosition on a round-robin basis.");

            // top-level properties
            dontDestroyOnLoadProperty = serializedObject.FindProperty("dontDestroyOnLoad");
            runInBackgroundProperty = serializedObject.FindProperty("runInBackground");
            showDebugMessagesProperty = serializedObject.FindProperty("showDebugMessages");

            // network foldout properties
            networkAddressProperty = serializedObject.FindProperty("networkAddress");

            // spawn foldout properties
            playerPrefabProperty = serializedObject.FindProperty("playerPrefab");
            autoCreatePlayerProperty = serializedObject.FindProperty("autoCreatePlayer");
            playerSpawnMethodProperty = serializedObject.FindProperty("playerSpawnMethod");
            spawnListProperty = serializedObject.FindProperty("spawnPrefabs");

            spawnList = new ReorderableList(serializedObject, spawnListProperty);
            spawnList.drawHeaderCallback = DrawHeader;
            spawnList.drawElementCallback = DrawChild;
            spawnList.onReorderCallback = Changed;
            spawnList.onRemoveCallback = RemoveButton;
            spawnList.onChangedCallback = Changed;
            spawnList.onReorderCallback = Changed;
            spawnList.onAddCallback = AddButton;
            spawnList.elementHeight = 16; // this uses a 16x16 icon. other sizes make it stretch.
        }

        static void ShowPropertySuffix(GUIContent content, SerializedProperty prop, string suffix)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, content);
            GUILayout.Label(suffix, EditorStyles.miniLabel, GUILayout.Width(64));
            EditorGUILayout.EndHorizontal();
        }

        protected void ShowSpawnInfo()
        {
            playerPrefabProperty.isExpanded = EditorGUILayout.Foldout(playerPrefabProperty.isExpanded, showSpawnLabel);
            if (!playerPrefabProperty.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel += 1;

            EditorGUILayout.PropertyField(playerPrefabProperty, playerPrefabLabel);
            EditorGUILayout.PropertyField(autoCreatePlayerProperty, autoCreatePlayerLabel);
            EditorGUILayout.PropertyField(playerSpawnMethodProperty, playerSpawnMethodLabel);

            EditorGUI.BeginChangeCheck();
            spawnList.DoLayoutList();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.indentLevel -= 1;
        }

        protected SceneAsset GetSceneObject(string sceneObjectName)
        {
            if (string.IsNullOrEmpty(sceneObjectName))
            {
                return null;
            }

            foreach (var editorScene in EditorBuildSettings.scenes)
            {
                var sceneNameWithoutExtension = Path.GetFileNameWithoutExtension(editorScene.path);
                if (sceneNameWithoutExtension == sceneObjectName)
                {
                    return AssetDatabase.LoadAssetAtPath(editorScene.path, typeof(SceneAsset)) as SceneAsset;
                }
            }
            Debug.LogWarning("Scene [" + sceneObjectName + "] cannot be used with networking. Add this scene to the 'Scenes in the Build' in build settings.");
            return null;
        }

        protected void ShowNetworkInfo()
        {
            networkAddressProperty.isExpanded = EditorGUILayout.Foldout(networkAddressProperty.isExpanded, showNetworkLabel);
            if (!networkAddressProperty.isExpanded)
            {
                return;
            }
            EditorGUI.indentLevel += 1;

            EditorGUILayout.PropertyField(networkAddressProperty, networkAddressLabel);

            var maxConn = serializedObject.FindProperty("maxConnections");
            ShowPropertySuffix(maxConnectionsLabel, maxConn, "connections");

            EditorGUI.indentLevel -= 1;
        }

        protected void ShowScenes()
        {
            var offlineObj = GetSceneObject(networkManager.offlineScene);
            var newOfflineScene = EditorGUILayout.ObjectField(offlineSceneLabel, offlineObj, typeof(SceneAsset), false);
            if (newOfflineScene == null)
            {
                var prop = serializedObject.FindProperty("offlineScene");
                prop.stringValue = "";
                EditorUtility.SetDirty(target);
            }
            else
            {
                if (newOfflineScene.name != networkManager.offlineScene)
                {
                    var sceneObj = GetSceneObject(newOfflineScene.name);
                    if (sceneObj == null)
                    {
                        Debug.LogWarning("The scene " + newOfflineScene.name + " cannot be used. To use this scene add it to the build settings for the project");
                    }
                    else
                    {
                        var prop = serializedObject.FindProperty("offlineScene");
                        prop.stringValue = newOfflineScene.name;
                        EditorUtility.SetDirty(target);
                    }
                }
            }

            var onlineObj = GetSceneObject(networkManager.onlineScene);
            var newOnlineScene = EditorGUILayout.ObjectField(onlineSceneLabel, onlineObj, typeof(SceneAsset), false);
            if (newOnlineScene == null)
            {
                var prop = serializedObject.FindProperty("onlineScene");
                prop.stringValue = "";
                EditorUtility.SetDirty(target);
            }
            else
            {
                if (newOnlineScene.name != networkManager.onlineScene)
                {
                    var sceneObj = GetSceneObject(newOnlineScene.name);
                    if (sceneObj == null)
                    {
                        Debug.LogWarning("The scene " + newOnlineScene.name + " cannot be used. To use this scene add it to the build settings for the project");
                    }
                    else
                    {
                        var prop = serializedObject.FindProperty("onlineScene");
                        prop.stringValue = newOnlineScene.name;
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }

        protected void ShowDerivedProperties(Type baseType, Type superType)
        {
            bool first = true;

            SerializedProperty property = serializedObject.GetIterator();
            bool expanded = true;
            while (property.NextVisible(expanded))
            {
                // ignore properties from base class.
                var f = baseType.GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var p = baseType.GetProperty(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (f == null && superType != null)
                {
                    f = superType.GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (p == null && superType != null)
                {
                    p = superType.GetProperty(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (f == null && p == null)
                {
                    if (first)
                    {
                        first = false;
                        EditorGUI.BeginChangeCheck();
                        serializedObject.Update();

                        EditorGUILayout.Separator();
                    }
                    EditorGUILayout.PropertyField(property, true);
                    expanded = false;
                }
            }
            if (!first)
            {
                serializedObject.ApplyModifiedProperties();
                EditorGUI.EndChangeCheck();
            }
        }

        public override void OnInspectorGUI()
        {
            if (dontDestroyOnLoadProperty == null || dontDestroyOnLoadLabel == null || showDebugMessagesLabel == null)
                initialized = false;

            Init();

            serializedObject.Update();
            EditorGUILayout.PropertyField(dontDestroyOnLoadProperty, dontDestroyOnLoadLabel);
            EditorGUILayout.PropertyField(runInBackgroundProperty, runInBackgroundLabel);

            if (EditorGUILayout.PropertyField(showDebugMessagesProperty, showDebugMessagesLabel))
            {
                LogFilter.Debug = networkManager.showDebugMessages;
            }

            ShowScenes();
            ShowNetworkInfo();
            ShowSpawnInfo();
            serializedObject.ApplyModifiedProperties();

            ShowDerivedProperties(typeof(NetworkManager), null);
        }

        static void DrawHeader(Rect headerRect)
        {
            GUI.Label(headerRect, "Registered Spawnable Prefabs:");
        }

        internal void DrawChild(Rect r, int index, bool isActive, bool isFocused)
        {
            SerializedProperty prefab = spawnListProperty.GetArrayElementAtIndex(index);
            GameObject go = (GameObject)prefab.objectReferenceValue;

            GUIContent label;
            if (go == null)
            {
                label = new GUIContent("Empty", "Drag a prefab with a NetworkIdentity here");
            }
            else
            {
                var identity = go.GetComponent<NetworkIdentity>();
                label = new GUIContent(go.name, identity != null ? "AssetId: [" + identity.assetId + "]" : "No Network Identity");
            }

            var newGameObject = (GameObject)EditorGUI.ObjectField(r, label, go, typeof(GameObject), false);

            if (newGameObject != go)
            {
                if (newGameObject != null && !newGameObject.GetComponent<NetworkIdentity>())
                {
                    Debug.LogError("Prefab " + newGameObject + " cannot be added as spawnable as it doesn't have a NetworkIdentity.");
                    return;
                }
                prefab.objectReferenceValue = newGameObject;
            }
        }

        internal void Changed(ReorderableList list)
        {
            EditorUtility.SetDirty(target);
        }

        internal void AddButton(ReorderableList list)
        {
            spawnListProperty.arraySize += 1;
            list.index = spawnListProperty.arraySize - 1;

            var obj = spawnListProperty.GetArrayElementAtIndex(spawnListProperty.arraySize - 1);
            if (obj.objectReferenceValue != null)
                obj.objectReferenceValue = null;

            spawnList.index = spawnList.count - 1;

            Changed(list);
        }

        internal void RemoveButton(ReorderableList list)
        {
            spawnListProperty.DeleteArrayElementAtIndex(spawnList.index);
            if (list.index >= spawnListProperty.arraySize)
            {
                list.index = spawnListProperty.arraySize - 1;
            }
        }
    }
}