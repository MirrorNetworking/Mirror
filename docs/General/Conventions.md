# Code Conventions

## General Philosophy

**KISS / Occam's Razor** - always use the most simple solution.

**No Premature Optimizations** - MMOs need to run for weeks without issues or
exploits. If you want your code to run 1% faster, spend \$100 on a better CPU.

## Parentheses

Always use {} even for one line ifs. HLAPI did this everywhere, and there is
value in not accidentally missing a line in an if statement because there were
no parentheses.

## Variable naming

'NetworkIdentity identity', not 'NetworkIdentity uv' or similar

If the variable needs a comment the name needs to be changed. For example, `msg
= ... // the message` use `message = ...` without a comment instead

Please Avoid **var** where possible. My text editor doesn't show me the type, it
needs to be obvious. And having two different ways to do the same thing only
creates unnecessary complexity and confusion.

 

## Asset Id

Mirror uses NetworkHash128 for Asset Ids. Every prefab with a NetworkIdentity
component has an Asset Id, which is simply Unity's AssetDatabase.AssetPathToGUID
converted to 16 bytes. Mirror needs that to know which prefabs to spawn.

## Scene Id

Mirror uses uint for Scene Ids. Every GameObject with a NetworkIdentity in the
scene (hierarchy) is assigned a scene id in OnPostProcessScene. Mirror needs
that to distinguish scene objects from each other, because Unity has no unique
id for different GameObjects in the scene.

## Network Instance Id (aka NetId)

Mirror uses uint for NetId. Every NetworkIdentity is assigned a NetId in
NetworkIdentity.OnStartServer, or after spawning it. Mirror uses the id when
passing messages between client and server to tell which object is the recipient
of the message.

## Connection Id

Every network connection has a connection id, which is assigned by the low level
Transport layer. Connection id 0 is reserved for the local connection when the
server is also a client (host)
