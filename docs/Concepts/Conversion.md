# Converting a Single-Player Game to Mirror

This document describes steps to converting a single player game to a multiplayer game, using Mirror. The process described here is a simplified, higher level version of the actual process for a real game; it doesn’t always work exactly like this, but it provides a basic recipe for the process.

## NetworkManager set-up

-   Add a new GameObject and rename it “NetworkManager”.
-   Add the NetworkManager component to the “NetworkManager” GameObject.
-   Add the NetworkManagerHUD component to the GameObject. This provides the default UI​ for managing the network game state.

See Using the NetworkManager.

## Player Prefab Setup

-   Find the Prefab for the player GameObject in the game, or create a Prefab from the player GameObject
-   Add the NetworkIdentity component to the player Prefab
-   Check the LocalPlayerAuthority box on the NetworkIdentity
-   Set the `playerPrefab` in the NetworkManager’s Spawn Info section to the player Prefab
-   Remove the player GameObject instance from the Scene if it exists in the Scene

See Player Objects for more information.

## Player Movement

-   Add a NetworkTransform component to the player Prefab
-   Update input and control scripts to respect `isLocalPlayer`
-   Fix Camera to use spawned player and `isLocalPlayer`

For example, this script only processes input for the local player:

```
using UnityEngine;
using Mirror;

public class Controls : NetworkBehaviour
{
    void Update()
    {
        if (!isLocalPlayer)
        {
            // exit from update if this is not the local player
            return;
        }

        // handle player input for movement
    }
}
```

## Basic Player Game State

-   Make scripts that contain important data into NetworkBehaviours instead of MonoBehaviours
-   Make important member variables into SyncVars

See State Synchronization.

## Networked Actions

-   Make scripts that perform important actions into NetworkBehaviours instead of MonoBehaviours
-   Update functions that perform important player actions to be commands

See Networked Actions.

## Non-player GameObjects

Fix non-player prefabs such as enemies:

-   Add the NetworkIdentify component
-   Add the NetworkTransform component
-   Register spawnable Prefabs with the NetworkManager
-   Update scripts with game state and actions

## Spawners

-   Potentially change spawner scripts to be NetworkBehaviours
-   Modify spawners to only run on the server (use isServer property or the `OnStartServer()` function)
-   Call `NetworkServer.Spawn()` for created GameObjects

## Spawn Positions for Players

-   Add a new GameObject and place it at player’s start location
-   Add the NetworkStartPosition component to the new GameObject

## Lobby

-   Create Lobby Scene
-   Add a new GameObject to the Scene and rename it to “NetworkLobbyManager”.
-   Add the NetworkLobbyManager component to the new GameObject.
-   Configure the Manager:
    -   Scenes
    -   Prefabs
    -   Spawners
