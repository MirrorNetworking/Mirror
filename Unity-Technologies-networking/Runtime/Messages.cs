using System;
using System.Collections.Generic;

#if ENABLE_UNET

namespace UnityEngine.Networking
{
    // This can't be an interface because users don't need to implement the
    // serialization functions, we'll code generate it for them when they omit it.
    public abstract class MessageBase
    {
        // De-serialize the contents of the reader into this message
        public virtual void Deserialize(NetworkReader reader) {}

        // Serialize the contents of this message into the writer
        public virtual void Serialize(NetworkWriter writer) {}
    }
}

namespace UnityEngine.Networking.NetworkSystem
{
    // ---------- General Typed Messages -------------------

    public class StringMessage : MessageBase
    {
        public string value;

        public StringMessage()
        {
        }

        public StringMessage(string v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadString();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(value);
        }
    }

    public class IntegerMessage : MessageBase
    {
        public int value;

        public IntegerMessage()
        {
        }

        public IntegerMessage(int v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)value);
        }
    }

    public class EmptyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader)
        {
        }

        public override void Serialize(NetworkWriter writer)
        {
        }
    }

    // ---------- Public System Messages -------------------

    public class ErrorMessage : MessageBase
    {
        public byte errorCode; // byte instead of int because NetworkServer uses byte anyway. saves bandwidth.

        public override void Deserialize(NetworkReader reader)
        {
            errorCode = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(errorCode);
        }
    }

    public class ReadyMessage : EmptyMessage
    {
    }

    public class NotReadyMessage : EmptyMessage
    {
    }

    public class AddPlayerMessage : MessageBase
    {
        public short playerControllerId;
        public byte[] msgData;

        public override void Deserialize(NetworkReader reader)
        {
            playerControllerId = reader.ReadInt16();
            msgData = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(playerControllerId);
            writer.WriteBytesAndSize(msgData);
        }
    }

    public class RemovePlayerMessage : MessageBase
    {
        public short playerControllerId;

        public override void Deserialize(NetworkReader reader)
        {
            playerControllerId = reader.ReadInt16();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(playerControllerId);
        }
    }

    // ---------- System Messages requried for code gen path -------------------
    /* These are not used directly but manually serialized, these are here for reference.

    public struct CommandMessage
    {
        public int cmdHash;
        public string cmdName;
        public byte[] payload;
    }
    public struct RPCMessage
    {
        public NetworkId netId;
        public int cmdHash;
        public byte[] payload;
    }
    public struct SyncEventMessage
    {
        public NetworkId netId;
        public int cmdHash;
        public byte[] payload;
    }

    internal class SyncListMessage<T> where T: struct
    {
        public NetworkId netId;
        public int cmdHash;
        public byte operation;
        public int itemIndex;
        public T item;
    }

*/

    // ---------- Internal System Messages -------------------

    class ObjectSpawnMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public NetworkHash128 assetId;
        public Vector3 position;
        public Quaternion rotation;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            assetId = reader.ReadNetworkHash128();
            position = reader.ReadVector3();
            rotation = reader.ReadQuaternion();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(assetId);
            writer.Write(position);
            writer.Write(rotation);
            writer.WriteBytesAndSize(payload);
        }
    }

    class ObjectSpawnSceneMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public NetworkSceneId sceneId;
        public Vector3 position;
        public byte[] payload;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            sceneId = reader.ReadSceneId();
            position = reader.ReadVector3();
            payload = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(sceneId);
            writer.Write(position);
            writer.WriteBytesAndSize(payload);
        }
    }

    class ObjectSpawnFinishedMessage : MessageBase
    {
        public byte state; // byte because it's always 0 or 1

        public override void Deserialize(NetworkReader reader)
        {
            state = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(state);
        }
    }

    class ObjectDestroyMessage : MessageBase
    {
        public NetworkInstanceId netId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
        }
    }

    class OwnerMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public short playerControllerId;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            playerControllerId = (short)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)playerControllerId);
        }
    }

    class ClientAuthorityMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public bool authority;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            authority = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.Write(authority);
        }
    }

    class OverrideTransformMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public byte[] payload;
        public bool teleport;
        public int time;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            payload = reader.ReadBytesAndSize();
            teleport = reader.ReadBoolean();
            time = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WriteBytesAndSize(payload);
            writer.Write(teleport);
            writer.WritePackedUInt32((uint)time);
        }
    }

    class AnimationMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public int      stateHash;      // if non-zero, then Play() this animation, skipping transitions
        public float    normalizedTime;
        public byte[]   parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            stateHash = (int)reader.ReadPackedUInt32();
            normalizedTime = reader.ReadSingle();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)stateHash);
            writer.Write(normalizedTime);
            writer.WriteBytesAndSize(parameters);
        }
    }

    class AnimationParametersMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public byte[] parameters;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            parameters = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WriteBytesAndSize(parameters);
        }
    }

    class AnimationTriggerMessage : MessageBase
    {
        public NetworkInstanceId netId;
        public int hash;

        public override void Deserialize(NetworkReader reader)
        {
            netId = reader.ReadNetworkId();
            hash = (int)reader.ReadPackedUInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(netId);
            writer.WritePackedUInt32((uint)hash);
        }
    }

    class LobbyReadyToBeginMessage : MessageBase
    {
        public byte slotId;
        public bool readyState;

        public override void Deserialize(NetworkReader reader)
        {
            slotId = reader.ReadByte();
            readyState = reader.ReadBoolean();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(slotId);
            writer.Write(readyState);
        }
    }
}
#endif //ENABLE_UNET
