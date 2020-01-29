# NetworkTransform

[![Network transform video tutorial](../images/video_tutorial.png)](https://www.youtube.com/watch?v=Z0th49NERl4&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=3)

The Network Transform component synchronizes the position, rotation, and scale of networked game objects across the network.

A game object with a Network Transform component must also have a Network Identity component. When you add a Network Transform component to a game object, Mirror also adds a Network Identity component on that game object if it does not already have one.

![The Network Transform component](NetworkTransform.png)

By default, Network Transform is server-authoritative unless you check the box for **Client Authority**.  Client Authority applies to player objects as well as non-player objects that have been specifically assigned to a client.

Under **Sensitivity**, you can set the minimum thresholds of change to the transform values in order for network messages to be generated.  This helps minimize network "noise" for minor twitch and jitter.

You can modify **Compress Rotation** to save some bandwidth when synchronizing the rotation.

You can use the **Network Sync Interval** to specify how often it syncs (in seconds).
