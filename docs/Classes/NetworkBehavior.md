# NetworkBehavior

NetworkBehaviour scripts work with GameObjects that have a NetworkIdentity component. These scripts can perform high-level API functions such as Commands, ClientRPCs, SyncEvents and SyncVars.

With the server-authoritative system of Mirror, the server must use the NetworkServer.Spawn function to spawn GameObjects with Network Identity components. Spawning them this way assigns them a NetworkInstanceId and creates them on clients connected to the server.

**Note:** This is not a component that you can add to a GameObject directly. Instead, you must create a script which inherits from `NetworkBehaviour` (instead of the default `MonoBehaviour`), then you can add your script as a component to a GameObject.

## Properties

-   **isLocalPlayer**  
    Returns true if this GameObject is the one that represents the player on the local client.
-   **isServer**  
    Returns true if this GameObject is running on the server, and has been spawned.
-   **isClient**  
    Returns true if this GameObject is on the client and has been spawned by the server.
-   **hasAuthority**  
    Returns true if this GameObject is the authoritative version of the GameObject, meaning it is the source for changes to be synchronized. For most GameObjects, this returns true on the server. However, if the localPlayerAuthority value on the NetworkIdentity is true, the authority rests with that player’s client, and this value is true on that client instead of on the server.
-   **netId**  
    The unique network ID of this GameObject. The server assigns this at runtime. It is unique for all GameObjects in that network session.
-   **playerControllerId**  
    The ID of the player associated with this NetworkBehaviour script. This is only valid if the object is a local player.
-   **connectionToServer**  
    The NetworkConnection associated with the Network Identity component attached to this GameObject. This is only valid for **player objects** on the client.
-   **connectionToClient**  
    The NetworkConnection associated with the Network Identity component attached to this GameObject. This is only valid for player GameObjects on the server.
-   **localPlayerAuthority**  
    This value is set on the Network Identity component and is accessible from the NetworkBehaviour script for convenient access in scripts.

NetworkBehaviour scripts have the following features:

-   Synchronized variables
-   Network callbacks
-   Server and client functions
-   Sending commands
-   Client RPC calls
-   Networked events

![Data Flow Graph](UNetDirections.jpg)

## Synchronized variables

You can synchronize member variables of NetworkBehaviour scripts from the server to clients. The server is authoritative in this system, so synchronization only takes place in the direction of server to client.

Use the SyncVar attribute to tag member variables as synchronized. Synchronized variables can be any basic type (bool, byte, sbyte, char, decimal, double, float, int, uint, long, ulong, short, ushort, string), but not classes, lists, or other collections.

```cs
public class SpaceShip : NetworkBehaviour
{
    [SyncVar]
    public int health;

    [SyncVar]
    public string playerName;
}
```

When the value of a `SyncVar` changes on the server, the server automatically sends the new value to all ready clients in the game, and updates the corresponding `SyncVar` values on those clients. When GameObjects spawn, they are created on the client with the latest state of all `SyncVar` attributes from the server.

**Note:** To make a request from a client to the server, you need to use commands, not synchronized variables. See documentation on Sending commands for more information.

## Network callbacks

There are built-in callback functions which are invoked on NetworkBehaviour scripts for various network events. These are virtual functions on the base class, so you can override them in your own code like this:

```cs
public class SpaceShip : NetworkBehaviour
{
    public override void OnStartServer()
    {
        // disable client stuff
    }

    public override void OnStartClient()
    {
        // register client events, enable effects
    }
}
```

The built-in callbacks are:

-   **OnStartServer**
    called when a GameObject spawns on the server, or when the server is started for GameObjects in the Scene
-   **OnStartClient**
    called when the GameObject spawns on the client, or when the client connects to a server for GameObjects in the Scene
-   **OnSerialize**
    called to gather state to send from the server to clients
-   **OnDeSerialize**
    called to apply state to GameObjects on clients
-   **OnNetworkDestroy**
    called on clients when the server destroys the GameObject
-   **OnStartLocalPlayer**
    called on clients for player GameObjects on the local client (only)
-   **OnRebuildObservers**
    called on the server when the set of observers for a GameObjects is rebuilt
-   **OnSetLocalVisibility**
    called on the client and/or server when the visibility of a GameObject changes for the local client
-   **OnCheckObserver**
    called on the server to check visibility state for a new client

Note that in a peer-hosted setup, when one of the clients is acting as both host and client, both `OnStartServer` and `OnStartClient` are called on the same GameObject. Both these functions are useful for actions that are specific to either the client or server, such as suppressing effects on a server, or setting up client-side events.

## Server and Client functions

You can tag member functions in NetworkBehaviour scripts with custom attributes to designate them as server-only or client-only functions. For example:

```cs
using UnityEngine;
using Mirror;

public class SimpleSpaceShip : NetworkBehaviour
{
    int health;

    [Server]
    public void TakeDamage( int amount)
    {
        // will only work on server
        health -= amount;
    }

    [ServerCallback]
    void Update()
    {
        // engine invoked callback - will only run on server
    }

    [Client]
    void ShowExplosion()
    {
        // will only run on client
    }

    [ClientCallback]
    void Update()
    {
        // engine invoked callback - will only run on client
    }
}
```

