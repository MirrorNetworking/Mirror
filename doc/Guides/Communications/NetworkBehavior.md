# NetworkBehaviour Callbacks

**See also <xref:Mirror.NetworkBehaviour> in the API Reference.**

Like the Network Manager callbacks, there are a number of events relating to network behaviours that can occur over the course of a normal multiplayer game. These include events such as the host starting up, a player joining, or a player leaving. Each of these possible events has an associated callback that you can implement in your own code to take action when the event occurs.

When you create a script which inherits from `NetworkBehaviour`, you can write your own implementation of what should happen when these events occur. To do this, you override the virtual methods on the `NetworkBehaviour` class with your own implementation of what should happen when the given event occurs.

This page lists all the virtual methods (callbacks) that you can implement on `NetworkBehaviour`, and when they occur.

## Server mode

**When a client connects:**
-   `OnStartServer`
-   `OnRebuildObservers`
-   `Start()` function is called

## Client mode

**When a client connects:**
-   `OnStartClient`
-   `OnStartLocalPlayer`
-   `OnStartAuthority`
-   `Start()` function is called

## Host mode

These are only called on the **Player Game Objects** when a client connects:
-   `OnStartServer`
-   `OnStartClient`
-   `OnRebuildObservers`
-   `OnStartAuthority`
-   `OnStartLocalPlayer`
-   `Start()` function is called
-   `OnSetLocalVisibility`
