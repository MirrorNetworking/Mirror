#if ENABLE_UNET
using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityObject = UnityEngine.Object;

namespace UnityEditor
{
    [CustomEditor(typeof(NetworkLobbyManager), true)]
    [CanEditMultipleObjects]
    class NetworkLobbyManagerEditor : NetworkManagerEditor
    {
        SerializedProperty m_ShowLobbyGUIProperty;
        SerializedProperty m_MaxPlayersProperty;
        SerializedProperty m_MaxPlayersPerConnectionProperty;
        SerializedProperty m_MinPlayersProperty;
        SerializedProperty m_LobbyPlayerPrefabProperty;
        SerializedProperty m_GamePlayerPrefabProperty;

        GUIContent m_LobbySceneLabel;
        GUIContent m_PlaySceneLabel;

        GUIContent m_MaxPlayersLabel;
        GUIContent m_MaxPlayersPerConnectionLabel;
        GUIContent m_MinPlayersLabel;

        GUIContent m_ShowLobbyGUILabel;
        GUIContent m_LobbyPlayerPrefabLabel;
        GUIContent m_GamePlayerPrefabLabel;

        bool ShowSlots;

        void InitLobby()
        {
            if (!m_Initialized)
            {
                m_LobbySceneLabel = new GUIContent("Lobby Scene", "The scene loaded for the lobby.");
                m_PlaySceneLabel = new GUIContent("Play Scene", "The scene loaded to play the game.");

                m_MaxPlayersLabel = new GUIContent("Max Players", "The maximum number of players allowed in the lobby.");
                m_MaxPlayersPerConnectionLabel = new GUIContent("Max Players Per Connection", "The maximum number of players that each connection/client can have in the lobby. Defaults to 1.");
                m_MinPlayersLabel = new GUIContent("Minimum Players", "The minimum number of players required to be ready for the game to start. If this is zero then the game can start with any number of players.");

                m_ShowLobbyGUILabel = new GUIContent("Show Lobby GUI", "Enable to display the default lobby UI.");
                m_LobbyPlayerPrefabLabel = new GUIContent("Lobby Player Prefab", "The prefab to use for a player in the Lobby Scene.");
                m_GamePlayerPrefabLabel = new GUIContent("Game Player Prefab", "The prefab to use for a player in the Play Scene.");

                m_ShowLobbyGUIProperty = serializedObject.FindProperty("m_ShowLobbyGUI");
                m_MaxPlayersProperty = serializedObject.FindProperty("m_MaxPlayers");
                m_MaxPlayersPerConnectionProperty = serializedObject.FindProperty("m_MaxPlayersPerConnection");
                m_MinPlayersProperty = serializedObject.FindProperty("m_MinPlayers");
                m_LobbyPlayerPrefabProperty = serializedObject.FindProperty("m_LobbyPlayerPrefab");
                m_GamePlayerPrefabProperty = serializedObject.FindProperty("m_GamePlayerPrefab");

                var lobby = target as NetworkLobbyManager;
                if (lobby == null)
                    return;

                if (lobby.lobbyScene != "")
                {
                    var offlineObj = GetSceneObject(lobby.lobbyScene);
                    if (offlineObj == null)
                    {
                        Debug.LogWarning("LobbyScene '" + lobby.lobbyScene + "' not found. You must repopulate the LobbyScene slot of the NetworkLobbyManager");
                        lobby.lobbyScene = "";
                    }
                }


                if (lobby.playScene != "")
                {
                    var onlineObj = GetSceneObject(lobby.playScene);
                    if (onlineObj == null)
                    {
                        Debug.LogWarning("PlayScene '" + lobby.playScene + "' not found. You must repopulate the PlayScene slot of the NetworkLobbyManager");
                        lobby.playScene = "";
                    }
                }
            }

            Init();
        }

