using System;
using System.Collections;
using System.Threading;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;
namespace Edgegap
{
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/transports/edgegap-transports/edgegap-relay")]
    public class EdgegapLobbyKcpTransport : EdgegapKcpTransport
    {
        [Header("Lobby Settings")]
        [Tooltip("URL to the Edgegap lobby service, automatically filled in after completing the creation process via button below (or enter manually)")]
        public string lobbyUrl;
        [Tooltip("How long to wait for the relay to be assigned after starting a lobby")]
        public float lobbyWaitTimeout = 60;

        public LobbyApi Api;
        private LobbyCreateRequest? _request;
        private string _lobbyId;
        private string _playerId;
        private TransportStatus _status = TransportStatus.Offline;
        public enum TransportStatus
        {
            Offline,
            CreatingLobby,
            StartingLobby,
            JoiningLobby,
            WaitingRelay,
            Connecting,
            Connected,
            Error,
        }
        public TransportStatus Status
        {
            get
            {
                if (!NetworkClient.active && !NetworkServer.active)
                {
                    return TransportStatus.Offline;
                }
                if (_status == TransportStatus.Connecting)
                {
                    if (NetworkServer.active)
                    {
                        switch (((EdgegapKcpServer)this.server).state)
                        {
                            case ConnectionState.Valid:
                                return TransportStatus.Connected;
                            case ConnectionState.Invalid:
                            case ConnectionState.SessionTimeout:
                            case ConnectionState.Error:
                                return TransportStatus.Error;
                        }
                    }
                    else if (NetworkClient.active)
                    {
                        switch (((EdgegapKcpClient)this.client).connectionState)
                        {
                            case ConnectionState.Valid:
                                return TransportStatus.Connected;
                            case ConnectionState.Invalid:
                            case ConnectionState.SessionTimeout:
                            case ConnectionState.Error:
                                return TransportStatus.Error;
                        }
                    }
                }
                return _status;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Api = new LobbyApi(lobbyUrl);
        }

        private void Reset()
        {
            this.relayGUI = false;
        }

        public override void ServerStart()
        {
            if (!_request.HasValue)
            {
                throw new Exception("No lobby request set. Call SetServerLobbyParams");
            }
            _status = TransportStatus.CreatingLobby;
            Api.CreateLobby(_request.Value, lobby =>
                {
                    _lobbyId = lobby.lobby_id;
                    _status = TransportStatus.StartingLobby;
                    Api.StartLobby(new LobbyIdRequest(_lobbyId), () =>
                    {
                        StartCoroutine(WaitForLobbyRelay(_lobbyId, true));
                    }, error =>
                    {
                        _status = TransportStatus.Error;
                        string errorMsg = $"Could not start lobby: {error}";
                        Debug.LogError(errorMsg);
                        OnServerError?.Invoke(0, TransportError.Unexpected, errorMsg);
                        ServerStop();
                    });
                },
                error =>
                {
                    _status = TransportStatus.Error;
                    string errorMsg = $"Couldn't create lobby: {error}";
                    Debug.LogError(errorMsg);
                    OnServerError?.Invoke(0, TransportError.Unexpected, errorMsg);
                });
        }

        public override void ServerStop()
        {
            base.ServerStop();

            Api.DeleteLobby(_lobbyId, () =>
            {
                // yay
            }, error =>
            {
                OnServerError?.Invoke(0, TransportError.Unexpected, $"Failed to delete lobby: {error}");
            });
        }

        public override void ClientDisconnect()
        {
            base.ClientDisconnect();
            // this gets called for host mode as well
            if (!NetworkServer.active)
            {
                Api.LeaveLobby(new LobbyJoinOrLeaveRequest
                {
                    player = new LobbyJoinOrLeaveRequest.Player
                    {
                        id = _playerId
                    },
                    lobby_id = _lobbyId
                }, () =>
                {
                    // yay
                }, error =>
                {
                    string errorMsg = $"Failed to leave lobby: {error}";
                    OnClientError?.Invoke(TransportError.Unexpected, errorMsg);
                    Debug.LogError(errorMsg);
                });
            }
        }

        public override void ClientConnect(string address)
        {
            _lobbyId = address;
            _playerId = RandomPlayerId();
            _status = TransportStatus.JoiningLobby;
            Api.JoinLobby(new LobbyJoinOrLeaveRequest
            {
                player = new LobbyJoinOrLeaveRequest.Player
                {
                    id = _playerId,
                },
                lobby_id = address
            }, () =>
            {
                StartCoroutine(WaitForLobbyRelay(_lobbyId, false));
            }, error =>
            {
                _status = TransportStatus.Offline;
                string errorMsg = $"Failed to join lobby: {error}";
                OnClientError?.Invoke(TransportError.Unexpected, errorMsg);
                Debug.LogError(errorMsg);
                OnClientDisconnected?.Invoke();
            });
        }

        private IEnumerator WaitForLobbyRelay(string lobbyId, bool forServer)
        {
            _status = TransportStatus.WaitingRelay;
            double startTime = NetworkTime.localTime;
            bool running = true;
            while (running)
            {
                if (NetworkTime.localTime - startTime >= lobbyWaitTimeout)
                {
                    _status = TransportStatus.Error;
                    string errorMsg = "Timed out waiting for lobby.";
                    Debug.LogError(errorMsg);
                    if (forServer)
                    {
                        _status = TransportStatus.Error;
                        OnServerError?.Invoke(0, TransportError.Unexpected, errorMsg);
                        ServerStop();
                    }
                    else
                    {
                        _status = TransportStatus.Error;
                        OnClientError?.Invoke(TransportError.Unexpected, errorMsg);
                        ClientDisconnect();
                    }
                    yield break;
                }
                bool waitingForResponse = true;
                Api.GetLobby(lobbyId, lobby =>
                {
                    waitingForResponse = false;
                    if (string.IsNullOrEmpty(lobby.assignment.ip))
                    {
                        // no lobby deployed yet, have the outer loop retry
                        return;
                    }
                    relayAddress = lobby.assignment.ip;
                    foreach (Lobby.Port aport in lobby.assignment.ports)
                    {
                        if (aport.protocol == "UDP")
                        {
                            if (aport.name == "server")
                            {
                                relayGameServerPort = (ushort)aport.port;

                            }
                            else if (aport.name == "client")
                            {
                                relayGameClientPort = (ushort)aport.port;
                            }
                        }
                    }
                    bool found = false;
                    foreach (Lobby.Player player in lobby.players)
                    {
                        if (player.id == _playerId)
                        {
                            userId = player.authorization_token;
                            sessionId = lobby.assignment.authorization_token;
                            found = true;
                            break;
                        }
                    }
                    running = false;
                    if (!found)
                    {
                        string errorMsg = $"Couldn't find my player ({_playerId})";
                        Debug.LogError(errorMsg);

                        if (forServer)
                        {
                            _status = TransportStatus.Error;
                            OnServerError?.Invoke(0, TransportError.Unexpected, errorMsg);
                            ServerStop();
                        }
                        else
                        {
                            _status = TransportStatus.Error;
                            OnClientError?.Invoke(TransportError.Unexpected, errorMsg);
                            ClientDisconnect();
                        }
                        return;
                    }
                    _status = TransportStatus.Connecting;
                    if (forServer)
                    {
                        base.ServerStart();
                    }
                    else
                    {
                        base.ClientConnect("");
                    }
                }, error =>
                {
                    running = false;
                    waitingForResponse = false;
                    _status = TransportStatus.Error;
                    string errorMsg = $"Failed to get lobby info: {error}";
                    Debug.LogError(errorMsg);
                    if (forServer)
                    {
                        OnServerError?.Invoke(0, TransportError.Unexpected, errorMsg);
                        ServerStop();
                    }
                    else
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, errorMsg);
                        ClientDisconnect();
                    }
                });
                while (waitingForResponse)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(0.2f);
            }
        }
        private static string RandomPlayerId()
        {
            return $"mirror-player-{Random.Range(1, int.MaxValue)}";
        }

        public void SetServerLobbyParams(string lobbyName, int capacity)
        {
            SetServerLobbyParams(new LobbyCreateRequest
            {
                player = new LobbyCreateRequest.Player
                {
                    id = RandomPlayerId(),
                },
                annotations = new LobbyCreateRequest.Annotation[]
                {
                },
                capacity = capacity,
                is_joinable = true,
                name = lobbyName,
                tags = new string[]
                {
                }
            });
        }

        public void SetServerLobbyParams(LobbyCreateRequest request)
        {
            _playerId = request.player.id;
            _request = request;
        }

        private void OnDestroy()
        {
            // attempt to clean up lobbies, if active
            if (NetworkServer.active)
            {
                ServerStop();
                // Absolutely make sure there's time for the network request to hit edgegap servers.
                // sorry. this can go once the lobby service can timeout lobbies itself
                Thread.Sleep(300);
            }
            else if (NetworkClient.active)
            {
                ClientDisconnect();
                // Absolutely make sure there's time for the network request to hit edgegap servers.
                // sorry. this can go once the lobby service can timeout lobbies itself
                Thread.Sleep(300);
            }
        }
    }
}
