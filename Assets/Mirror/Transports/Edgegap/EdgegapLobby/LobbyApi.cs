using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Edgegap
{
    // Implements the edgegap lobby api: https://docs.edgegap.com/docs/lobby/functions
    public class LobbyApi
    {
        [Header("Lobby Config")]
        public string LobbyUrl;
        public LobbyBrief[] Lobbies;

        public LobbyApi(string url)
        {
            LobbyUrl = url;
        }



        private static UnityWebRequest SendJson<T>(string url, T data, string method = "POST")
        {
            string body = JsonUtility.ToJson(data);
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static bool CheckErrorResponse(UnityWebRequest request, Action<string> onError)
        {
#if UNITY_2020_3_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
            {
                // how I hate http libs that think they need to be smart and handle status code errors.
                if (request.result != UnityWebRequest.Result.ProtocolError || request.responseCode == 0)
                {
                    onError?.Invoke(request.error);
                    return true;
                }
            }
#else
            if (request.isNetworkError)
            {
                onError?.Invoke(request.error);
                return true;
            }
#endif
            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                onError?.Invoke($"non-200 status code: {request.responseCode}. Body:\n {request.downloadHandler.text}");
                return true;
            }
            return false;
        }

        public void RefreshLobbies(Action<LobbyBrief[]> onLoaded, Action<string> onError)
        {
            UnityWebRequest request = UnityWebRequest.Get($"{LobbyUrl}/lobbies");
            request.SendWebRequest().completed += operation =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    ListLobbiesResponse lobbies = JsonUtility.FromJson<ListLobbiesResponse>(request.downloadHandler.text);
                    Lobbies = lobbies.data;
                    onLoaded?.Invoke(lobbies.data);
                }
            };
        }

        public void CreateLobby(LobbyCreateRequest createData, Action<Lobby> onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies", createData);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    Lobby lobby = JsonUtility.FromJson<Lobby>(request.downloadHandler.text);
                    onResponse?.Invoke(lobby);
                }
            };
        }

        public void UpdateLobby(string lobbyId, LobbyUpdateRequest updateData, Action<LobbyBrief> onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies/{lobbyId}", updateData, "PATCH");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    LobbyBrief lobby = JsonUtility.FromJson<LobbyBrief>(request.downloadHandler.text);
                    onResponse?.Invoke(lobby);
                }
            };
        }

        public void GetLobby(string lobbyId, Action<Lobby> onResponse, Action<string> onError)
        {
            UnityWebRequest request = UnityWebRequest.Get($"{LobbyUrl}/lobbies/{lobbyId}");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    Lobby lobby = JsonUtility.FromJson<Lobby>(request.downloadHandler.text);
                    onResponse?.Invoke(lobby);
                }
            };
        }

        public void JoinLobby(LobbyJoinOrLeaveRequest data, Action onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies:join", data);
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    onResponse?.Invoke();
                }
            };
        }

        public void LeaveLobby(LobbyJoinOrLeaveRequest data, Action onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies:leave", data);
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    onResponse?.Invoke();
                }
            };
        }

        public void StartLobby(LobbyIdRequest data, Action onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies:start", data);
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    onResponse?.Invoke();
                }
            };
        }

        public void DeleteLobby(string lobbyId, Action onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson($"{LobbyUrl}/lobbies/{lobbyId}", "", "DELETE");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    onResponse?.Invoke();
                }
            };
        }

        struct CreateLobbyServiceRequest
        {
            public string name;
        }
        public struct LobbyServiceResponse
        {
            public string name;
            public string url;
            public string status;
        }

        public static void TrimApiKey(ref string apiKey)
        {
            if (apiKey == null)
            {
                return;
            }
            if (apiKey.StartsWith("token "))
            {
                apiKey = apiKey.Substring("token ".Length);
            }
            apiKey = apiKey.Trim();
        }

        public static void CreateAndDeployLobbyService(string apiKey, string name, Action<LobbyServiceResponse> onResponse, Action<string> onError)
        {
            TrimApiKey(ref apiKey);

            // try to get the lobby first
            GetLobbyService(apiKey, name, response =>
            {
                if (response == null)
                {
                    CreateLobbyService(apiKey, name, onResponse, onError);
                }
                else if (!string.IsNullOrEmpty(response.Value.url))
                {
                    onResponse(response.Value);
                }
                else
                {
                    DeployLobbyService(apiKey, name, onResponse, onError);
                }
            }, onError);
        }

        private static void CreateLobbyService(string apiKey, string name, Action<LobbyServiceResponse> onResponse, Action<string> onError)
        {
            UnityWebRequest request = SendJson("https://api.edgegap.com/v1/lobbies", new CreateLobbyServiceRequest
            {
                name = name
            });
            request.SetRequestHeader("Authorization", $"token {apiKey}");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    DeployLobbyService(apiKey, name, onResponse, onError);
                }
            };
        }

        public static void GetLobbyService(string apiKey, string name, Action<LobbyServiceResponse?> onResponse, Action<string> onError)
        {
            TrimApiKey(ref apiKey);

            var request = UnityWebRequest.Get($"https://api.edgegap.com/v1/lobbies/{name}");
            request.SetRequestHeader("Authorization", $"token {apiKey}");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (request.responseCode == 404)
                    {
                        onResponse(null);
                        return;
                    }
                    if (CheckErrorResponse(request, onError)) return;
                    LobbyServiceResponse response = JsonUtility.FromJson<LobbyServiceResponse>(request.downloadHandler.text);
                    onResponse(response);
                }
            };
        }

        public static void TerminateLobbyService(string apiKey, string name, Action<LobbyServiceResponse> onResponse, Action<string> onError)
        {
            TrimApiKey(ref apiKey);

            var request = SendJson("https://api.edgegap.com/v1/lobbies:terminate", new CreateLobbyServiceRequest
            {
                name = name
            });
            request.SetRequestHeader("Authorization", $"token {apiKey}");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    LobbyServiceResponse response = JsonUtility.FromJson<LobbyServiceResponse>(request.downloadHandler.text);
                    onResponse?.Invoke(response);
                }
            };
        }
        private static void DeployLobbyService(string apiKey, string name, Action<LobbyServiceResponse> onResponse, Action<string> onError)
        {
            var request = SendJson("https://api.edgegap.com/v1/lobbies:deploy", new CreateLobbyServiceRequest
            {
                name = name
            });
            request.SetRequestHeader("Authorization", $"token {apiKey}");
            request.SendWebRequest().completed += (op) =>
            {
                using (request)
                {
                    if (CheckErrorResponse(request, onError)) return;
                    LobbyServiceResponse response = JsonUtility.FromJson<LobbyServiceResponse>(request.downloadHandler.text);
                    onResponse?.Invoke(response);
                }
            };
        }
    }
}
