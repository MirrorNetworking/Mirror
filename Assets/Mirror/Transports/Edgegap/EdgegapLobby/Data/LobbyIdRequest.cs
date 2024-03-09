using System;
namespace Edgegap
{
    [Serializable]
    public struct LobbyIdRequest
    {
        public string lobby_id;
        public LobbyIdRequest(string lobbyId)
        {
            lobby_id = lobbyId;
        }
    }
}
