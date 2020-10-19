# NetworkBehaviour

**See also <xref:Mirror.NetworkBehaviour> in the API Reference.**

Network Behaviour scripts work with game objects that have a NetworkIdentity component. These scripts can perform high-level API functions such as ServerRpcs, ClientRpcs and SyncVars.

With the server-authoritative system of MirrorNG, the server must use the `NetworkServer.Spawn` function to spawn game objects with Network Identity components. Spawning them this way assigns them a `netId` and creates them on clients connected to the server.

**Note:** This is not a component that you can add to a game object directly. Instead, you must create a script which inherits from `NetworkBehaviour` (instead of the default `MonoBehaviour`), then you can add your script as a component to a game object.

NetworkBehaviour scripts have the following features:
- [NetworkBehaviour](#networkbehaviour)
  - [Synchronized variables](#synchronized-variables)
  - [Server and Client functions](#server-and-client-functions)
  - [Server RPC Calls](#server-rpc-calls)
  - [Client RPC Calls](#client-rpc-calls)

![Data Flow Graph](UNetDirections.jpg)

**Note:** NetworkBehaviors in Mirror and in UNet provide virtual functions as a way for you to add logic in response to lifecycle events.  MirrorNG does not,  instead add listeners to the events in [NetworkIdentity](../Components/NetworkIdentity.md).

## Synchronized variables

Your component can have data which is automatically synchronized from the server to the client. You can use [SyncVars](../Guides/Sync/SyncVars.md) as well as [SyncLists](Sync/SyncLists.md), [SyncSet](Sync/SyncHashSet.md) and [SyncDictionary](Sync/SyncDictionary.md) inside a NetworkBehaviour.  They will be automatically propagated to the clients whenever their value change in the server.

## Server and Client functions

You can tag member functions in NetworkBehaviour scripts with custom attributes to designate them as server-only or client-only functions. <xref:Mirror.ServerAttribute> will check that the function is called in the server. Likewise, <xref:Mirror.ClientAttribute> will check if the function is called in the client.

For more information, see [Attributes](Attributes.md).

## Server RPC Calls

To execute code on the server, you must use Server RPC calls. The high-level API is a server-authoritative system, so ServerRpc are the only way for a client to trigger some code on the server.

Only player game objects can send ServerRpcs.

When a client player game object sends a ServerRpc, that ServerRpc runs on the corresponding player game object on the server. This routing happens automatically, so it is impossible for a client to send a ServerRpc for a different player.

To define a Server RPC Call in your code, you must write a function which has:
-   A name that begins with `Cmd`
-   The `ServerRpc` attribute

Server RPC Calls are called just by invoking the function normally on the client. Instead of the ServerRpc function running on the client, it is automatically invoked on the corresponding player game object on the server.

Server RPC Calls are type-safe, have built-in security and routing to the player, and use an efficient serialization mechanism for the arguments to make calling them fast.

See [Communications](Communications/index.md) and related sections for more information.

## Client RPC Calls

Client RPC calls are a way for server game objects to make things happen on client game objects.

Client RPC calls are not restricted to player game objects, and may be called on any game object with a Network Identity component.

To define a Client RPC call in your code, you must write a function which:
-   Has a name that begins with `Rpc`
-   Has the `ClientRpc` attribute

See [Communications](Communications/index.md) and related sections for more information.
