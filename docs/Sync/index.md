# Sync Overview

There are some very important optimizations when it comes to bandwidth done in Mirror.

## Channels

There was a bug in HLAPI that caused syncvar to be sent to every channel when they changed. If you had 10 channels, then all the variables would be sent 10 times during the same frame, all as different packages.

## SyncLists

HLAPI SyncLists sent a message for every change immediately. They did not respect the SyncInterval. If you add 10 items to a list, it means sending 10 messages.

In Mirror SyncList were redesigned. The lists queue up their changes, and the changes are sent as part of the syncvar synchronization. If you add 10 items, then only 1 message is sent with all changes according to the next SyncInterval.

We also raised the limit from 32 SyncVars to 64 per NetworkBehavior.

A SyncList can only be of the following type

-   Basic type (byte, int, float, string, UInt64, etc)
-   Built-in Unity math type (Vector3, Quaternion, etc)
-   NetworkIdentity
-   NetworkInstanceId
-   NetworkHash128
-   GameObject with a NetworkIdentity component attached.

## Usage

Don't modify them in Awake, use OnStartServer or Start. SyncListStructs use Structs. C\# structs are value types, just like int, float, Vector3 etc. You can't do synclist.value = newvalue; You have to copy the element, assign the new value, assign the new element to the synclist. You can use hooks like this:

```
// for the official things
[SyncListString(hook="MyHook")] SyncListString mylist;
void MyHook(SyncListString.Operation op, int index) {
    // do things
}
     
// for custom structs
[SyncListString(hook="MyHook")] SyncListStructCustom mylist;
void MyHook(SyncListStructCustom.Operation op, int index) {
    // do things
}
```
