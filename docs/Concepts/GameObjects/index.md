# Networked GameObjects

Networked GameObjects are GameObjects which are controlled and synchronized by Mirror’s networking system. Using synchronized networked GameObjects, you can create a shared experience for all the players who are playing an instance of your game. They see and hear the same events and actions - even though that may be from their own unique viewpoints within your game.

Multiplayer games in Mirror are typically built using Scenes that contain a mix of networked GameObjects and regular (non-networked) GameObjects. The networked GameObjects are those which move or change during gameplay in a way that needs to be synchronized across all users who are playing the game together. Non-networked GameObjects are those which either don’t move or change at all during gameplay (for example, static obstacles like rocks or fences), or GameObjects which have movement or changes that don’t need to be synchronized across players (for example, a gently swaying tree or clouds passing by in the background of your game).

A networked GameObject is one which has a Network Identity component attached. However, a Network Identity component alone is not enough for your GameObject to be functional and active in your multiplayer game. The Network Identity component is the starting point for synchronization, and it allows the Network Manager to synchronize the creation and destruction of the GameObject, but other than that, it does not specify *which properties* of your GameObject should be synchronized.

What exactly should be synchronized on each networked GameObject depends on the type of game you are making, and what each GameObject’s purpose is. Some examples of what you might want to synchronize are:

-   The position and rotation of moving GameObjects such as the players and non-player characters.
-   The animation state of an animated GameObject
-   The value of a variable, for example how much time is left in the current round of a game, or how much energy a player has.

Some of these things can be automatically synchronized by Mirror. The synchronized creation and destruction of networked GameObjects is managed by the NetworkManager, and is known as Spawning. You can use the Network Transform component to synchronize the position and rotation of a GameObject, and you can use the Network Animator component to synchronize the animation of a GameObject.

To synchronize other properties of a networked GameObject, you need to use scripting. See State Synchronization for more information about this.
