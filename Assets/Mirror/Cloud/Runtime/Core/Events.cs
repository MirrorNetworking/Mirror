using System;
using Mirror.CloudServices.ListServerService;
using UnityEngine.Events;

namespace Mirror.CloudServices
{
    [Serializable]
    public class ServerListEvent : UnityEvent<ServerCollectionJson> { }

    [Serializable]
    public class MatchFoundEvent : UnityEvent<ServerJson> { }
}
