# Components Overview

These core components are included in Mirror:

-   [NetworkAnimator](NetworkAnimator.md)  
    The Network Animator component allows you to synchronize animation states for networked objects. It synchronizes state and parameters from an Animator Controller.
-   [NetworkIdentity](NetworkIdentity.md)  
    The Network Identity component is at the heart of the Mirror networking high-level API. It controls a game object’s unique identity on the network, and it uses that identity to make the networking system aware of the game object. It offers two different options for configuration and they are mutually exclusive, which means either one of the options or none can be checked.
-   [NetworkManager](NetworkManager.md)  
    The Network Manager is a component for managing the networking aspects of a multiplayer game.
-   [NetworkManagerHUD](NetworkManagerHUD.md)  
    The Network Manager HUD is a quick-start tool to help you start building your multiplayer game straight away, without first having to build a user interface for game creation/connection/joining. It allows you to jump straight into your gameplay programming, and means you can build your own version of these controls later in your development schedule.
-   [NetworkProximityChecker](NetworkProximityChecker.md)  
    The Network Proximity Checker component controls the visibility of game objects for network clients, based on proximity to players.
-   [NetworkRoomManager](NetworkRoomManager.md)  
    The Network Room Manager is an extension component of Network Manager that provides a basic functional room.
-   [NetworkRoomPlayer](NetworkRoomPlayer.md)  
    The Network Room Player is a component that's required on Player prefabs used in the Room Scene with the Network Room Manager above.
-   [NetworkStartPosition](NetworkStartPosition.md)  
    Network Start Position is used by the Network Manager when creating player objects. The position and rotation of the Network Start Position are used to place the newly created player object.
-   [NetworkTransform](NetworkTransform.md)  
    The Network Transform component synchronizes the movement and rotation of game objects across the network. Note that the network Transform component only synchronizes spawned networked game objects.
-   [NetworkTransformChild](NetworkTransformChild.md)  
    The Network Transform Child component synchronizes the position and rotation of the child game object of a game object with a Network Transform component.

## Authenticators

[Authenticators](Authenticators/index.md) are also available and more will be added soon:

-   [Basic Authenticator](Authenticators/Basic.md)  
    Mirror includes a Basic Authenticator in the Mirror / Authenticators folder which just uses a simple username and password.
-   [PlayFab Authenticator](Authenticators/PlayFab.md)  
    Coming Soon
-   [Facebook Authenticator](Authenticators/Facebook.md)  
    Coming Soon
-   [Google Authenticator](Authenticators/Google.md)  
    Coming Soon
