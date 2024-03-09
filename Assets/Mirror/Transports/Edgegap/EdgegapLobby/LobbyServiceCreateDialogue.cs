using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
namespace Edgegap
{
    public class LobbyServiceCreateDialogue : EditorWindow
    {
        public Action<string> onLobby;
        public bool waitingCreate;
        public bool waitingStatus;
        private string _name;
        private string _key;
        private string _lastStatus;
        private void OnGUI()
        {
            if (waitingCreate)
            {
                EditorGUILayout.LabelField("Waiting for lobby to create..");
                return;
            }
            if (waitingStatus)
            {
                EditorGUILayout.LabelField("Waiting for lobby to deploy..");
                EditorGUILayout.LabelField($"Latest status: {_lastStatus}");
                return;
            }
            _key = EditorGUILayout.TextField("Edgegap API key", _key);
            EditorGUILayout.LabelField("Your API key won't be saved.");
            if (GUILayout.Button("I have no api key?"))
            {
                Application.OpenURL("https://app.edgegap.com/user-settings?tab=tokens");
            }
            EditorGUILayout.Separator();
            _name = EditorGUILayout.TextField("Lobby Name", _name);
            EditorGUILayout.LabelField("The lobby name must be unique");


            if (GUILayout.Button("Create"))
            {
                if (string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_name))
                {
                    EditorUtility.DisplayDialog("Error", "Key and Name can't be empty.", "Ok");
                    return;
                }

                waitingCreate = true;
                Repaint();

                LobbyApi.CreateAndDeployLobbyService(_key, _name, res =>
                {
                    waitingCreate = false;
                    waitingStatus = true;
                    _lastStatus = res.status;
                    RefreshStatus();
                    Repaint();
                }, error =>
                {
                    EditorUtility.DisplayDialog("Failed to create lobby", $"The following error happened while trying to create (&deploy) a lobby:\n\n{error}", "Ok");
                    waitingCreate = false;
                });
            }

            if (GUILayout.Button("Cancel"))
                Close();
            EditorGUILayout.Separator();

            if (GUILayout.Button("Terminate existing deploy"))
            {
                LobbyApi.TerminateLobbyService(_key, _name, res =>
                {
                    EditorUtility.DisplayDialog("Success", $"The lobby service will start terminating (shutting down the deploy) now", "Ok");
                }, error =>
                {
                    EditorUtility.DisplayDialog("Failed to create lobby", $"The following error happened while trying to create (&deploy) a lobby:\n\n{error}", "Ok");
                    waitingCreate = false;
                });
            }
        }
        private void RefreshStatus()
        {
            // Stop if window is closed
            if (!this)
            {
                return;
            }
            LobbyApi.GetLobbyService(_key, _name, res =>
            {
                if (!res.HasValue)
                {
                    EditorUtility.DisplayDialog("Failed to create lobby", $"The lobby seems to have vanished while waiting for it to deploy.", "Ok");
                    waitingStatus = false;
                    Repaint();
                    return;
                }
                if (!string.IsNullOrEmpty(res.Value.url))
                {
                    onLobby(res.Value.url);
                    Close();
                    return;
                }
                _lastStatus = res.Value.status;
                Repaint();
                Thread.Sleep(100); // :( but this is a lazy editor script, its fiiine
                RefreshStatus();
            }, error =>
            {
                EditorUtility.DisplayDialog("Failed to create lobby", $"The following error happened while trying to create (&deploy) a lobby:\n\n{error}", "Ok");
                waitingStatus = false;
            });
        }
    }
}
#endif
