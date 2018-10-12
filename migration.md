# Migrating a project from UNet  (HLAPI)

This guide gives you a step by step instruction for migrating your project from HLAP to Mirror.
Mirror is a fork of HLAPI.  As such the migration is straight forward for most projects.

## 1. BACKUP
You have been warned.

## 2. Install Mirror
Get Mirror from the asset store and import it in your project (link to be provided)

## 3. Replace namespace

Replace `UnityEngine.Networking` for `Mirror`  everywhere in your project. For example, if you have this:
```C#
using UnityEngine.Networking;

public class Player : NetworkBehaviour {
    ...
}
```

replace it with:
```C#
using Mirror;

public class Player : NetworkBehaviour {
    ...
}
```
At this point,  you might get some compilation errors.  Don't panic,  these are easy to fix. Keep going

## 4. Remove all channels
As of this writing,  all messages are reliable, ordered, fragmented.  There is no need for channels at all.
To avoid misleading code, we have removed channels.

For example, if you have this code:

```C#
[Command(channel= Channels.DefaultReliable)]
public void CmdMove(int x, int y)
{
    ...
}
```

replace it with:
```C#
[Command]
public void CmdMove(int x, int y)
{
    ...
}
```
The same applies for `[ClientRPC]`, `[NetworkSettings]`, `[SyncEvent]`, `[SyncVar]`, `[TargetRPC]`

## 5. Rename SyncListStruct to SyncListSTRUCT
There is a bug in the original UNET Weaver that makes it mess with our `Mirror.SyncListStruct` without checking the namespace.
Until Unity officially removes UNET in 2019.1, we will have to use the name `SyncListSTRUCT` instead.

So for example, if you have definitions like:

```C#
public class SyncListQuest : SyncListStruct<Quest> {}
```

replace them with:
```C#
public class SyncListQuest : SyncListSTRUCT<Quest> {}
```

## 6. Replace Components
Every networked prefab and scene object needs to be adjusted.  They will be using `NetworkIdentity` from Unet,  and you need to replace that componenent with `NetworkIdentity` from Mirror.  You may be using other network components,  such as `NetworkAnimator` or `NetworkTransform`.   All components from Unet should be replaced with their corresponding component from Mirror.

Note that if you remove and add a NetworkIdentity,  you will need to reassign it in any component that was referencing it.

## 7. Update your firewall and router
LLAPI uses UDP.   Mirror uses TCP by default.  This means you may need to change your router
port forwarding and firewall rules in your machine to expose the TCP port instead of UDP.
This highly depends on your router and operating system.

## Video version

See for yourself how uMMORPG was migrated to Mirror

[![IMAGE ALT TEXT HERE](http://img.youtube.com/vi/LF9rTSS3rlI/0.jpg)](http://www.youtube.com/watch?v=LF9rTSS3rlI)

## Possible Error Messages ##
* TypeLoadException: A type load exception has occurred. - happens if you still have SyncListStruct instead of SyncListSTRUCT in your project.

* NullPointerException: The most likely cause is that you replaced NetworkIdentities or other components but you had them assigned somewhere. Reassign those references.

* `error CS0246: The type or namespace name 'UnityWebRequest' could not be found. Are you missing 'UnityEngine.Networking' using directive?`

    Add this to the top of your script:
    ```C#
    using UnityWebRequest = UnityEngine.Networking.UnityWebRequest ;
    ```
    `UnityWebRequest` is not part of UNet or Mirror,  but it is in the same namespace as UNet. Changing the namespace to Mirror caused your script not to find UnityWebRequest.  The same applies for `WWW` and all `UnityWebRequest` related classes.
