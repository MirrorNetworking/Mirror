# Transports Overview

MirrorNG is a high level Networking Library that can use several different low level transports. To use a transport, simply add it as component to the NetworkManager and drag it into the NetworkManager's Transport field.

-   [KCP ](Kcp.md) Simple, message based, MMO Scale UDP networking in C\#. And no magic.

-   [WebGL - WebSockets](WebSockets.md) WebSockets transport layer for MirrorNG that target WebGL clients, without relying on Unity's stodgy old LLAPI.

-   [Multiplexer](Multiplexer.md) Multiplexer is a bridging transport to allow a server to handle clients on different transports concurrently, for example desktop clients using Kcp together with WebGL clients using Websockets.

-   [UDP - Ignorance](Ignorance.md) Ignorance implements a reliable and unreliable sequenced UDP transport based on ENet.

-   [UDP - LiteNetLib4Mirror](LiteNetLib4Mirror.md) LiteNetLib4Mirror implements a UDP transport based on LiteNetLib with Network Discovery and uPnP included.

-   [Steam - FizzySteamworks](FizzySteamworks.md) Transport utilising Steam P2P network, building on Steamworks.NET.

-   [Steam - FizzyFacepunch](FizzyFacepunch.md) Transport utilising Steam P2P network, building on Facepunch.Steamworks.