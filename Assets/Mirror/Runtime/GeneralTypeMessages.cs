using System;
using UnityEngine;

namespace Mirror
{
    public class StringMessage : MessageBase
    {
        public string value;

        public StringMessage() {}

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

    public class ByteMessage : MessageBase
    {
        public byte value;

        public ByteMessage() {}

        public ByteMessage(byte v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadByte();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(value);
        }
    }

    public class BytesMessage : MessageBase
    {
        public byte[] value;

        public BytesMessage() {}

        public BytesMessage(byte[] v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadBytesAndSize();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WriteBytesAndSize(value);
        }
    }

    public class IntegerMessage : MessageBase
    {
        public int value;

        public IntegerMessage() {}

        public IntegerMessage(int v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadPackedInt32();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedInt32(value);
        }
    }

    public class DoubleMessage : MessageBase
    {
        public double value;

        public DoubleMessage() {}

        public DoubleMessage(double v)
        {
            value = v;
        }

        public override void Deserialize(NetworkReader reader)
        {
            value = reader.ReadDouble();
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(value);
        }
    }

    public class EmptyMessage : MessageBase
    {
        public override void Deserialize(NetworkReader reader) {}

        public override void Serialize(NetworkWriter writer) {}
    }
}
