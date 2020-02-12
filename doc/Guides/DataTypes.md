# Data types

[![built in data types video tutorial](../images/video_tutorial.png)](https://www.youtube.com/watch?v=DIIeGYAawY0&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=9)

The client and server can pass data to each other via [Remote methods](Communications/RemoteActions.md), [State Synchronization](Sync/index.md) or via [Network Messages](Communications/NetworkMessages.md)

Mirror supports a number of data types you can use with these, including:
- Basic c# types (byte, int, char, uint, UInt64, float, string, etc)
- Built-in Unity math type (Vector3, Quaternion, Rect, Plane, Vector3Int, etc)
- URI
- NetworkIdentity
- Game object with a NetworkIdentity component attached.
- Structures with any of the above (it's recommended to implement IEquatable\<T\> to avoid boxing and to have the struct readonly, cause modifying one of fields doesn't cause a resync)
- Classes as long as each field has a supported data type.
- ScriptableObject as long as each field has a supported data type
- Arrays of any of the above (not supported with syncvars or synclists)
- ArraySegments of any of the above (not supported with syncvars or synclists)

## Custom Data Types

[![built in data types video tutorial](../images/video_tutorial.png)](https://www.youtube.com/watch?v=svXHy2TGaS8&list=PLkx8oFug638oBYF5EOwsSS-gOVBXj1dkP&index=10)

Sometimes you don't want mirror to generate serialization for your own types. For example, instead of serializing quest data, you may want to serialize just the quest id, and the receiver can look up the quest by id in a predefined list.

Sometimes you may want to serialize data which uses a different type not supported by Mirror, such as DateTime or System.Uri

You can add support for any type by adding extension methods to `NetworkWriter` and `NetworkReader`. For example, to add support for `DateTime`, add this somewhere in your project:

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

...then you can use `DateTime` in your `[Command]` or `SyncList`

## Inheritance and Polymorphism

Sometimes you might want to send a polymorphic data type to your commands. Mirror does not serialize the type name to keep messages small and for security reasons, therefore Mirror cannot figure out the type of object it received by looking at the message.

>   **This code does not work out of the box.**

```cs
class Item 
{
    public string name;
}

class Weapon : Item
{
    public int hitPoints;
}

class Armor : Item
{
    public int hitPoints;
    public int level;
}

class Player : NetworkBehaviour
{
    [Command]
    void CmdEquip(Item item)
    {
        // IMPORTANT: this does not work. Mirror will pass you an object of type item
        // even if you pass a weapon or an armor.
        if (item is Weapon weapon)
        {
            // The item is a weapon, 
            // maybe you need to equip it in the hand
        }
        else if (item is Armor armor)
        {
            // you might want to equip armor in the body
        }
    }

    [Command]
    void CmdEquipArmor(Armor armor)
    {
        // IMPORTANT: this does not work either,  you will receive an armor,  but 
        // the armor will not have a valid Item.name,  even if you passed an armor with name
    }
}
```

CmdEquip will work if you provide a custom serializer for the `Item` type. For example:

```cs

public static class ItemSerializer 
{
    const byte WEAPON = 1;
    const byte ARMOR = 2;

    public static void WriteItem(this NetworkWriter writer, Item item)
    {
        if (item is Weapon weapon)
        {
            writer.WriteByte(WEAPON);
            writer.WriteString(weapon.name);
            writer.WritePackedInt32(weapon.hitPoints);
        }
        else if (item is Armor armor)
        {
            writer.WriteByte(ARMOR);
            writer.WriteString(armor.name);
            writer.WritePackedInt32(armor.hitPoints);
            writer.WritePackedInt32(armor.level);
        }
    }

    public static Item ReadItem(this NetworkReader reader)
    {
        byte type = reader.ReadByte();
        switch(type)
        {
            case WEAPON:
                return new Weapon
                {
                    name = reader.ReadString(),
                    hitPoints = reader.ReadPackedInt32()
                };
            case ARMOR:
                return new Armor
                {
                    name = reader.ReadString(),
                    hitPoints = reader.ReadPackedInt32(),
                    level = reader.ReadPackedInt32()
                };
            default:
                throw new Exception($"Invalid weapon type {type}");
        }
    }
}
```

## Scriptable Objects

People often want to send scriptable objects from the client or server. For example, you may have a bunch of swords created as scriptable objects and you want put the equipped sword in a syncvar. This will work fine, Mirror will generate a reader and writer for scriptable objects by calling ScriptableObject.CreateInstance and copy all the data. 

However the generated reader and writer are not suitable for every occasion. Scriptable objects often reference other assets such as textures, prefabs, or other types that can't be serialized. Scriptable objects are often saved in the in the Resources folder. Scriptable objects sometimes have a large amount of data in them. The generated reader and writers may not work or may be inneficient for these situations.

Instead of passing the scriptable object data,  you can pass the name and the other side can lookup the same object by name. This way you can have any kind of data in your scriptable object. You can do that by providing a custom reader and writer.  Here is an example:

```cs
[CreateAssetMenu(fileName = "New Armor", menuName = "Armor Data")]
class Armor : ScriptableObject
{
    public int Hitpoints;
    public int Weight;
    public string Description;
    public Texture2D Icon;
    // ...
}

public static class ArmorSerializer 
{
    public static void WriteArmor(this NetworkWriter writer, Armor armor)
    {
       // no need to serialize the data, just the name of the armor
       writer.WriteString(armor.name);
    }

    public static Armor ReadArmor(this NetworkReader reader)
    {
        // load the same armor by name.  The data will come from the asset in Resources folder
        return Resources.Load<Armor>(reader.ReadString());
    }
}
```

