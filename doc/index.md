# Mirror Networking for Unity

**Mirror is the most compatible direct replacement for the deprecated Unity Networking API.**

Mirror has nearly all of the components and features from UNet, making networking easy, concise and maintainable, whether you're starting from scratch or converting an existing project. We even have a [Migration Tool](Articles/General/Migration.md) to do most of the work for you!

Built to support games of any scale, from LAN party games to dedicated high-volume authoritative servers running hundreds of players, Mirror is the core networking solution for [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015), [Cubica](https://www.cubica.net/), and [more](https://mirror-networking.com/showcase/)!

uMMORPG was possible with \<6000 lines of code. We needed a networking library that allows us to launch our games, period!
-   **Full Source included** for debugging convenience
-   Several working examples included
-   Active [Discord](https://discord.gg/2BvnM4R) for prompt support
-   **Requires Unity 2018.4 LTS and Runtime .Net 4.x (default in Unity 2019) and .Net 2.0 Compatibility is recommended**
-   Alpha / Beta Unity versions cannot be supported

**Multiple Transports Available:**
-   [KCP - KCP Transport](Articles/Transports/KCPTransport.md) based on kcp.c v1.7, nearly translated 1:1.
-   [TCP - Libuv2k](Articles/Transports/Libuv2k.md) based on Native C networking backend used by Node.js.
-   [TCP - Telepathy](Articles/Transports/Telepathy.md) Simple, message based, MMO Scale TCP networking in C\#. And no magic.
-   [WebSockets - SimpleWebTransport](Articles/Transports/SimpleWebTransport.md) WebSockets transport layer for Mirror that target WebGL clients.
-   [Multiplexer](Articles/Transports/Multiplexer.md) Multiplexer is a bridging transport to allow a server to handle clients on different transports concurrently, for example desktop clients using Telepathy together with WebGL clients using Websockets.
-   [Fallback](Articles/Transports/Fallback.md) Fallback is a compatibility transport for transports that don't run on all platforms and need fallback options to cover all other platforms.
-   [UDP - Ignorance](Articles/Transports/Ignorance.md) Ignorance implements a reliable and unreliable sequenced UDP transport based on ENet.
-   [UDP - LiteNetLibTransport](Articles/Transports/LiteNetLibTransport.md) LiteNetLibTransport implements a UDP transport based on [LiteNetLib](https://github.com/RevenantX/LiteNetLib).
-   [Steam - FizzySteamworks](Articles/Transports/FizzySteamworks.md) Transport utilising Steam P2P network, building on Steamworks.NET.
-   [Steam - FizzyFacepunch](Articles/Transports/FizzyFacepunch.md) Transport utilising Steam P2P network, building on Facepunch.Steamworks.
-   [Epic - EpicTransport](Articles/Transports/EpicTransport.md) Transport utilising Epic Online Services for lobbies and relay.

**List Server**

We've developed a [List Server](https://mirror-networking.com/list-server/) where game servers can register and clients can connect to find those servers to play on them.

**Key Features & Components:**
-   [Transports](Articles/Transports/index.md) are interchangeable components
-   Additive Scene handling
-   Single and separated Unity projects supported
-   [Network Authenticators](Articles/Components/Authenticators/index.md) to manage access to your game
-   [Network Discovery](Articles/Components/NetworkDiscovery.md) to easily connect LAN players to a LAN Server or Host
-   [Network Manager](Articles/Components/NetworkManager.md) and [HUD](Articles/Components/NetworkManagerHUD.md)
-   [Network Room Manager](Articles/Components/NetworkRoomManager.md) and [Room Player](Articles/Components/NetworkRoomPlayer.md)
-   [Network Identity](Articles/Components/NetworkIdentity.md)
-   [Network Transform](Articles/Components/NetworkTransform.md) to sync position, rotation, and scale with interpolation
-   [Network Animator](Articles/Components/NetworkAnimator.md) with 64 parameters
-   [Network Proximity Checker](Articles/Components/NetworkProximityChecker.md) to help with Area of Interest
-   [Network Scene Checker](Articles/Components/NetworkSceneChecker.md) to isolate players and networked objects to Additive scene instances
-   [Network Match Checker](Articles/Components/NetworkMatchChecker.md) to isolate players and networked objects by [Network Visibility](Articles/Guides/Visibility.md)
-   [SyncVar](Articles/Guides/Sync/SyncVars.md), [SyncList](Articles/Guides/Sync/SyncLists.md), [SyncDictionary](Articles/Guides/Sync/SyncDictionary.md), and [SyncHashSet](Articles/Guides/Sync/SyncHashSet.md)

**Integrations**
-   [Dissonance Voice Chat](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078)
-   [Network Sync Transform](https://github.com/emotitron/NetworkSyncTransform)
-   [Noble Connect Free](https://assetstore.unity.com/packages/tools/network/noble-connect-free-141599)
-   [Rucksack](https://assetstore.unity.com/packages/templates/systems/rucksack-multiplayer-inventory-system-114921)
-   [RTS Engine](https://assetstore.unity.com/packages/templates/packs/rts-engine-79732)
-   [Smooth Sync](https://assetstore.unity.com/packages/tools/network/smooth-sync-96925)
-   [Weather Maker](https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-unity-weather-system-sky-water-volumetric-clouds-a-60955)
-   [Steamworks Networking](https://assetstore.unity.com/packages/tools/integration/steamworks-networking-151300)
-   [Master Audio Multiplayer](https://assetstore.unity.com/packages/tools/audio/master-audio-multiplayer-69547)
