# Getting Started

This document describes steps to creating a multiplayer game with MirrorNG. The process described here is a simplified, higher level version of the actual process for a real game; it doesn’t always work exactly like this, but it provides a basic recipe for the process.

## Video tutorials

Check out these [awesome videos](https://www.youtube.com/playlist?list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP) showing you how to get started with mirror. Courtesy of [First Gear Games](https://www.youtube.com/channel/UCGIF1XekJqHYIafvE7l0c2A) also known as Punfish in discord.

## Script Templates
-   Create new Network Behaviours and other common scripts faster

See [Script Templates](ScriptTemplates.md).

## Networking set-up
-   Right click in the Scene, select *Network* > *NetworkManager*. This will create a new GameObject with all the neccessary networking components set up for you.
-   Add the NetworkManagerHUD component to the game object. This provides the default UI for managing the network game state.

See [Using the NetworkManager](../Components/NetworkManager.md).

## Player Prefab
-   Find the Prefab for the player game object in the game, or create a Prefab from the player game object
-   Add the NetworkIdentity component to the player Prefab
-   Set the `playerPrefab` field on the `PlayerSpawner` component  to the player Prefab. You can find this component on the GameObject you created in the first setup.
-   Remove the player game object instance from the Scene if it exists in the Scene

See [Player Objects](../Guides/GameObjects/SpawnPlayer.md) for more information.

## Player movement
-   Add a NetworkTransform component to the player Prefab
-   Check the Client Authority checkbox on the component.
-   Update input and control scripts to respect `IsLocalPlayer`
-   Subscribe to `NetIdentity.OnStartLocalPlayer` event to take control of the Main Camera in the scene for the player.

For example, this script only processes input for the local player:

``` cs
using UnityEngine;
using Mirror;

public class Controls : NetworkBehaviour
{
    void Update()
    {
        if (!IsLocalPlayer)
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

See [State Synchronization](../Guides/Sync/index.md).

## Networked actions
-   Make scripts that perform important actions into NetworkBehaviours instead of MonoBehaviours
-   Update functions that perform important player actions to be `ServerRpc`s

See [Networked Actions](../Guides/Communications/index.md).

## Non-player game objects

Fix non-player prefabs such as enemies:
-   Add the `NetworkIdentify` component
-   Add the `NetworkTransform` component
-   Register spawnable Prefabs with the `ClientObjectManager`
-   Update scripts with game state and actions

## Spawners
-   Potentially change spawner scripts to be NetworkBehaviours
-   Modify spawners to only run on the server (use `IsServer` property or the `OnStartServer` callback)
-   Call `ServerObjectManager.Spawn()` for created game objects

## Spawn positions for players
-   Add a new game object and place it at player’s start location
-   Add this GameObject to the `PlayerSpawner`'s `Positions` list.
