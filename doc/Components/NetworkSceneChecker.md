# NetworkSceneChecker

The Network Scene Checker component controls the visibility of game objects for network clients, based on which scene they're in.

![Network Scene Checker component](NetworkSceneCheck.png)

-   **Force Hidden**  
    Tick this checkbox to hide this object from all players.

With the Network Scene Checker, a game running on a client doesn’t have information about game objects that are not visible. This has two main benefits: it reduces the amount of data sent across the network, and it makes your game more secure against hacking.

This component would typically be used when the server has several subscenes loaded and needs to isolate networked objects to the subscene they're in.

A game object with a Network Scene Checker component must also have a Network Identity component. When you create a Network Scene Checker component on a game object, Mirror also creates a Network Identity component on that game object if it does not already have one.

Scene objects with a Network Scene Checker component are disabled when they're not in the same scene, and spawned objects are destroyed when they're not in the same scene.
