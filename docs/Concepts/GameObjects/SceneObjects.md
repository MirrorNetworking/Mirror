# Scene GameObjects

There are two types of networked GameObjects in Mirror’s multiplayer system:

-   Those that are created dynamically at runtime
-   Those that are saved as part of a Scene

GameObjects that are created dynamically at runtime use the multiplayer Spawning system, and the prefabs they are instantiated from must be registered in the Network Manager’s list of networked GameObject prefabs.

However, networked GameObjects that you save as part of a Scene (and therefore already exist in the Scene when it is loaded) are handled differently. These GameObjects are loaded as part of the Scene on both the client and server, and exist at runtime before any spawn messages are sent by the multiplayer system.

When the Scene is loaded, all networked GameObjects in the Scene are disabled on both the client and the server. Then, when the Scene is fully loaded, the Network Manager automatically processes the Scene’s networked GameObjects, registering them all (and therefore causing them to be synchronized across clients), and enabling them, as if they were spawned at runtime.

Saving networked GameObjects in your Scene (rather than dynamically spawning them after the scene has loaded) has some benefits:

-   They are loaded with the level, so there will be no pause at runtime.
-   They can have specific modifications that differ from prefabs
-   Other GameObject instances in the Scene can reference them, which can avoid you having to use code to finding the GameObjects and make references to them up at runtime.

When the Network Manager spawns the networked Scene GameObjects, those GameObjects behave like dynamically spawned GameObjects. Mirror sends them updates and ClientRPC calls.

If a Scene GameObject is destroyed on the server before a client joins the game, then it is never enabled on new clients that join.

When a client connects, the client is sent an ObjectSpawnScene spawn message for each of the Scene GameObjects that exist on the server, that are visible to that client. This message causes the GameObject on the client to be enabled, and has the latest state of that GameObject from the server in it. This means that only GameObjects that are visible to the client, and not destroyed on the server, are activated on the client. Like regular non-Scene GameObjects, these Scene GameObjects are started with the latest state when the client joins the game.
