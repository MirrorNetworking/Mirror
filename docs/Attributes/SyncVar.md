# SyncVar

SyncVars are used to synchronize a variable from the server to all clients
automatically. Don't assign to them from a client, it's pointless. Don't let
them be null, you will get errors. You can use int, long, float, string, Vector3
etc. (all simple types) and NetworkIdentity and GameObject if the GameObject has
a NetworkIdentity attached to it. You can use hooks.
