# Data types

The client and server can pass data to each other via [Remote methods](Communications/RemoteActions.md), [State Synchronization](Sync/index.md) or via [Network Messages](Communications/NetworkMessages.md)

Mirror supports a number of data types you can use with these, including:
- Basic c# types (byte, int, char, uint, float, string, UInt64, etc)
- Built-in Unity math type (Vector3, Quaternion, Rect, Plane, Vector3Int, etc)
- NetworkIdentity
- Game object with a NetworkIdentity component attached.
- Structures with any of the above (it's recommended to implement IEquatable\<T\> to avoid boxing and to have the struct readonly, cause modifying one of fields doesn't cause a resync)
- Arrays of any of the above (not supported with syncvars or synclists)
- ArraySegments of any of the above (not supported with syncvars or synclists)

## Custom Data Types

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

## Polymorphism

Sometimes you might want to send a polymorphic data type to your commands. Mirror does not serialize the type name to keep messages small and for security reasons, therefore Mirror cannot figure out the type of object it received by looking at the message.

>   **This code does not work out of the box.**

```cs
class Item 
{
    public String name;
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
    public void CmdEquip(Item item)
    {
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

    public OnGUI()
    {
        if (isLocalPlayer)
        {
            if (GUI.Button(new Rect(10, 10, 50, 50), "Equip Weapon"))
            {
                CmdEquip(new Weapon() 
                {
                    name = "Excalibur",
                    hitPoints= 100
                });
            }

            if (GUI.Button(new Rect(10, 70, 50, 30), "Equip Armor"))
            {
                CmdEquip(new Armor() 
                {
                    name = "Gold Armor",
                    hitPoints= 100,
                    Level = 3
                });
            }
        }
    }
}
```

The above code works if you provide a custom serializer for the `Item` type. For example:

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
                    hitPoints = reader.ReadPackedInt32()
                };
            default:
                throw new Exception($"Invalid weapon type {type}");
        }
    }
}
```

## Inheritance

Data types do not support inheritance yet:

>   **This does not work out of the box.**

```cs
class Player : NetworkBehaviour
{
    [Command]
    public void CmdEquip(Armor armor)
    {
        // Mirror will give you an armor object, 
        // but the fields in Armor's parent class will be null.
    }
}
```

However, you can get it to work if you provide a custom serializer for Armor.
