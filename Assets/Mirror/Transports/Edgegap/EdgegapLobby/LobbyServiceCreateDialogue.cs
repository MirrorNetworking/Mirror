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

        private void Awake()
        {
            minSize = maxSize = new Vector2(450, 300);
            titleContent = new GUIContent("Edgegap Lobby Service Setup");
        }

#if !UNITY_SERVER
        private void OnGUI()
        {
            if (waitingCreate)
            {
                EditorGUILayout.LabelField("Waiting for lobby to create . . . ");
                return;
            }
            if (waitingStatus)
            {
                EditorGUILayout.LabelField("Waiting for lobby to deploy . . . ");
                EditorGUILayout.LabelField($"Latest status: {_lastStatus}");
                return;
            }
            _key = EditorGUILayout.TextField("Edgegap API key", _key);
            LobbyApi.TrimApiKey(ref _key);
            EditorGUILayout.HelpBox(new GUIContent("Your API key won't be saved."));
            if (GUILayout.Button("I have no api key?"))
            {
                Application.OpenURL("https://app.edgegap.com/user-settings?tab=tokens");
            }
            EditorGUILayout.Separator();
            EditorGUILayout.HelpBox("There's currently a bug where lobby names longer than 5 characters can fail to deploy correctly and will return a \"503 Service Temporarily Unavailable\"\nIt's recommended to limit your lobby names to 4-5 characters for now", UnityEditor.MessageType.Warning);
            _name = EditorGUILayout.TextField("Lobby Name", _name);
            EditorGUILayout.HelpBox(new GUIContent("The lobby name is your games identifier for the lobby service"));

            if (GUILayout.Button("Create"))
            {
                if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_name))
                {
                    EditorUtility.DisplayDialog("Error", "Key and Name can't be empty.", "Ok");
                }
                else
                {
                    waitingCreate = true;
                    Repaint();

                    LobbyApi.CreateAndDeployLobbyService(_key.Trim(), _name.Trim(), res =>
                    {
                        waitingCreate = false;
                        waitingStatus = true;
                        _lastStatus = res.status;
                        RefreshStatus();
                        Repaint();
                    }, error =>
                    {
                        EditorUtility.DisplayDialog("Failed to create lobby", $"The following error happened while trying to create (&deploy) the lobby service:\n\n{error}", "Ok");
                        waitingCreate = false;
                    });
                    return;
                }

            }

            if (GUILayout.Button("Cancel"))
                Close();

            EditorGUILayout.HelpBox(new GUIContent("Note: If you forgot your lobby url simply re-create it with the same name!\nIt will re-use the existing lobby service"));
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();


            if (GUILayout.Button("Terminate existing deploy"))
            {

                if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_name))
                {
                    EditorUtility.DisplayDialog("Error", "Key and Name can't be empty.", "Ok");
                }
                else
                {
                    LobbyApi.TerminateLobbyService(_key.Trim(), _name.Trim(), res =>
                    {
                        EditorUtility.DisplayDialog("Success", $"The lobby service will start terminating (shutting down the deploy) now", "Ok");
                    }, error =>
                    {
                        EditorUtility.DisplayDialog("Failed to terminate lobby", $"The following error happened while trying to terminate the lobby service:\n\n{error}", "Ok");
                    });
                }
            }
            EditorGUILayout.HelpBox(new GUIContent("Done with your lobby?\nEnter the same name as creation to shut it down"));
        }
#endif
    
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
