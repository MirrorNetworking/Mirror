using System;
namespace Edgegap
{
    // https://docs.edgegap.com/docs/lobby/functions/#starting-a-lobby
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
