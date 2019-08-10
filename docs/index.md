# Documentation

**Mirror is the most compatible direct replacement for the deprecated Unity Networking API.**

Mirror has nearly all of the components and features from UNet, making networking easy, concise and maintainable, whether you're starting from scratch or converting an existing project. We even have a [Migration Tool](General/Migration) to do most of the work for you!

Built to support games of any scale, from LAN party games to dedicated high-volume authoritative servers running hundreds of players, Mirror is the core networking solution for [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015), [Cubica](https://www.cubica.net/), and [more](https://vis2k.github.io/Mirror/General/Showcase)!

uMMORPG was possible with \<6000 lines of code. We needed a networking library that allows us to launch our games, period!

-   **Full Source included** for debugging convenience

-   Several working examples included

-   Active [Discord](https://discord.gg/2BvnM4R) for prompt support

-   **Requires Unity 2018.3.6+ and Runtime .Net 4.x**

-   Alpha / Beta Unity versions cannot be supported

**Multiple Transports Available:**

-   **TCP** ([Telepathy](Transports/Telepathy))

-   **UDP** ([ENet](Transports/Ignorance) and [LiteNetLib](Transports/LiteNetLib4Mirror))

-   **Steam** ([Steamworks.Net](Transports/Fizzy))

-   **WebGL** ([Web Sockets](Transports/WebSockets))

**List Server**

We've developed a [List Server](https://mirror-networking.com/list-server/) where game servers can register and clients can connect to find those servers to play on them.

**Key Features & Components:**

-   [Transports](Transports) are interchangeable components

-   Additive Scene Loading

-   Single and separated Unity projects supported

-   Network [Manager](Components/NetworkManager) and [HUD](Components/NetworkManagerHUD)

-   Network [Lobby Manager](Components/NetworkLobbyManager) and [Lobby Player](Components/NetworkLobbyPlayer)

-   Network [Identity](Components/NetworkIdentity)

-   Network [Transform](Components/NetworkTransform)

-   Network [Animator](Components/NetworkAnimator) with 64 parameters

-   Network [Proximity Checker](Components/NetworkProximityChecker)

-   [SyncVar](Classes/SyncVars), [SyncList](Classes/SyncLists), [SyncEvent](Classes/SyncEvent), [SyncDictionary](Classes/SyncDictionary), and [SyncHashSet](Classes/SyncHashSet)

**Integrations**

-   [Dissonance Voice Chat](https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078)

-   [Network Sync Transform](https://github.com/emotitron/NetworkSyncTransform)

-   [Noble Connect Free](https://assetstore.unity.com/packages/tools/network/noble-connect-free-141599)

-   [Rucksack](https://assetstore.unity.com/packages/templates/systems/rucksack-multiplayer-inventory-system-114921)

-   [RTS Engine](https://assetstore.unity.com/packages/templates/packs/rts-engine-79732)

-   [Smooth Sync](https://assetstore.unity.com/packages/tools/network/smooth-sync-96925)

-   [Weather Maker](https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-unity-weather-system-sky-water-volumetric-clouds-a-60955)
