# Got ID?

## Asset Id

MirrorNG uses GUID for Asset Ids. Every prefab with a NetworkIdentity component has an Asset Id, which is simply Unity's AssetDatabase.AssetPathToGUID converted to 16 bytes. MirrorNG needs that to know which prefabs to spawn.

## Scene Id

MirrorNG uses uint for Scene Ids. Every game object with a NetworkIdentity in the scene (hierarchy) is assigned a scene id in OnPostProcessScene. MirrorNG needs that to distinguish scene objects from each other, because Unity has no unique id for different game objects in the scene.

## Network Instance Id (a.k.a. NetId)

MirrorNG uses uint for NetId. Every NetworkIdentity is assigned a NetId in NetworkIdentity.OnStartServer, or after spawning it. MirrorNG uses the id when passing messages between client and server to tell which object is the recipient of the message.

## Connection Id

Every network connection has a connection id, which is assigned by the low level Transport layer. Connection id 0 is reserved for the local connection when the server is also a client (host)
