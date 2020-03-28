# Network Room Player

The Network Room Player stores per-player state for the [Network Room Manager](NetworkRoomManager.md) while in the room. When using this component, you need to write a script which allows players to indicate they are ready to begin playing, which sets the ReadyToBegin property.

A game object with a Network Room Player component must also have a Network Identity component. When you create a Network Room Player component on a game object, Unity also creates a Network Identity component on that game object if it does not already have one.

![Network Room Player](NetworkRoomPlayer.png)
-   **Show Room GUI**  
    Enable this to show the developer GUI for players in the room. This UI is only intended to be used for ease of development. This is enabled by default.
-   **Ready To Begin**  
    Diagnostic indicator that a player is Ready.
-   **Index**  
    Diagnostic index of the player, e.g. Player 1, Player 2, etc.
-   **Network Sync Interval**  
    The rate at which information is sent from the Network Room Player to the server.

## Methods

### Client Virtual Methods

```cs
public virtual void OnClientEnterRoom() {}

public virtual void OnClientExitRoom() {}

public virtual void OnClientReady(bool readyState) {}
```
