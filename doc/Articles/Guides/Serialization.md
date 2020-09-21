# Serialization

This page goes into depth about Serialization, for the basics see [DataTypes](DataTypes.md).

Mirror creates Serialize and Deserialize functions for types using Weaver. Weaver edits dll after unity compiles them using Mono.Cecil. This allows mirror to have a lot of complex features like SyncVar, ClientRpc and Message  Serialization without the user needed to manually set everything up.

## Rules and tips

There are some rules and limits for what Weaver can do. Some features add complexity and are hard to maintain so have not been implemented. These features are not impossible to implement and could be added if there is a high demand for them.

- You should be able to write Custom Read/Write functions for any type, and Weaver will use.
    - This means if there is a unsupported type like `int[][]` creating a custom Read/Write function will allow you to sync `int[][]` in SyncVar/ClientRpc/etc
- If you have a type that has a field that is not able to be Serialize, you can mark that field with `[System.NonSerialized]` and weaver will ignore it


### Unsupported Types

Some of these types are unsupported due to the complexity they would add, as mentioned above.

> NOTE: Types in this list can have custom writers.

- Jagged and Multidimensional array
- Types that Inherit from `UnityEngine.Component`
- `UnityEngine.Object`
- `UnityEngine.ScriptableObject`
- Generic Types, eg `MyData<T>`
    - Custom Read/Write must declare T, eg `MyData<int>`
- Interfaces
- Types that references themselves

### Built-in Read Write Functions

Mirror provides some built-in Read/Write Functions. They can be found in `NetworkReaderExtensions` and `NetworkWriterExtensions`.

This is a Non-compete list of types that have built-in functions, check the classes above to see the full list.

- Most [c# primitive types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/built-in-types)
- Common Unity structs
    - Vector3
    - Quaternion
    - Rect
    - Ray
    - Guid
- NetworkIdentity, GameObject, Transform
    
#### NetworkIdentity, GameObject, Transform

The `netId` of the Object is sent over the network, and the Object with the same `netId` is returned on the other side. If the `netId` is zero or an object is not found then `null` will be returned.


### Generated Read Write Functions

Weaver will Generate Read Write functions for

- Classes or Structs
- Enums
- Arrays
    - eg `int[]`
- ArraySegments
    - eg `ArraySegment<int>`
- Lists
    - eg `List<int>`

#### Classes and Structs

Weaver will Read/Write every public field in the type, unless the field is marked with `[System.NonSerialized]`. If there is an unsupported type in the class or struct Weaver will fail to make Read/Write functions for it.

> NOTE: Weaver does not check properties

#### Enums

Weaver will use the underlying Type of an enum to Read and Write them. By default this is `int`.

For example `Switch` will use the `byte` Read/Write functions to be Serialized
```cs
public enum Switch : byte
{
    Left,
    Middle,
    Right,
}
```


#### Collections
 
Weaver will Generate writes for the collections listed above. Weaver will use the elements Read/Write function. The element must have a Read/Write function so must be a supported type, or have a custom Read/Write function.

For example:
- `float[]` is a supported type because Mirror has a built-in Read/Write function for `float`.
- `MyData[]` is a supported type as Weaver is able to generate a Read/Write function for `MyData` 
```cs
public struct MyData
{
    public int someValue;
    public float anotherValue;
}
```

## Adding Custom Read Write functions

Read Write functions are static methods in the form of:
```cs
public static void WriteMyType(this NetworkWriter writer, MyType value)
{
    // write MyType data here
}

public static MyType ReadMyType(this NetworkReader reader)
{
    // read MyType data here
}
```

It is best practice to make Read/Write functions [extension methods](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods) so they can be called like `writer.WriteMyType(value)`.

It is a good idea to call them `ReadMyType` and `WriteMyType` so it is obvious what type they are for. However the name of the function doesn't matter, weaver should be able to find it no matter what it is called.

#### Properties Example 

Weaver wont write properties, but a custom writer can be used to send them over the network.

This can be useful if you want to have private set for your properties

```cs
public struct MyData
{
    public int someValue { get; private set; }
    public float anotherValue { get; private set; }

    public MyData(int someValue, float anotherValue)
    {
        this.someValue = someValue;
        this.anotherValue = anotherValue;
    }
}

public static class CustomReadWriteFunctions 
{
    public static void WriteMyType(this NetworkWriter writer, MyData value)
    {
        writer.WriteInt32(value.someValue);
        writer.WriteSingle(value.anotherValue);
    }

    public static MyData ReadMyType(this NetworkReader reader)
    {
        return new MyData(reader.ReadInt32(), reader.ReadSingle());
    }
}
```

#### Unsupported type Example 

Rigidbody is an unsupported type because it inherits from `Component`. But a custom writer can be added so that it is synced using a NetworkIdentity if one is attached.


```cs
public struct MyCollision
{
    public Vector3 force;
    public Rigidbody rigidbody;
}

public static class CustomReadWriteFunctions
{
    public static void WriteMyCollision(this NetworkWriter writer, MyCollision value)
    {
        writer.WriteVector3(value.force);

        NetworkIdentity networkIdentity = value.rigidbody.GetComponent<NetworkIdentity>();
        writer.WriteNetworkIdentity(networkIdentity);
    }

    public static MyCollision ReadMyCollision(this NetworkReader reader)
    {
        Vector3 force = reader.ReadVector3();

        NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
        Rigidbody rigidBody = networkIdentity != null
            ? networkIdentity.GetComponent<Rigidbody>()
            : null;

        return new MyCollision
        {
            force = force,
            rigidbody = rigidBody,
        };
    }
}
```

Above are functions for `MyCollision`, but instead you could add functions for `Rigidbody` and let weaver would generate a writer for `MyCollision`.
```cs 
public static class CustomReadWriteFunctions
{
    public static void WriteRigidbody(this NetworkWriter writer, Rigidbody rigidbody)
    {
        NetworkIdentity networkIdentity = rigidbody.GetComponent<NetworkIdentity>();
        writer.WriteNetworkIdentity(networkIdentity);
    }

    public static Rigidbody ReadRigidbody(this NetworkReader reader)
    {
        NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
        Rigidbody rigidBody = networkIdentity != null
            ? networkIdentity.GetComponent<Rigidbody>()
            : null;

        return rigidBody;
    }
}
```

## Debugging

You can use tools like [dnSpy](https://github.com/0xd4d/dnSpy) to view the complied code after Weaver has altered it. This can help to understand and debug what Mirror and Weaver does.