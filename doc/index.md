# Mirror Networking for Unity

**Mirror is the most compatible direct replacement for the deprecated Unity Networking API.**

Mirror has nearly all of the components and features from UNet, making networking easy, concise and maintainable, whether you're starting from scratch or converting an existing project. We even have a [Migration Tool](articles/General/Migration.md) to do most of the work for you!

Built to support games of any scale, from LAN party games to dedicated high-volume authoritative servers running hundreds of players, Mirror is the core networking solution for [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015), [Cubica](https://www.cubica.net/), and [more](articles/General/Showcase.md)!

uMMORPG was possible with \<6000 lines of code. We needed a networking library that allows us to launch our games, period!
-   **Full Source included** for debugging convenience
-   Several working examples included
-   Active [Discord](https://discord.gg/2BvnM4R) for prompt support
-   **Requires Unity 2018.3.6+ and Runtime .Net 4.x**
-   Alpha / Beta Unity versions cannot be supported

**Multiple Transports Available:**
-   **TCP** ([Telepathy](articles/Transports/Telepathy.md))
-   **UDP** ([ENet](articles/Transports/Ignorance.md) and [LiteNetLib](articles/Transports/LiteNetLib4Mirror.md))
-   **Steam** ([Steamworks.Net](articles/Transports/Fizzy.md))
-   **WebGL** ([Secure Web Sockets](articles/Transports/WebSockets.md))

**List Server**

We've developed a [List Server](https://mirror-networking.com/list-server/) where game servers can register and clients can connect to find those servers to play on them.

**Key Features & Components:**
-   [Transports](articles/Transports/index.md) are interchangeable components
-   Additive Scene Loading
-   Single and separated Unity projects supported
-   Network [Manager](articles/Components/NetworkManager.md) and [HUD](articles/Components/NetworkManagerHUD.md)
-   Network [Room Manager](articles/Components/NetworkRoomManager.md) and [Room Player](articles/Components/NetworkRoomPlayer.md)
-   Network [Identity](articles/Components/NetworkIdentity.md)
-   Network [Transform](articles/Components/NetworkTransform.md)
-   Network [Animator](articles/Components/NetworkAnimator.md) with 64 parameters
-   Network [Proximity Checker](articles/Components/NetworkProximityChecker.md)
-   [SyncVar](articles/Classes/SyncVars.md), [SyncList](articles/Classes/SyncLists.md), [SyncEvent](articles/Classes/SyncEvent.md), [SyncDictionary](articles/Classes/SyncDictionary.md), and [SyncHashSet](articles/Classes/SyncHashSet.md)

**Integrations**
-   [Dissonance Voice Chat](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078)
-   [Network Sync Transform](https://github.com/emotitron/NetworkSyncTransform)
-   [Noble Connect Free](https://assetstore.unity.com/packages/tools/network/noble-connect-free-141599)
-   [Rucksack](https://assetstore.unity.com/packages/templates/systems/rucksack-multiplayer-inventory-system-114921)
-   [RTS Engine](https://assetstore.unity.com/packages/templates/packs/rts-engine-79732)
-   [Smooth Sync](https://assetstore.unity.com/packages/tools/network/smooth-sync-96925)
-   [Weather Maker](https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-unity-weather-system-sky-water-volumetric-clouds-a-60955)
