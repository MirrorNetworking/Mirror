# Transports Overview

Mirror is a high level Networking Library that can use several different low level transports. To use a transport, simply add it as component to the NetworkManager and drag it into the NetworkManager's Transport field.

-   [KCP - KCP Transport](KCPTransport.md) based on kcp.c v1.7, nearly translated 1:1.
-   [TCP - Libuv2k](Libuv2k.md) based on Native C networking backend used by Node.js.
-   [TCP - Telepathy](Telepathy.md) Simple, message based, MMO Scale TCP networking in C\#. And no magic.
-   [WebSockets - SimpleWebTransport](SimpleWebTransport.md) WebSockets transport layer for Mirror that target WebGL clients.
-   [WebSockets - AsioTransport](AsioTransport.md) WebSockets transport layer for Mirror that target WebGL clients.
-   [WebRTC - WebRTCTransport](WebRTCTransport.md) WebRTC transport layer for Mirror that target WebRTC clients.
-   [Multiplexer](Multiplexer.md) Multiplexer is a bridging transport to allow a server to handle clients on different transports concurrently, for example desktop clients using Telepathy together with WebGL clients using Websockets.
-   [Fallback](Fallback.md) Fallback is a compatibility transport for transports that don't run on all platforms and need fallback options to cover all other platforms.
-   [UDP - Ignorance](Ignorance.md) Ignorance implements a reliable and unreliable sequenced UDP transport based on ENet.
-   [UDP - LiteNetLibTransport](LiteNetLibTransport.md) LiteNetLibTransport implements a UDP transport based on [LiteNetLib](https://github.com/RevenantX/LiteNetLib).
-   [Steam - FizzySteamworks](FizzySteamworks.md) Transport utilising Steam P2P network, building on Steamworks.NET.
-   [Steam - FizzyFacepunch](FizzyFacepunch.md) Transport utilising Steam P2P network, building on Facepunch.Steamworks.
-   [Epic - EpicTransport](EpicTransport.md) Transport utilising Epic Online Services for lobbies and relay.
-   [Relay - DarkReflectiveMirror](DarkReflectiveMirror.md) Relay service transport for Mirror.
