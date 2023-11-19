using System;

namespace Mirror.SimpleWeb
{
    [Serializable]
    public struct ClientWebsocketSettings
    {
        public WebsocketPortOption ClientPortOption;
        public WebsocketPathOption ClientPathOption;
        public ushort CustomClientPort;
        public string CustomClientPath;
    }
    public enum WebsocketPortOption
    {
        DefaultSameAsServer,
        MatchWebpageProtocol,
        SpecifyPort
    }
    public enum WebsocketPathOption
    {
        DefaultNone,
        SpecifyPath
    }

}
