using System;
namespace Edgegap
{
    // https://docs.edgegap.com/docs/lobby/functions#updating-a-lobby
    // https://docs.edgegap.com/docs/lobby/functions#leaving-a-lobby
    [Serializable]
    public struct LobbyJoinOrLeaveRequest
    {
        [Serializable]
        public struct Player
        {
            public string id;
        }
        public string lobby_id;
        public Player player;
    }
}
