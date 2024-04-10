using System;
namespace Edgegap
{
    // https://docs.edgegap.com/docs/lobby/functions#updating-a-lobby
    [Serializable]
    public struct LobbyUpdateRequest
    {
        public int capacity;
        public bool is_joinable;
        public string[] tags;
    }
}
