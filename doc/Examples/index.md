# Samples Overview

Mirror includes several small examples to help you learn how to use various features and how to set things up so they work together.
-   [Basic](Basic.md)  
    Basic is what it sounds like...the most rudimentary baseline of a networked game. Features SyncVars updating random UI data for each player.
-   [Tanks](Tanks.md)  
    This is a simple scene with animated tanks, networked rigidbody projectiles, and NavMesh movement
-   [Pong](Pong.md)  
    A simple example for "How to build a multiplayer game with Mirror" is Pong. It illustrates the usage of `NetworkManager`, `NetworkManagerHUD`, NetworkBehaviour, NetworkIdentity, `NetworkTransform`, `NetworkStartPosition`and various Attributes.
-   [Additive Scenes](AdditiveScenes.md)  
    The Additive Scenes example demonstrates a server additively loading a sub-scene into a main scene at startup, and having a server-only trigger that generates a message to any client whose player enters the trigger zone to also load the sub-scene, and subsequently unload it when they leave the trigger zone. Only players inside the trigger zone can see the objects in the sub-scene. Network Proximity Checker components are key to making this scenario work.
-   [Room System](Room.md)  
    The Room System example demonstrates how to set up a "staging" scene where players assemble before starting a match. When all players are ready, the server sends them all a message to change scenes (along with the server itself) to the actual game play scene so they all come in at once.  Includes fully playable game with a character controller where players collect server-spawned prizes for score.
