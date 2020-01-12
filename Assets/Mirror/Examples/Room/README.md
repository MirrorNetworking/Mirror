# Room Example

In Build Settings, remove all scenes and add all of the scenes from the Examples\Room\Scenes folder in the following order:
-	OfflineScene
-	RoomScene
-	OnlineScene

If you opened the Offline scene before doing the above steps, you may have to reassign the scenes to the NetworkRoomManagerExt component of the RoomManager scene object.

File -> Build and Run

Start up to 4 built instances:  These will all be client players.

Open the Offline scene in the Editor and press Play

Click LAN Host in the editor: This will be host and the 5th player.  You can also use LAN Server if you prefer.

Click LAN Client in the built instances.

Click Ready in each instance, and finally in the Editor (Host).

Click the Start Game button when all players are ready.

You should now be in the Online scene with your players of random color.

WASDQE keys to move & turn your player capsule.
Collide with the spheres to score points.
Lighter colors score higher.
