# Room Example

In Build Settings, remove all scenes and add all of the scenes from the Examples\Room\Scenes folder in the following order:
-	Offline
-	Room
-	Online

If you opened the Room scene before doing the above steps, you may have to reassign the scenes to the NetworkRoomManagerExt component of the RoomManager scene object.

** Do not assign anything to the Online scene field!**  If you do, the room will be bypassed.  Assign **only* the Offline and Room and Gameplay scene fields in the inspector.

File -> Build and Run

Start up to 4 built instances:  These will all be client players.

Open the Offline scene in the Editor and press Play

Click the Join Game and LAN Host in the editor: This will be host and the 5th player.  You can also use LAN Server if you prefer.

Click Join Game and LAN Client in the built instances.

Click Ready in each instance, and finally in the Editor (Host).

Click the Start Game button when all players are ready.

You should now be in the Online scene with your players of random color.

WASDQE keys to move & turn your player capsule.
Collide with the spheres to score points.
Lighter colors score higher.
