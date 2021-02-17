# Best Practices

> work in progress


## Custom Messages

If you send custom message regularly then the message should be a struct so that there is no GC/allocations

```cs
struct CreateVisualEffect : NetworkMessage
{
    public Vector3 position;
    public Guid prefabId;

    // Mirror will automatically implement message that are empty
    public void Deserialize(NetworkReader reader) { }
    public void Serialize(NetworkWriter writer) { }
}
```
