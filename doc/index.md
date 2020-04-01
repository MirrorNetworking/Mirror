# Mirror Networking for Unity

**Mirror is the most compatible direct replacement for the deprecated Unity Networking API.**

Mirror has nearly all of the components and features from UNet, making networking easy, concise and maintainable, whether you're starting from scratch or converting an existing project. We even have a [Migration Tool](General/Migration.md) to do most of the work for you!

Built to support games of any scale, from LAN party games to dedicated high-volume authoritative servers running hundreds of players, Mirror is the core networking solution for [uMMORPG](https://assetstore.unity.com/packages/templates/systems/ummorpg-51212), [uSurvival](https://assetstore.unity.com/packages/templates/systems/usurvival-95015), [Cubica](https://www.cubica.net/), and [more](https://mirror-networking.com/showcase/)!

uMMORPG was possible with \<6000 lines of code. We needed a networking library that allows us to launch our games, period!
-   **Full Source included** for debugging convenience
-   Several working examples included
-   Active [Discord](https://discord.gg/2BvnM4R) for prompt support
-   **Requires Unity 2018.3.6+ and Runtime .Net 4.x (default in Unity 2019) and .Net 2.0 Compatibility is recommended**
-   Alpha / Beta Unity versions cannot be supported

**Multiple Transports Available:**
-   **TCP** ([Telepathy](Transports/Telepathy.md))
-   **UDP** ([ENet](Transports/Ignorance.md) and [LiteNetLib](Transports/LiteNetLib4Mirror.md))
-   **WebGL** ([Secure Web Sockets](Transports/WebSockets.md))
-   **Steam** ([Steamworks.Net](Transports/Fizzy.md))
-   **Discord** ([Discord DSK](Transports/Discord.md))

**List Server**

We've developed a [List Server](https://mirror-networking.com/list-server/) where game servers can register and clients can connect to find those servers to play on them.

**Key Features & Components:**
-   [Transports](Transports/index.md) are interchangeable components
-   Additive Scene Loading
-   Single and separated Unity projects supported
-   Network [Authenticators](Guides/Authentication.md) to protect your game
-   Network [Discovery](Guides/NetworkDiscovery.md) to easily connect LAN players to a LAN Server or Host
-   Network [Manager](Components/NetworkManager.md) and [HUD](Components/NetworkManagerHUD.md)
-   Network [Room Manager](Components/NetworkRoomManager.md) and [Room Player](Components/NetworkRoomPlayer.md)
-   Network [Identity](Components/NetworkIdentity.md)
-   Network [Transform](Components/NetworkTransform.md) to sync position, rotation, and scale with interpolation
-   Network [Animator](Components/NetworkAnimator.md) with 64 parameters
-   Network [Proximity Checker](Components/NetworkProximityChecker.md) to help with Area of Interest
-   Network [Scene Checker](Components/NetworkSceneChecker.md) to islolate players and networked objects to Additive scene instances
-   [SyncVar](Guides/Sync/SyncVars.md), [SyncList](Guides/Sync/SyncLists.md), [SyncEvent](Guides/Sync/SyncEvent.md), [SyncDictionary](Guides/Sync/SyncDictionary.md), and [SyncHashSet](Guides/Sync/SyncHashSet.md)

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
