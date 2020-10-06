# Best Practices

> work in progress

## Custom Messages

If you send custom message regularly then the message should be a struct so that there is no GC/allocations

```cs
struct CreateVisualEffect
{
    public Vector3 position;
    public Guid prefabId;
}
```