        public override void OnInspectorGUI()
        {
            if (m_DontDestroyOnLoadProperty == null || m_DontDestroyOnLoadLabel == null)
                m_Initialized = false;

            InitLobby();

            var lobby = target as NetworkLobbyManager;
            if (lobby == null)
                return;

            serializedObject.Update();
            EditorGUILayout.PropertyField(m_DontDestroyOnLoadProperty, m_DontDestroyOnLoadLabel);
            EditorGUILayout.PropertyField(m_RunInBackgroundProperty , m_RunInBackgroundLabel);

            if (EditorGUILayout.PropertyField(m_LogLevelProperty))
            {
                LogFilter.currentLogLevel = m_NetworkManager.logLevel;
            }

            ShowLobbyScenes();

            EditorGUILayout.PropertyField(m_ShowLobbyGUIProperty, m_ShowLobbyGUILabel);
            EditorGUILayout.PropertyField(m_MaxPlayersProperty, m_MaxPlayersLabel);
            EditorGUILayout.PropertyField(m_MaxPlayersPerConnectionProperty, m_MaxPlayersPerConnectionLabel);
            EditorGUILayout.PropertyField(m_MinPlayersProperty, m_MinPlayersLabel);
            EditorGUILayout.PropertyField(m_LobbyPlayerPrefabProperty, m_LobbyPlayerPrefabLabel);

            EditorGUI.BeginChangeCheck();
            var newGamPlayer = EditorGUILayout.ObjectField(m_GamePlayerPrefabLabel, lobby.gamePlayerPrefab, typeof(NetworkIdentity), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newGamPlayer == null)
                {
                    m_GamePlayerPrefabProperty.objectReferenceValue = null;
                }
                else
                {
                    var newGamePlayerIdentity = newGamPlayer as NetworkIdentity;
                    if (newGamePlayerIdentity != null)
                    {
                        if (newGamePlayerIdentity.gameObject != lobby.gamePlayerPrefab)
                        {
                            m_GamePlayerPrefabProperty.objectReferenceValue = newGamePlayerIdentity.gameObject;
                        }
                    }
                }
            }

            EditorGUILayout.Separator();

            ShowNetworkInfo();
            ShowSpawnInfo();
            ShowConfigInfo();
            serializedObject.ApplyModifiedProperties();

            ShowDerivedProperties(typeof(NetworkLobbyManager), typeof(NetworkManager));

            if (!Application.isPlaying)
                return;

            EditorGUILayout.Separator();
            ShowLobbySlots();
        }

        protected void ShowLobbySlots()
        {
            var lobby = target as NetworkLobbyManager;
            if (lobby == null)
                return;

            ShowSlots = EditorGUILayout.Foldout(ShowSlots, "LobbySlots");
            if (ShowSlots)
            {
                EditorGUI.indentLevel += 1;
                foreach (var slot in lobby.lobbySlots)
                {
                    if (slot == null)
                        continue;

                    EditorGUILayout.ObjectField("Slot " + slot.slot, slot.gameObject, typeof(UnityObject), true);
                }
                EditorGUI.indentLevel -= 1;
            }
        }

        void SetLobbyScene(NetworkLobbyManager lobby, string sceneName)
        {
            var prop = serializedObject.FindProperty("m_LobbyScene");
            prop.stringValue = sceneName;

            var offlineProp = serializedObject.FindProperty("m_OfflineScene");
            offlineProp.stringValue = sceneName;

            EditorUtility.SetDirty(lobby);
        }

        void SetPlayScene(NetworkLobbyManager lobby, string sceneName)
        {
            var prop = serializedObject.FindProperty("m_PlayScene");
            prop.stringValue = sceneName;

            var onlineProp = serializedObject.FindProperty("m_OnlineScene");
            onlineProp.stringValue = ""; // this is set to empty deliberately to prevent base class functionality from interfering with LobbyManager

            EditorUtility.SetDirty(lobby);
        }

        protected void ShowLobbyScenes()
        {
            var lobby = target as NetworkLobbyManager;
            if (lobby == null)
                return;

            var offlineObj = GetSceneObject(lobby.lobbyScene);

            EditorGUI.BeginChangeCheck();
            var newOfflineScene = EditorGUILayout.ObjectField(m_LobbySceneLabel, offlineObj, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newOfflineScene == null)
                {
                    SetLobbyScene(lobby, "");
                }
                else
                {
                    if (newOfflineScene.name != lobby.offlineScene)
                    {
                        var sceneObj = GetSceneObject(newOfflineScene.name);
                        if (sceneObj == null)
                        {
                            Debug.LogWarning("The scene " + newOfflineScene.name + " cannot be used. To use this scene add it to the build settings for the project");
                        }
                        else
                        {
                            SetLobbyScene(lobby, newOfflineScene.name);
                        }
                    }
                }
            }

            var onlineObj = GetSceneObject(lobby.playScene);

            EditorGUI.BeginChangeCheck();
            var newOnlineScene = EditorGUILayout.ObjectField(m_PlaySceneLabel, onlineObj, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (newOnlineScene == null)
                {
                    SetPlayScene(lobby, "");
                }
                else
                {
                    if (newOnlineScene.name != m_NetworkManager.onlineScene)
                    {
                        var sceneObj = GetSceneObject(newOnlineScene.name);
                        if (sceneObj == null)
                        {
                            Debug.LogWarning("The scene " + newOnlineScene.name + " cannot be used. To use this scene add it to the build settings for the project");
                        }
                        else
                        {
                            SetPlayScene(lobby, newOnlineScene.name);
                        }
                    }
                }
            }
        }
    }
}
#endif // ENABLE_UNET
