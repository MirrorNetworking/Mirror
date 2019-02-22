# Components Overview

General description of Components

-   [NetworkManager](NetworkManager)  
    The Network Manager is a component for managing the networking aspects of a multiplayer game.
-   [NetworkLobbyManager](NetworkLobbyManager)  
    The Network Lobby Manager is an extension component of Network Manager that provides a basic functional lobby.
-   [NetworkLobbyPlayer](NetworkLobbyPlayer)  
    The Network Lobby Player is a component that's required on Player prefabs used in the Lobby Scene with the Network Lobby Manager above.
-   [NetworkManagerHUD](NetworkManagerHUD)  
    The Network Manager HUD is a quick-start tool to help you start building your multiplayer game straight away, without first having to build a user interface for game creation/connection/joining. It allows you to jump straight into your gameplay programming, and means you can build your own version of these controls later in your development schedule.
-   [NetworkIdentity](NetworkIdentity)  
    The Network Identity component is at the heart of the Mirror networking high-level API. It controls a GameObject’s unique identity on the network, and it uses that identity to make the networking system aware of the GameObject. It offers two different options for configuration and they are mutually exclusive, which means either one of the options or none can be checked.
-   [NetworkStartPosition](NetworkStartPosition)  
    Network Start Position is used by the Network Manager when creating player objects. The position and rotation of the Network Start Position are used to place the newly created player object.
-   [NetworkProximityChecker](NetworkProximityChecker)  
    The Network Proximity Checker component controls the visibility of GameObjects for network clients, based on proximity to players.
-   [NetworkTransform](NetworkTransform)  
    The Network Transform component synchronizes the movement and rotation of GameObjects across the network. Note that the network Transform component only synchronizes spawned networked GameObjects.
-   [NetworkTransformChild](NetworkTransformChild)  
    The Network Transform Child component synchronizes the position and rotation of the child GameObject of a GameObject with a Network Transform component.
-   [NetworkAnimator](NetworkAnimator)  
    The Network Animator component allows you to synchronize animation states for networked objects. It synchronizes state and parameters from an Animator Controller.
-   [NetworkNavMeshAgent](NetworkNavMeshAgent)  
    Coming Soon
-   [NetworkController](NetworkController)  
    Coming Soon
-   [NetworkRigidbody](NetworkRigidbody)  
    Coming Soon

 
