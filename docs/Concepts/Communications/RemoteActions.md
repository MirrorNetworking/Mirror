# Remote Actions

The network system has ways to perform actions across the network. These type of actions are sometimes called Remote Procedure Calls. There are two types of RPCs in the network system, Commands - which are called from the client and run on the server; and ClientRpc calls - which are called on the server and run on clients.

The diagram below shows the directions that remote actions take:

![Data Flow Graph](UNetDirections.jpg)

## Commands

Commands are sent from player objects on the client to player objects on the server. For security, Commands can only be sent from YOUR player object, so you cannot control the objects of other players. To make a function into a command, add the [Command] custom attribute to it, and add the “Cmd” prefix. This function will now be run on the server when it is called on the client. Any arguments will automatically be passed to the server with the command.

Commands functions must have the prefix “Cmd”. This is a hint when reading code that calls the command - this function is special and is not invoked locally like a normal function.

``` cs
public class Player : NetworkBehaviour
{
    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKey(KeyCode.X))
            CmdDropCube();
    }

    // assigned in inspector
    public GameObject cubePrefab;

    [Command]
    void CmdDropCube()
    {
        if (cubePrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 2;
            Quaternion spawnRot = transform.rotation;
            GameObject cube = Instantiate(cubePrefab, spawnPos, spawnRot);
            NetworkServer.Spawn(cube);
        }
    }
}
```

Be careful of sending commands from the client every frame! This can cause a lot of network traffic.

It is possible to send commands from non-player objects that have client authority. These objects must have been spawned with `NetworkServer.SpawnWithClientAuthority` or have authority set with `NetworkIdentity.AssignClientAuthority`. Commands sent from these object are run on the server instance of the object, not on the associated player object for the client.

## ClientRpc Calls

ClientRpc calls are sent from objects on the server to objects on clients. They can be sent from any server object with a NetworkIdentity that has been spawned. Since the server has authority, then there no security issues with server objects being able to send these calls. To make a function into a ClientRpc call, add the [ClientRpc] custom attribute to it, and add the “Rpc” prefix. This function will now be run on clients when it is called on the server. Any arguments will automatically be passed to the clients with the ClientRpc call..

ClientRpc functions must have the prefix “Rpc”. This is a hint when reading code that calls the method - this function is special and is not invoked locally like a normal function.

``` cs
public class Player : NetworkBehaviour
{
    int health;

    public void TakeDamage(int amount)
    {
        if (!isServer) return;

        health -= amount;
        RpcDamage(amount);
    }

    [ClientRpc]
    void RpcDamage(int amount)
    {
        Debug.Log("Took damage:" + amount);
    }
}
```

When running a game as a host with a local client, ClientRpc calls will be invoked on the local client even though it is in the same process as the server. So the behaviours of local and remote clients are the same for ClientRpc calls.

## TargetRpc Calls

TargetRpc functions are called by user code on the server, and then invoked on the corresponding client object on the client of the specified NetworkConnection. The arguments to the RPC call are serialized across the network, so that the client function is invoked with the same values as the function on the server. These functions must begin with the prefix "Target" and cannot be static.

The first argument to an TargetRpc function must be a NetworkConnection object.

This example shows how a client can use a Command to make a request from the server (`CmdMagic`) by including its own `connectionToClient` as one of the parameters of the TargetRpc invoked directly from that Command:

``` cs
public class Player : NetworkBehaviour
{
    [Command]
    void CmdMagic(int damage)
    {
        TargetDoMagic(connectionToClient, damage);
    }

    [TargetRpc]
    public void TargetDoMagic(NetworkConnection target, int damage)
    {
        // This output will appear on the client that called the [Command] above
        Debug.LogFormat("Magic Damage = {0}", damage);
    }
}
```

## Arguments to Remote Actions

The arguments passed to commands and ClientRpc calls are serialized and sent over the network. These arguments can be:

-   basic types (byte, int, float, string, UInt64, etc)

-   arrays of basic types

-   structs containing allowable types

-   built-in unity math types (Vector3, Quaternion, etc)

-   NetworkIdentity

-   game object with a NetworkIdentity component attached

Arguments to remote actions cannot be sub-components of game objects, such as script instances or Transforms. They cannot be other types that cannot be serialized across the network.
