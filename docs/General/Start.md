# Getting Started

This document describes steps to converting a single player game to a multiplayer game, using the new Unity Multiplayer networking system. The process described here is a simplified, higher level version of the actual process for a real game; it doesn’t always work exactly like this, but it provides a basic recipe for the process.

## NetworkManager set-up

-   Add a new GameObject to the Scene and rename it “NetworkManager”.
-   Add the NetworkManager component to the “NetworkManager” GameObject.
-   Add the NetworkManagerHUD component to the GameObject. This provides the default UI for managing the network game state.

See [Using the NetworkManager](/Mirror/Components/NetworkManager).

## Player Prefab

-   Find the Prefab for the player GameObject in the game, or create a Prefab from the player GameObject
-   Add the NetworkIdentity component to the player Prefab
-   Check the LocalPlayerAuthority box on the NetworkIdentity
-   Set the `playerPrefab` in the NetworkManager’s Spawn Info section to the player Prefab
-   Remove the player GameObject instance from the Scene if it exists in the Scene

See [Player Objects](/Mirror/Concepts/GameObjects/SpawnPlayer) for more information.

## Player movement

-   Add a NetworkTransform component to the player Prefab
-   Update input and control scripts to respect `isLocalPlayer`
-   Fix Camera to use spawned player and `isLocalPlayer`

For example, this script only processes input for the local player:

``` cs
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

## Basic player game state

-   Make scripts that contain important data into NetworkBehaviours instead of MonoBehaviours
-   Make important member variables into SyncVars

See [State Synchronization](/Mirror/Concepts/StateSync).

## Networked actions

-   Make scripts that perform important actions into NetworkBehaviours instead of MonoBehaviours
-   Update functions that perform important player actions to be commands

See [Networked Actions](/Mirror/Concepts/Communications/).

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

## Spawn positions for players

-   Add a new GameObject and place it at player’s start location
-   Add the NetworkStartPosition component to the new GameObject
