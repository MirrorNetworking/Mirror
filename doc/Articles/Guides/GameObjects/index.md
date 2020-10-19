# Networked Game Objects

Networked game objects are game objects which are controlled and synchronized by MirrorNG’s networking system. Using synchronized networked game objects, you can create a shared experience for all the players who are playing an instance of your game. They see and hear the same events and actions - even though that may be from their own unique viewpoints within your game.

Multiplayer games in MirrorNG are typically built using Scenes that contain a mix of networked game objects and regular (non-networked) game objects. The networked game objects are those which move or change during gameplay in a way that needs to be synchronized across all users who are playing the game together. Non-networked game objects are those which either don’t move or change at all during gameplay (for example, static obstacles like rocks or fences), or game objects which have movement or changes that don’t need to be synchronized across players (for example, a gently swaying tree or clouds passing by in the background of your game).

A networked game object is one which has a Network Identity component attached. However, a Network Identity component alone is not enough for your game object to be functional and active in your multiplayer game. The Network Identity component is the starting point for synchronization, and it allows the Network Manager to synchronize the creation and destruction of the game object, but other than that, it does not specify *which properties* of your game object should be synchronized.

What exactly should be synchronized on each networked game object depends on the type of game you are making, and what each game object’s purpose is. Some examples of what you might want to synchronize are:
-   The position and rotation of moving game objects such as the players and non-player characters.
-   The animation state of an animated game object
-   The value of a variable, for example how much time is left in the current round of a game, or how much energy a player has.

As a convenience,  the initial position, rotation and scale of the object is automatically synchronized by the NetworkIdentity. Any other state you wish to synchronize accross the network needs to go in a [NetworkBehaviour](NetworkBehaviour.md) inside your GameObject.  

When an object is created in the server, you must tell MirrorNG to create it in the clients and keep track of it.  This is known as Spawning.  
