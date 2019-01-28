# NetworkLobbyManager

The NetworkLobbyManager is an extension of [NetworkManager](NetworkManager).

Create a Lobby Scene, and a Game Scene if you don't already have one.

In the Lobby Scene, add an empty game object called Lobby Manager and give it a NetworkLobbyManager component.  A Telepathy Transport component will be added automatically.

Also add a NetworkManagerHUD component to Lobby Manager.

Save the Lobby Scene, and then drag it to **both** the Offline Scene field and the Lobby Scene field in Lobby Manager.

Drag your Game Scene to the Gameplay Scene field in Lobby Manager.

NOTE: Offline and Online Scene are separate from Lobby and Gameplay Scene... **do not** assign anything to Online Scene or the Lobby will be bypassed!

Create an empty game object in the scene called LobbyPlayer and give it a NetworkLobbyPlayer component.  A Network Identity component will be added automatically.  In the Network Identity component, check the box for Local Player Authority.

Drag the Lobby Player to a folder in your Project to make it a prefab, then remove it from the scene.

Select Lobby Manager and drag the Lobby Player prefab you just made to the Lobby Player Prefab field.

Drag your normal Player prefab to the Player Prefab field...this can be any game object with a Network Identity on it.

If you already have a Network Manager in your Game Scene you'll need to remove it and apply it's settings to the NetworkLobbyManager, except for Offline and Online Scene as noted above.

Don't assign anything in the Lobby Slots list...that's for visualization in the inspector during testing only.

This will do for the moment until I get a nicer doc added to the formal docs.

![The Network Lobby Manager component, as viewed in the inspector](NetworkLobbyManager.png)
