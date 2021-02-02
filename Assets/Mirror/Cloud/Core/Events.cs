using System;
using Mirror.Cloud.ListServerService;
using UnityEngine.Events;

namespace Mirror.Cloud
{
    [System.Serializable]
    public class ServerListEvent : UnityEvent<ServerCollectionJson> { }

    [System.Serializable]
    public class MatchFoundEvent : UnityEvent<ServerJson> { }
}
