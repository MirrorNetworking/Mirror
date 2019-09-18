# Transports
Mirror is a high level Networking Library that can use several different low level transports.
To use a transport, simply add it as component to the NetworkManager and drag it into the NetworkManager's Transport field.

# Transports Overview
-   [TCP - Telepathy](Telepathy.md)  
    Simple, message based, MMO Scale TCP networking in C#. And no magic.
-   [UDP - Ignorance](Ignorance.md)  
    Ignorance implements a reliable and unreliable sequenced UDP transport based on ENet.
-   [UDP - LiteNetLib4Mirror](LiteNetLib4Mirror.md)  
    LiteNetLib4Mirror implements a UDP transport based on LiteNetLib with Network Discovery and uPnP included.
-   [WebGL - WebSockets](WebSockets.md)  
    WebSockets transport layer for Mirror that target WebGL clients, without relying on Unity's stodgy old LLAPI.
-   [Steam - Fizzy](Fizzy.md)  
    A complete rebuild utilising Async (Previously SteamNetNetworkTransport) of a Steam P2P network transport layer.
-   [Multiplexer - Multiplexer](Multiplexer.md)  
    Multiplexer is a bridging transport to allow a server to handle clients on different transports concurrnently, for example desktop clients using Telepathy together with WebGL clients using Websockets.
