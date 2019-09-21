# Data types

The client and server can pass data to each other via [Remote methods](Communications/RemoteActions.md), [State Synchronization](StateSync.md) or via [Network Messages](Communications/NetworkMessages.md)

Mirror supports a number of data types you can use with these,  including:
- Basic c# types (byte, int, char, uint, float, string, UInt64, etc)
- Built-in Unity math type (Vector3, Quaternion, Rect, Plane, Vector3Int, etc)
- NetworkIdentity
- Game object with a NetworkIdentity component attached.
- Structures with any of the above
- Arrays of any of the above (not supported with syncvars or synclists)
- ArraySegments of any of the above (not supported with syncvars or synclists)

# Custom Data Types

Sometimes you don't want mirror to generate serialization for your own types.  For example,  instead of serializing quest data,  you may want to serialize just the quest id,  and the receiver can look up the quest by id in a predefined list.

Sometimes you may want to serialize data which uses a different type not supported by Mirror, such as DateTime or System.Uri

You can add support for any type by adding extension methods to `NetworkWriter` and `NetworkReader`.  For example,  to add support for `DateTime`, add this somewhere in your project:

```cs
public static class DateTimeReaderWriter
{
      public static void WriteDateTime(this NetworkWriter writer, DateTime dateTime)
      {
          writer.WriteInt64(dateTime.Ticks);
      }
     
      public static DateTime ReadDateTime(this NetworkReader reader)
      {
          return new DateTime(reader.ReadInt64());
      }
}
```

Then you can use `DateTime` in your `[Command]` or `SyncList`