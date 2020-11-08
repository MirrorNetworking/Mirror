# MirrorNG Networking for Unity

**MirrorNG is the most compatible direct replacement for the deprecated Unity Networking API.**

MirrorNG has nearly all of the components and features from UNet, making networking easy, concise and maintainable, whether you're starting from scratch or converting an existing project.

Built to support games of any scale, from LAN party games to dedicated high-volume authoritative servers running hundreds of players, MirrorNG is the core networking solution for [Cubica](https://www.cubica.net/), and [more](Articles/General/Showcase.md)!

-   **Full Source included** for debugging convenience
-   Several working examples included
-   Active [Discord](https://discord.gg/2BvnM4R) for prompt support
-   **Requires Unity 2018.4 LTS and Runtime .Net 4.x (default in Unity 2019) and .Net 2.0 Compatibility is recommended**
-   Alpha / Beta Unity versions cannot be supported

**Key Features & Components:**
-   [Transports](Articles/Transports/index.md) are interchangeable components
-   [NetworkSceneManager](Articles/Components/NetworkSceneManager.md) to load normal and additive network scenes.
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
-   [List Server](https://mirror-networking.com/list-server/) where game servers can register and clients can connect to find those servers to play on them.