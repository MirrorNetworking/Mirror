# SyncListString

General description of SyncListString

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
