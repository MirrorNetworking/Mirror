using System;
using System.Collections;
using System.Threading;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;
namespace Edgegap
{
    public class EdgegapLobbyKcpTransport : EdgegapKcpTransport
    {
        [Header("Lobby Settings")]
        [Tooltip("URL to the Edgegap lobby service")]
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
                    }, s =>
                    {
                        _status = TransportStatus.Error;
                        OnServerError?.Invoke(0, TransportError.Unexpected, $"Could not start lobby: {s}");
                        ServerStop();
                    });
                },
                s =>
                {
                    _status = TransportStatus.Error;
                    OnServerError?.Invoke(0, TransportError.Unexpected, $"Couldn't create lobby: {s}");
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
                OnServerError?.Invoke(0, TransportError.Unexpected, error);
                Debug.Log($"Failed to delete lobby: {error}");
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
                    OnClientError?.Invoke(TransportError.Unexpected, error);
                    Debug.Log($"Failed to leave lobby: {error}");
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
                OnClientError?.Invoke(TransportError.Unexpected, error);
                OnClientDisconnected?.Invoke();
            });
        }

        private IEnumerator WaitForLobbyRelay(string lobbyId, bool forServer)
        {
            _status = TransportStatus.WaitingRelay;
            double time = NetworkTime.localTime;
            bool running = true;
            while (running)
            {
                if (NetworkTime.localTime - time >= lobbyWaitTimeout)
                {
                    _status = TransportStatus.Error;
                    if (forServer)
                    {
                        _status = TransportStatus.Error;
                        OnServerError?.Invoke(0, TransportError.Unexpected, $"Timed out waiting for lobby.");
                        ServerStop();
                    }
                    else
                    {
                        _status = TransportStatus.Error;
                        OnClientError?.Invoke(TransportError.Unexpected, $"Couldn't find my player ({_playerId})");
                        ClientDisconnect();
                    }
                    yield break;
                }
                bool waitingForResponse = true;
                Api.GetLobby(lobbyId, lobby =>
                {
                    waitingForResponse = false;
                    if (!string.IsNullOrEmpty(lobby.assignment.ip))
                    {
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
                            if (forServer)
                            {
                                _status = TransportStatus.Error;
                                OnServerError?.Invoke(0, TransportError.Unexpected, $"Couldn't find my player ({_playerId})");
                                ServerStop();
                            }
                            else
                            {
                                _status = TransportStatus.Error;
                                OnClientError?.Invoke(TransportError.Unexpected, $"Couldn't find my player ({_playerId})");
                                ClientDisconnect();
                            }
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
                    }
                }, error =>
                {
                    running = false;
                    waitingForResponse = false;
                    _status = TransportStatus.Error;
                    if (forServer)
                    {
                        OnServerError?.Invoke(0, TransportError.Unexpected, error);
                        ServerStop();
                    }
                    else
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, error);
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
