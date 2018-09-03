using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Mirror
{
    [CustomEditor(typeof(NetworkManagerHUD), true)]
    [CanEditMultipleObjects]
    public class NetworkManagerHUDEditor : Editor
    {
        SerializedProperty m_ShowGUIProperty;
        SerializedProperty m_OffsetXProperty;
        SerializedProperty m_OffsetYProperty;

        GUIContent m_ShowNetworkLabel;
        GUIContent m_ShowServerLabel;
        GUIContent m_ShowServerConnectionsLabel;
        GUIContent m_ShowServerObjectsLabel;
        GUIContent m_ShowClientLabel;
        GUIContent m_ShowClientObjectsLabel;
        GUIContent m_ShowControlsLabel;
        GUIContent m_ShowRuntimeGuiLabel;
        GUIContent m_OffsetXLabel;
        GUIContent m_OffsetYLabel;

        bool m_ShowServer;
        bool m_ShowServerConnections;
        bool m_ShowServerObjects;
        bool m_ShowClient;
        bool m_ShowClientObjects;

        bool m_ShowControls;

        bool m_Initialized;


        NetworkManagerHUD m_ManagerHud;
        NetworkManager m_Manager;

        void Init()
        {
            if (m_Initialized)
            {
                if (m_ShowGUIProperty == null)
                {
                    // initialize again.. something got broken
                }
                else
                {
                    return;
                }
            }
            m_Initialized = true;
            m_ManagerHud = target as NetworkManagerHUD;
            if (m_ManagerHud != null)
            {
                m_Manager = m_ManagerHud.manager;
            }

            m_ShowGUIProperty = serializedObject.FindProperty("showGUI");
            m_OffsetXProperty = serializedObject.FindProperty("offsetX");
            m_OffsetYProperty = serializedObject.FindProperty("offsetY");

            m_ShowServerLabel = new GUIContent("Server Info", "Details of internal server state");
            m_ShowServerConnectionsLabel = new GUIContent("Server Connections", "List of local and remote network connections to the server");
            m_ShowServerObjectsLabel = new GUIContent("Server Objects", "Networked objects spawned by the server");
            m_ShowClientLabel = new GUIContent("Client Info", "Details of internal client state");
            m_ShowClientObjectsLabel = new GUIContent("Client Objects", "Networked objects created on the client");
            m_ShowControlsLabel = new GUIContent("Runtime Controls", "Buttons for controlling network state at runtime");
            m_ShowRuntimeGuiLabel = new GUIContent("Show Runtime GUI", "Show the default network control GUI when the game is running");
            m_OffsetXLabel = new GUIContent("GUI Horizontal Offset", "Horizontal offset of runtime GUI");
            m_OffsetYLabel = new GUIContent("GUI Vertical Offset", "Vertical offset of runtime GUI");
        }

        List<bool> m_ShowDetailForConnections;
        List<bool> m_ShowPlayersForConnections;
        List<bool> m_ShowVisibleForConnections;
        List<bool> m_ShowOwnedForConnections;

        void ShowServerConnections()
        {
            m_ShowServerConnections = EditorGUILayout.Foldout(m_ShowServerConnections, m_ShowServerConnectionsLabel);
            if (m_ShowServerConnections)
            {
                EditorGUI.indentLevel += 1;

                // ensure arrays of bools exists and are large enough
                if (m_ShowDetailForConnections == null)
                {
                    m_ShowDetailForConnections = new List<bool>();
                    m_ShowPlayersForConnections = new List<bool>();
                    m_ShowVisibleForConnections = new List<bool>();
                    m_ShowOwnedForConnections = new List<bool>();
                }
                while (m_ShowDetailForConnections.Count < NetworkServer.connections.Count)
                {
                    m_ShowDetailForConnections.Add(false);
                    m_ShowPlayersForConnections.Add(false);
                    m_ShowVisibleForConnections.Add(false);
                    m_ShowOwnedForConnections.Add(false);
                }

                // all connections
                int index = 0;
                foreach (var con in NetworkServer.connections)
                {
                    if (con == null)
                    {
                        index += 1;
                        continue;
                    }

                    m_ShowDetailForConnections[index] = EditorGUILayout.Foldout(m_ShowDetailForConnections[index], "Conn: " + con.connectionId + " (" + con.address + ")");
                    if (m_ShowDetailForConnections[index])
                    {
                        EditorGUI.indentLevel += 1;

                        m_ShowPlayersForConnections[index] = EditorGUILayout.Foldout(m_ShowPlayersForConnections[index], "Players");
                        if (m_ShowPlayersForConnections[index])
                        {
                            EditorGUI.indentLevel += 1;
                            foreach (var player in con.playerControllers)
                            {
                                EditorGUILayout.ObjectField("Player: " + player.playerControllerId, player.gameObject, typeof(GameObject), true);
                            }
                            EditorGUI.indentLevel -= 1;
                        }

                        m_ShowVisibleForConnections[index] = EditorGUILayout.Foldout(m_ShowVisibleForConnections[index], "Visible Objects");
                        if (m_ShowVisibleForConnections[index])
                        {
                            EditorGUI.indentLevel += 1;
                            foreach (var v in con.visList)
                            {
                                EditorGUILayout.ObjectField("NetId: " + v.netId, v, typeof(NetworkIdentity), true);
                            }
                            EditorGUI.indentLevel -= 1;
                        }

                        if (con.clientOwnedObjects != null)
                        {
                            m_ShowOwnedForConnections[index] = EditorGUILayout.Foldout(m_ShowOwnedForConnections[index], "Owned Objects");
                            if (m_ShowOwnedForConnections[index])
                            {
                                EditorGUI.indentLevel += 1;
                                foreach (var netId in con.clientOwnedObjects)
                                {
                                    var obj = NetworkServer.FindLocalObject(netId);
                                    EditorGUILayout.ObjectField("Owned: " + netId, obj, typeof(NetworkIdentity), true);
                                }
                                EditorGUI.indentLevel -= 1;
                            }
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                    index += 1;
                }
                EditorGUI.indentLevel -= 1;
            }
        }

        void ShowServerObjects()
        {
            m_ShowServerObjects = EditorGUILayout.Foldout(m_ShowServerObjects, m_ShowServerObjectsLabel);
            if (m_ShowServerObjects)
            {
                EditorGUI.indentLevel += 1;

                foreach (var obj in NetworkServer.objects)
                {
                    string first = "NetId:" + obj.Key;
                    GameObject value = null;
                    if (obj.Value != null)
                    {
                        NetworkIdentity uv = obj.Value.GetComponent<NetworkIdentity>();
                        first += " SceneId:" + uv.sceneId;
                        value = obj.Value.gameObject;
                    }
                    EditorGUILayout.ObjectField(first, value, typeof(GameObject), true);
                }
                EditorGUI.indentLevel -= 1;
            }
        }

        void ShowServerInfo()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            m_ShowServer = EditorGUILayout.Foldout(m_ShowServer, m_ShowServerLabel);
            if (!m_ShowServer)
            {
                return;
            }

            EditorGUI.indentLevel += 1;
            EditorGUILayout.BeginVertical();
            ShowServerConnections();
            ShowServerObjects();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel -= 1;
        }

        void ShowClientObjects()
        {
            m_ShowClientObjects = EditorGUILayout.Foldout(m_ShowClientObjects, m_ShowClientObjectsLabel);
            if (m_ShowClientObjects)
            {
                EditorGUI.indentLevel += 1;
                foreach (var obj in ClientScene.objects)
                {
                    string first = "NetId:" + obj.Key;
                    GameObject value = null;
                    if (obj.Value != null)
                    {
                        NetworkIdentity id = obj.Value.GetComponent<NetworkIdentity>();
                        first += " SceneId:" + id.sceneId;
                        value = obj.Value.gameObject;
                    }
                    EditorGUILayout.ObjectField(first, value, typeof(GameObject), true);
                }
                EditorGUI.indentLevel -= 1;
            }
        }

        void ShowClientInfo()
        {
            if (!NetworkClient.active)
            {
                return;
            }

            m_ShowClient = EditorGUILayout.Foldout(m_ShowClient, m_ShowClientLabel);
            if (!m_ShowClient)
            {
                return;
            }

            EditorGUI.indentLevel += 1;
            EditorGUILayout.BeginVertical();

            int count = 0;
            foreach (var cl in NetworkClient.allClients)
            {
                if (cl.connection == null)
                {
                    EditorGUILayout.TextField("client " + count + ": ", cl.GetType().Name + " Conn: null");
                }
                else
                {
                    EditorGUILayout.TextField("client " + count + ":" ,  cl.GetType().Name + " Conn: " + cl.connection);
                    EditorGUI.indentLevel += 1;
                    foreach (var p in cl.connection.playerControllers)
                    {
                        EditorGUILayout.LabelField("Player", p.ToString());
                    }
                    EditorGUI.indentLevel -= 1;
                }
                count++;
            }

            ShowClientObjects();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel -= 1;
        }

        static UnityObject GetSceneObject(string sceneObjectName)
        {
            if (string.IsNullOrEmpty(sceneObjectName))
            {
                return null;
            }

            foreach (var editorScene in EditorBuildSettings.scenes)
            {
                if (editorScene.path.IndexOf(sceneObjectName) != -1)
                {
                    return AssetDatabase.LoadAssetAtPath(editorScene.path, typeof(UnityObject));
                }
            }
            return null;
        }

        static Rect GetButtonRect()
        {
            Rect rect = EditorGUILayout.GetControlRect();
            float endcap = rect.width / 6;
            Rect newRect = new Rect(rect.xMin + endcap, rect.yMin, rect.width - (endcap * 2), rect.height);
            return newRect;
        }

        void ShowControls()
        {
            m_ShowControls = EditorGUILayout.Foldout(m_ShowControls, m_ShowControlsLabel);
            if (!m_ShowControls)
            {
                return;
            }

            if (!string.IsNullOrEmpty(NetworkManager.networkSceneName))
            {
                EditorGUILayout.ObjectField("Current Scene:", GetSceneObject(NetworkManager.networkSceneName), typeof(UnityObject), true);
            }
            EditorGUILayout.Separator();

            if (!NetworkClient.active && !NetworkServer.active)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(false, "LAN Host", EditorStyles.miniButton))
                {
                    m_Manager.StartHost();
                }
                if (GUILayout.Toggle(false, "LAN Server", EditorStyles.miniButton))
                {
                    m_Manager.StartServer();
                }
                if (GUILayout.Toggle(false, "LAN Client", EditorStyles.miniButton))
                {
                    m_Manager.StartClient();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (NetworkClient.active && !ClientScene.ready)
            {
                if (GUI.Button(GetButtonRect(), "Client Ready"))
                {
                    ClientScene.Ready(m_Manager.client.connection);

                    if (ClientScene.localPlayers.Count == 0)
                    {
                        ClientScene.AddPlayer(0);
                    }
                }
            }

            if (NetworkServer.active || NetworkClient.active)
            {
                if (GUI.Button(GetButtonRect(), "Stop"))
                {
                    m_Manager.StopServer();
                    m_Manager.StopClient();
                }
            }

            EditorGUILayout.Separator();
        }

        public override void OnInspectorGUI()
        {
            Init();

            serializedObject.Update();
            EditorGUILayout.PropertyField(m_ShowGUIProperty, m_ShowRuntimeGuiLabel);

            if (m_ManagerHud.showGUI)
            {
                EditorGUI.indentLevel += 1;
                EditorGUILayout.PropertyField(m_OffsetXProperty, m_OffsetXLabel);
                EditorGUILayout.PropertyField(m_OffsetYProperty, m_OffsetYLabel);
                EditorGUI.indentLevel -= 1;
            }
            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            ShowControls();
            ShowServerInfo();
            ShowClientInfo();
        }
    }
}
