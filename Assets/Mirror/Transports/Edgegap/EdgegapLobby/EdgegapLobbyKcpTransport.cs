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
        public string lobbyUrl;
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
                throw new Exception("No lobby request set. Call CreateLobbyAndStartServer");
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
                        OnServerError?.Invoke(0, TransportError.Unexpected, s);
                        throw new Exception($"Couldn't start lobby: {s}");
                    });
                },
                s =>
                {
                    _status = TransportStatus.Error;
                    OnServerError?.Invoke(0, TransportError.Unexpected, s);
                    throw new Exception($"Couldn't create lobby: {s}");
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
            _playerId = Random.Range(1, int.MaxValue).ToString(); // todo
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
                OnClientError?.Invoke(TransportError.Unexpected, error);
                OnClientDisconnected?.Invoke();
            });
        }

        private IEnumerator WaitForLobbyRelay(string lobbyId, bool server)
        {
            // TODO: timeout
            _status = TransportStatus.WaitingRelay;
            bool running = true;
            while (running)
            {
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
                            if (server)
                            {
                                _status = TransportStatus.Error;
                                OnServerError?.Invoke(0, TransportError.Unexpected, $"Couldn't find my player ({_playerId})");
                            }
                            else
                            {
                                _status = TransportStatus.Error;
                                OnClientError?.Invoke(TransportError.Unexpected, $"Couldn't find my player ({_playerId})");
                                OnClientDisconnected?.Invoke();
                            }
                        }
                        _status = TransportStatus.Connecting;
                        if (server)
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
                    if (server)
                    {
                        OnServerError?.Invoke(0, TransportError.Unexpected, error);
                    }
                    else
                    {
                        OnClientError?.Invoke(TransportError.Unexpected, error);
                        OnClientDisconnected?.Invoke();
                    }
                });
                while (waitingForResponse)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        public void CreateLobbyAndStartServer(LobbyCreateRequest request, bool host)
        {
            _playerId = Random.Range(1, int.MaxValue).ToString(); // todo
            request.player.id = _playerId;
            _request = request;
            if (host)
            {
                NetworkManager.singleton.StartHost();
            }
            else
            {
                NetworkManager.singleton.StartServer();
            }
        }

        private void OnDestroy()
        {
            // attempt to clean up lobbies, if active
            if (NetworkServer.active)
            {
                ServerStop();
                Thread.Sleep(300); // sorry. this can go once the lobby service can timeout lobbies itself
            } else if (NetworkClient.active)
            {
                ClientDisconnect();
                Thread.Sleep(300); // sorry. this can go once the lobby service can timeout lobbies itself
            }
        }
    }
}
