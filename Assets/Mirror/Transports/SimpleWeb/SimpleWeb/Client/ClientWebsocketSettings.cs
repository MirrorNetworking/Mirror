using System;

namespace Mirror.SimpleWeb
{
    [Serializable]
    public struct ClientWebsocketSettings
    {
        public WebsocketPortOption ClientPortOption;
        public ushort CustomClientPort;
    }
    public enum WebsocketPortOption
    {
        DefaultSameAsServer,
        MatchWebpageProtocol,
        SpecifyPort
    }
}
