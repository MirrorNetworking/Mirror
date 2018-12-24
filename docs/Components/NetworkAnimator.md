# NetworkAnimator

The Network Animator component allows you to synchronize animation states for networked objects. It synchronizes state and parameters from an Animator Controller.

Note that if you create a Network Animator component on an empty GameObject, Mirror also creates a Network Identity component and an Animator component on that GameObject.

![The Network Animator component in the Inspector window](NetworkAnimatorComponent.png)

-   **Animator**  
    Use this field to define the Animator component you want the Network Animator to synchronize with.

## Details

The Network Animator ensures the synchronization of GameObject animation across the network - meaning that all players see the animation happen at the same. There are two kinds of authority for networked animation (see documentation on Network system concepts for more information about authority)):

-   If the GameObject has authority on the client, you should animate it locally on the client that owns the GameObject. That client sends the animation state information to the server, which broadcasts it to all the other clients. For example, this would be suitable for player characters.
-   If the GameObject has authority on the server, then you should animate it on the server. The server then sends state information to all clients. This is common for animated GameObjects that are not related to a specific client, such as non-player characters.

The Network Animator synchronizes the animation parameters checked in the Inspector window. It does not automatically synchronize animation triggers. A GameObject with authority can use the SetTrigger function to fire an animation trigger on other clients.

The GetParameterAutoSend and SetParameterAutoSend functions can be used to control which individual animator parameters should be automatically synchronized.