`Server` and `[ServerCallback]` return immediately if the client is not active. Likewise, `Client` and `ClientCallback` return immediately if the server is not active.

The `[Server]` and `Client` attributes are for your own custom callback functions. They do not generate compile time errors, but they do emit a warning log message if called in the wrong scope.

The `ServerCallback` and `ClientCallback` attributes are for built-in callback functions that are called automatically by Mirror. These attributes do not cause a warning to be generated.

For more information, see API reference documentation on the attributes discussed:

-   ClientAttribute
-   ClientCallbackAttribute
-   ServerAttribute
-   ServerCallbackAttribute

## Sending commands

To execute code on the server, you must use commands. The high-level API is a server-authoritative system, so commands are the only way for a client to trigger some code on the server.

Only player GameObjects can send commands.

When client player GameObject sends a command, that command runs on the corresponding player GameObject on the server. This routing happens automatically, so it is impossible for a client to send a command for a different player.

To define a command in your code, you must write a function which has:

-   A name that begins with `Cmd`
-   The `Command` attribute

For example:

```cs
using UnityEngine;
using Mirror;

public class SpaceShip : NetworkBehaviour
{
    bool alive;
    float thrusting;
    int spin;

    [ClientCallback] // This code executes on the client, gathering input
    void Update()
    {
        int spin = 0;

        if (Input.GetKey(KeyCode.LeftArrow))
            spin += 1;

        if (Input.GetKey(KeyCode.RightArrow))
            spin -= 1;

        // This line triggers the code to run on the server
        CmdThrust(Input.GetAxis("Vertical"), spin);
    }

    [Command] // This code executes on the server after Update() is called from below.
    public void CmdThrust(float thrusting, int spin)
    {   
        if (!alive)
        {
            this.thrusting = 0;
            this.spin = 0;
            return;
        }
            
        this.thrusting = thrusting;
        this.spin = spin;
    }
}
```

Commands are called just by invoking the function normally on the client. Instead of the command function running on the client, it is automatically invoked on the corresponding player GameObject on the server.

Commands are type-safe, have built-in security and routing to the player, and use an efficient serialization mechanism for the arguments to make calling them fast.

## Client RPC calls

Client RPC calls are a way for server GameObjects to make things happen on client GameObjects.

Client RPC calls are not restricted to player GameObjects, and may be called on any GameObject with a Network Identity component.

To define a client RPC call in your code, you must write a function which:

-   Has a name that begins with `Rpc`
-   Has the `ClientRPC` attribute

For example:

```cs
using UnityEngine;
using Mirror;

public class SpaceShipRpc : NetworkBehaviour
{
    [ServerCallback] // This is code run on the server
    void Update()
    {
        int value = UnityEngine.Random.Range(0,100);

        if (value < 10)
        {
            // This invokes the RpcDoOnClient function on all clients
            RpcDoOnClient(value);
        }
    }

    [ClientRpc] // This code will run on all clients
    public void RpcDoOnClient(int foo)
    {
        Debug.Log("OnClient " + foo);
    }

}
```

## Networked events

Networked events are like Client RPC calls, but instead of calling a function on the GameObject, they trigger Events instead.

This allows you to write scripts which can register for a callback when an event is triggered.

To define a Networked event in your code, you must write a function which both:

-   Has a name that begins with `Event`
-   Has the `SyncEvent` attribute

You can use events to build powerful networked game systems that can be extended by other scripts. This example shows how an effect script on the client can respond to events generated by a combat script on the server.

SyncEvent is the base class that Commands and ClientRPC calls are derived from. You can use the SyncEvent attribute on your own functions to make your own event-driven networked gameplay code. Using SyncEvent, you can extend Mirror’s Multiplayer features to better fit your own programming patterns. For example:

```cs
using UnityEngine;
using Mirror;

// Server script
public class MyCombat : NetworkBehaviour
{
    public delegate void TakeDamageDelegate(int amount);
    public delegate void DieDelegate();
    public delegate void RespawnDelegate();
    
    float deathTimer;
    bool alive;
    int health;

    [SyncEvent(channel=1)]
    public event TakeDamageDelegate EventTakeDamage;
    
    [SyncEvent]
    public event DieDelegate EventDie;
    
    [SyncEvent]
    public event RespawnDelegate EventRespawn;

    [ServerCallback]
    void Update()
    {
        // Check if it is time to Respawn
        if (!alive)
        {
            if (Time.time > deathTimer)
            {
                Respawn();
            }
            return;
        }
    }

    [Server]
    void Respawn()
    {
        alive = true;

        // send respawn event to all clients from the Server
        EventRespawn();
    }

    [Server]
    void EventTakeDamage(int amount)
    {
        if (!alive)
            return;
            
        if (health > amount)
            health -= amount;
        else
        {
            health = 0;
            alive = false;

            // send die event to all clients
            EventDie();
            deathTimer = Time.time + 5.0f;
        }
    }
}
```
