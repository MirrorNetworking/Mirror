# Network Authority

Authority is a way of deciding who owns an object and has control over it. 


## Server Authority

Server authority means that the server has control of an object. Server has authority over an object by default. This means the server would manage and control of all collectible items, moving platforms, NPCs, and any other networked objects that aren't the player.

## Client Authority

Client authority means that the client has control of an object. 

When a client has authority over an object it means that they can call [ServerRpc](Communications/RemoteActions.md) and that the object will automatically be destroyed when the client disconnects.

Even if a client has authority over an object the server still controls SyncVar and control other serialization features. A component will need to use a [Commands](Communications/RemoteActions.md) to update the state on the server in order for it to sync to other clients.


## How to give authority

By default the server has Authority over all objects. The server can give authority to objects that a client needs to control, like the player object. 

If you spawn a player object using `NetworkServer.AddPlayerForConnection` then it will automatically be given authority.


### Using NetworkServer.Spawn

You can give authority to a client when an object is spawned. This is done by passing in the connection to the spawn message
```cs
GameObject go = Instantiate(prefab);
NetworkServer.Spawn(go, connectionToClient);
```

### Using identity.AssignClientAuthority

You can give authority to a client any time using `AssignClientAuthority`. This can be done by calling `AssignClientAuthority` on the object you want to give authority too
```cs
identity.AssignClientAuthority(conn);
```

You may want to do this when a player picks up an item

```cs
// Command on player object
[ServerRpc]
void PickupItem(NetworkIdentity item)
{
    item.AssignClientAuthority(connectionToClient); 
}
```

## How to remove authority

You can use `identity.RemoveClientAuthority` to remove client authority from an object. 

```cs
identity.RemoveClientAuthority();
```

Authority can't be removed from the player object. Instead you will have to replace the player object using `NetworkServer.ReplacePlayerForConnection`.


## On Authority

When authority is given to or removed from an object a message will be sent to that client to notify them. This will cause the `OnStartAuthority` or `OnStopAuthority` functions to be called. 


## Check Authority

### Client Side

The `identity.hasAuthority` property can be used to check if the local player has authority over an object.

### Server Side

The `identity.connectionToClient` property can be check to see which client has authority over an object. If it is null then the server has authority.
