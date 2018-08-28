# Migrating a project from UNet  (HLAPI) #

This guide gives you a step by step instruction for migrating your project from HLAP to Mirror.
Mirror is a fork of HLAPI.  As such the migration is straight forward for most projects.

## 1) BACKUP ##
You have been warned.

## 2) Install Mirror ##
Get Mirror from the asset store and import it in your project (link to be provided)

## 3) Replace namespace ##

Replace `Unity.Networking` for `Mirror`  everywhere in your project.   So for example, if you have this:
```
using Unity.Networking;

public class Player : NetworkBehaviour {
    ...
}
```

you would replace it with:
```
using Mirror;

public class Player : NetworkBehaviour {
    ...
}
```
At this point,  you might get some compilation errors.  Don't panic,  these are easy to fix. Keep going

## 4) Remove all channels ##
As of this writing,  all messages are reliable, ordered, fragmented.  There is no need for channels at all.
To avoid making misleading code, we have removed channels.

For example, if you have this code:

```
[Command(channel= Channels.DefaultReilable)]
public void CmdMove(int x, int y)
{
    ...
}
```

replace it with:
```
[Command]
public void CmdMove(int x, int y)
{
    ...
}
```
The same applies for `[ClientRPC]`, `[NetworkSettings]`, `[SyncEvent]`, `[SyncVar]`, `[TargetRPC]`

## 5) Rename SyncListStruct to SyncListSTRUCT ##
There is a bug in the original UNET Weaver that makes it mess withour Mirror.SyncListStruct without checking the namespace.
Until Unity officially removes UNET in 2019.1, we will have to use the name `[SyncListSTRUCT]` instead.

So for example, if you have definitions like:

```
public class SyncListQuest : SyncListStruct<Quest> {}
```

replace them with:
```
public class SyncListQuest : SyncListSTRUCT<Quest> {}
```

## 6. Replace Components ##
Every networked prefab and scene object needs to be adjusted.  They will be using `NetworkIdentity` from Unet,  and you need to replace that componenent with `NetworkIdentity` from Mirror.  You may be using other network components,  such as `NetworkAnimator` or `NetworkTransform`.   All components from Unet should be replaced with their corresponding component from Mirror.

## 7. Adjust your firewall and router ##
LLAPI uses UDP.   Mirror uses TCP by default.  This means you may need to change your router
port forwarding and the firewall on your machine to expose the TCP port instead of UDP.
This highly depends on your router and operating system
