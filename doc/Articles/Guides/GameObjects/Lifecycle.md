## Lifecycle of a GameObject

Networked GameObjects go through several lifecycle states. 
You can add custom logic to the object lifecycle events by subscribing to the corresponding event in <xref:Mirror.NetworkIdentity>

| Server                             | Client                                    |
| ---------------------------------- | ----------------------------------------- |
| [Instantiate](#server-instantiate) |                                           |
| [Start Server](#server-start)      |                                           |
|                                    | [Instantiate](#client-instantiate)        |
|                                    | [StartAuthority](#client-start-authority) |
|                                    | [StartClient](#start-client)              |
|                                    | [StartLocalPlayer](#start-local-player)   |
|                                    | [StopAuthority](#stop-authority)          |
| [StopServer](#server-stop)         |                                           |
| [Destroy](#server-destroy)         |                                           |
|                                    | [StopClient](#stop-client)                |
|                                    | [Destroy](#client-destroy)                |


> **Note:** In Mirror and UNet, you can add logic to lifecycle events by overriding methods in NetworkBehaviour
> In MirrorNG you do it by subscribing to events in <xref:Mirror.NetworkIdentity>

# Server Instantiate

This is done usual by you using Unity's `GameObject.Instantiate` 
This goes through the regular GameObject Lifecycle events such as Awake, Start, Enabled, etc..
Basically this is outside MirrorNG's control.

[Scene Objects](SceneObjects.md) are normally instantiated as part of the scene.

# Server Start

To start a server object,  [spawn it](SpawnObject.md). 
If you wish to perform some logic when the object starts in the server, add a 
component in your gameobject with your own method and subscribe to 
<xref:Mirror.NetworkIdentity.OnStartServer>

For example:

```cs
class MyComponent : MonoBehaviour {

    public void Awake() {
        GetComponent<NetworkIdentity>.OnStartServer.AddListener(OnStartServer);
    }

    public void OnStartServer() {
        Debug.Log("The object started on the server")
    }
}
```

You can also simply drag your `OnStartServer` method in the <xref:Mirror.NetworkIdentity.OnStartServer> event in the inspector.

During the spawn a message will be sent to all the clients telling them to spawn the object. The message
will include all the data in [SyncVars](../Sync/SyncVars.md), [SyncLists](../Sync/SyncLists.md), [SyncSet](../Sync/SyncHashSet.md), [SyncDictionary](../Sync/SyncDictionary.md)

# Client Instantiate

When an object is spawned,  the server will send a message to the clients telling it to spawn a GameObject and provide 
an asset id.

By default, MirrorNG will look up all the known prefabs looking for that asset id.  
Make sure to add your prefabs in the NetworkClient list of prefabs.
Then MirrorNG will instantiate the prefab,  and it will go through the regular Unity Lifecycle events.
You can customize how objects are instantiated using Spawn Handlers.

Do not add Network logic to these events.  Instead,  use these events to subscribe to network events in NetworkIdentity.

Immediatelly after the object is instantiated, all the data is updated to match the data in the server.

# Client Start Authority

If the object is owned by this client, then NetworkIdentity will invoke the <xref:Mirror.NetworkIdentity.OnStartAuthority>
Subscribe to this event either by using `AddListener`,  or adding your method to the event in the inspector.
Note the Authority can be revoked, and granted again.  Every time the client gains authority, this event will be invoked again.

# Start Client

The event <xref:Mirror.NetworkIdentity.OnStartClient> will be invoked. 
Subscribe to this event by using `AddListener` or adding your method in the event in the inspector

# Start Local Player

If the object spawned is the [player object](SpawnPlayer.md),  the event <xref:Mirror.NetworkIdentity.OnStartLocalPlayer>
is invoked.
Subscribe to this event by using `AddListener` or adding your method in the event in the inspector

# Stop Authority

If the object loses authority over the object, then NetworkIdentity will invoke the <xref:Mirror.NetworkIdentity.OnStopAuthority>
Subscribe to this event either by using `AddListener`,  or adding your method to the event in the inspector.
Note the Authority can be revoked, and granted again.  Every time the client loses authority, this event will be invoked again.

# Server Stop

Either because the client disconnected, the server stopped, 
you called <xref:Mirror.ServerObjectManager.UnSpawn(GameObject)>,  or you called <xref:Mirror.ServerObjectManager.Destroy(GameObject)> the object may stop in the server.
During this state, a message is sent to all the clients to unspawn the object.
The event <xref:Mirror.NetworkIdentity.OnStopServer> will be invoked. 

Subscribe to this event either by using `AddListener`,  or adding your method to the event in the inspector.

# Server Destroy

By default, the server will call `GameObject.Destroy` to destroy the object.  
Note that if it is a [Scene Object](SceneObjects.md) the server will invoke `GameObject.SetActive(false)` instead.  

The regular unity lifecycle events apply.

Note that the server will destroy the object, and will not wait for the clients to unspawn their objects.

# Stop Client

This can be triggered either because the client received an Unspawn message or the client was disconnected
The event <xref:Mirror.NetworkIdentity.OnStopClient> will be invoke.  
Subscribe to this event either by using `AddListener`,  or adding your method to the event in the inspector.

Use it to cleanup any network related resource used by this object.

# Client Destroy

After an object is stopped on the client,  by default unity will call `GameObject.Destroy` if it is a prefab [Spawned Object](SpawnObject.md)
Or it will call `GameObject.SetActive(false)` if it is a [Scene Object](SceneObjects.md)
You can customize how objects are destroying using Spawn Handlers

The normal Unity lifecycle events applies.
