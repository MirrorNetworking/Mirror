# NetworkIdentity

The NetworkIdentity component is used to make a gameobject part of the network
and identify it. It offers 2 different options for configuration:Server Only and
Local Player Authority. They are mutually exclusive, which means either one of
the options or none can be checked.

**Server Only**: This checkbox will ensure that Unity only spawns the gameobject
on the server, and not on clients.

**Local Player Authority**: A gameobject where this checkbox is checked, will
give the player gameobject (or also called *PlayerObject*) authority over it.
This means the authority owner (client or *PlayerObject*) can control this
gameobject e.g. for movement.

If none of these options is checked, the server will have authority over the
object. Changes made by clients (e.g. moving the object) are not allowed and
will not be synchronized.

![](NetworkIdentity.jpg)
