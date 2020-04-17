# Network Scene Checker

The Network Match Checker component controls visibility of networked objects based on match id.

![Network Scene Checker component](NetworkMatchChecker.png)

Any object with this component on it will only be visible to other objects in the same match.

This would be used to isolate players to their respective matches within a single game server instance.

When you create a match, generate and store, in a List for example, a new match id with `System.Guid.NewGuid();` and assign the same match id to the Network Scene Checker via `GetComponent<NetworkMatchChecker>().matchId`.

Mirror's built-in Observers system will isolate SyncVar's and ClientRpc's on networked objects to only send updates to clients with the same match id.
