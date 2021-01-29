# Remote Actions

The network system has ways to perform actions across the network. These type of actions are sometimes called Remote Procedure Calls. There are two types of RPCs in the network system, ServerRpc - which are called from the client and run on the server; and ClientRpc calls - which are called on the server and run on clients.

The diagram below shows the directions that remote actions take:

![Data Flow Graph](UNetDirections.jpg)

## Server RPC Calls

Server RPC Calls are sent from player objects on the client to player objects on the server. For security, Server RPC Calls can only be sent from YOUR player object by default, so you cannot control the objects of other players.  You can bypass the authority check using `[ServerRpc(requireAuthority = false)]`.

To make a function into a Server RPC Calls, add the [ServerRpc] custom attribute to it. This function will now be run on the server when it is called on the client. Any parameters of [allowed data type](../DataTypes.md) will be automatically passed to the server with the Server RPC Call.

Server RPC Calls functions cannot be static. 

``` cs
public class Player : NetworkBehaviour
{
    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKey(KeyCode.X))
            DropCube();
    }

    // assigned in inspector
    public GameObject cubePrefab;

    [ServerRpc]
    void DropCube()
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

Be careful of sending ServerRpcs from the client every frame! This can cause a lot of network traffic.

### Returning values

ServerRpcs can return values.  It can take a long time for the server to reply, so they must return a UniTask which the client can await.
To return a value,  add a return value using `UniTask<MyReturnType>` where `MyReturnType` is any [supported MirrorNG type](../DataTypes.md).  In the server you can make your method async,  or you can use `UniTask.FromResult(myresult);`.  For example:

```cs
public class Shop: NetworkBehavior {

    [ServerRpc]
    public UniTask<int> GetPrice(string item) 
    {
        switch (item) 
        {
             case "turnip":
                 return UniTask.FromResult(10);
             case "apple":
                return UniTask.FromResult(3);
             default:
                return UniTask.FromResult(int.MaxValue);
        }
    }

    [Client]
    public async UniTaskVoid DisplayTurnipPrice() 
    {
        // call the RPC and wait for the response without blocking the main thread
        int price = await GetPrice("turnip");
        Debug.Log($"Turnips price {price}");
    }
}
```

### ServerRpc and Authority

It is possible to invoke ServerRpcs on non-player objects if any of the following are true:

- The object was spawned with client authority
- The object has client authority set with `NetworkIdentity.AssignClientAuthority`
- the Server RPC Call has the `requireAuthority` option set false.  
    - You can include an optional `NetworkConnectionToClient sender = null` parameter in the Server RPC Call method signature and MirrorNG will fill in the sending client for you.
    - Do not try to set a value for this optional parameter...it will be ignored.

Server RPC Calls sent from these object are run on the server instance of the object, not on the associated player object for the client.

```cs
public enum DoorState : byte
{
    Open, Closed
}

public class Door : NetworkBehaviour
{
    [SyncVar]
    public DoorState doorState;

    [ServerRpc(requireAuthority = false)]
    public void CmdSetDoorState(DoorState newDoorState, NetworkConnectionToClient sender = null)
    {
        if (sender.identity.GetComponent<Player>().hasDoorKey)
            doorState = newDoorState;
    }
}
```

## ClientRpc Calls

ClientRpc calls are sent from objects on the server to objects on clients. They can be sent from any server object with a NetworkIdentity that has been spawned. Since the server has authority, then there no security issues with server objects being able to send these calls. To make a function into a ClientRpc call, add the [ClientRpc] custom attribute to it. This function will now be run on clients when it is called on the server. Any parameters of [allowed data type](../DataTypes.md) will automatically be passed to the clients with the ClientRpc call..

ClientRpc functions cannot be static.  They must return `void`

ClientRpc messages are only sent to observers of an object according to its [Network Visibility](../Visibility.md). Player objects are always obeservers of themselves. In some cases, you may want to exclude the owner client when calling a ClientRpc.  This is done with the `excludeOwner` option: `[ClientRpc(excludeOwner = true)]`.

``` cs
public class Player : NetworkBehaviour
{
    int health;

    public void TakeDamage(int amount)
    {
        if (!isServer) return;

        health -= amount;
        Damage(amount);
    }

    [ClientRpc]
    void Damage(int amount)
    {
        Debug.Log("Took damage:" + amount);
    }
}
```

When running a game as a host with a local client, ClientRpc calls will be invoked on the local client even though it is in the same process as the server. So the behaviours of local and remote clients are the same for ClientRpc calls.

You can also specify which client gets the call with the `target` parameter. 

If you only want the client that owns the object to be called,  use `[ClientRpc(target = Client.Owner)]` or you can specify which client gets the message by using `[ClientRpc(target = Client.Connection)]` and passing the connection as a parameter.  For example:

``` cs
public class Player : NetworkBehaviour
{
    int health;

    [Server]
    void Magic(GameObject target, int damage)
    {
        target.GetComponent<Player>().health -= damage;

        NetworkIdentity opponentIdentity = target.GetComponent<NetworkIdentity>();
        DoMagic(opponentIdentity.connectionToClient, damage);
    }

    [ClientRpc(target = Client.Connection)]
    public void DoMagic(NetworkConnection target, int damage)
    {
        // This will appear on the opponent's client, not the attacking player's
        Debug.Log($"Magic Damage = {damage}");
    }

    [Server]
    void HealMe()
    {
        health += 10;
        Healed(10);
    }

    [ClientRpc(target = client.Owner)]
    public void Healed(int amount)
    {
        // No NetworkConnection parameter, so it goes to owner
        Debug.Log($"Health increased by {amount}");
    }
}
```

## Arguments to Remote Actions

The arguments passed to ServerRpc and ClientRpc calls are serialized and sent over the network. You can use any [supported MirrorNG type](../DataTypes.md).

Arguments to remote actions cannot be sub-components of game objects, such as script instances or Transforms.
