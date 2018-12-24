# Setting up a multiplayer project

This page contains an overview of the most basic and common things you need when setting up a multiplayer project. In terms of what you require in your project, these are:

-   A Network Manager
-   A user interface (for players to find and join games)
-   Networked Player Prefabs (for players to control)
-   Scripts which are multiplayer-aware

There are variations on this list; for example, in a multiplayer chess game, or a real-time strategy (RTS) game, you don’t need a visible GameObject to represent the player. However, you might still want an invisible empty GameObject to represent the player, and attach scripts to it which relate to what the player is able to do.

This introductory page contains a brief description of each of the items listed above. However, each section links to more detailed documentation, which you need to continue reading to fully understand them.

There are also some important concepts that you need to understand and make choices about when building your game. These concepts can broadly be summarised as:

-   The relationship between a client, a server, and a host
-   The idea of authority over GameObjects and actions

To learn about these concepts, see documentation on Network System Concepts.

## The Network Manager

The Network Manager is responsible for managing the networking at a time.

![The Network Manager Component](NetworkManagerInspector.png)

Mirror’s built-in Network Manager component wraps up all of the features for managing your multiplayer game into one single component. If you have custom requirements which aren’t covered by this component, you can write your own network manager in script instead of using this component. If you’re just starting out with multiplayer games, you should use this component.

To learn more, see documentation on the Network Manager.

Mirror has an extremely basic built-in version of such an interface, called the NetworkManagerHUD. However, it is very basic in both functionality and visual design, so you should replace this with your own UI before you finish your project.

![Mirror’s built-in Network Manager HUD, shown in MatchMaker mode.](NetworkManagerHUD-MatchMakerMode.png)

To learn more, see documentation on the Network Manager HUD.

## Networked player GameObjects

Most multiplayer games feature some kind of object that a player can control, like a character, a car, or something else. Some multiplayer games don’t feature a single visible player object, and attach all the scripts to it which control what the player can do in your game.

If you are using Mirror’s Network Manager component (see *The Network Manager*, above), assign the Prefab to the Player Prefab field.

![The network manager with a “Player Car” prefab assigned to the Player Prefab field.](NetworkManagerWithPlayerPrefab.png)

When the game is running, the Network Manager creates a copy (an “instance”) of your player Prefab for each player that connects to the match.

However - and this is where it can get confusing for people new to multiplayer programming - you need to make sure the scripts on your player Prefab instance are “aware” of whether the player controlling the instance is using the host computer (the computer that is managing the game) or a client computer (a different computer to the one that is managing the game).

This is because both situations will be occurring at the same time.

## Multiplayer-aware Scripts

Writing scripts for a multiplayer game is different to writing scripts for a single-player game. This is because when you write a script for a multiplayer game, you need to think about the different contexts that the scripts run in. To learn about the networking concepts discussed here, see documentation on Network System Concepts.

For example, the scripts you place on your player Prefab should allow the “owner” of that player instance to control it, but it should not allow other people to control it.

You need to think about whether the server or the client has authority over what the script does. Sometimes, you want the script to run on both the server and the clients. Other times, you only want the script to run on the server, and you only want the clients to replicate how the GameObjects are moving (for example, in a game in which players pick up collectible GameObjects, the script should only run on the server so that the server can be the authority on the number of GameObjects collected).

Depending on what your script does, you need to decide which parts of your script should be active in which situations.

For player GameObjects, each person usually has active control over their own player instance. This means each client has local authority over its own player, and the server accepts what the client tells it about what the player is doing.

For non-player GameObjects, the server usually has authority over what happens (such as whether an item has been collected), and all clients accept what the server tells them about what has happened to that GameObject.
