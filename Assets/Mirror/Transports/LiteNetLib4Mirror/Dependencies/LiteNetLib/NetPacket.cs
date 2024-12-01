using System;
using LiteNetLib.Utils;

namespace LiteNetLib
{
	internal enum PacketProperty : byte
	{
		Unreliable,
		Channeled,
		Ack,
		Ping,
		Pong,
		ConnectRequest,
		ConnectAccept,
		Disconnect,
		UnconnectedMessage,
		MtuCheck,
		MtuOk,
		Broadcast,
		Merged,
		ShutdownOk,
		PeerNotFound,
		InvalidProtocol,
		NatMessage,
		Empty
	}

	internal sealed class NetPacket
	{
		private static readonly int PropertiesCount = Enum.GetValues(typeof(PacketProperty)).Length;
		private static readonly int[] HeaderSizes;

		static NetPacket()
		{
			HeaderSizes = NetUtils.AllocatePinnedUninitializedArray<int>(PropertiesCount);
			for (int i = 0; i < HeaderSizes.Length; i++)
			{
				switch ((PacketProperty) i)
				{
					case PacketProperty.Channeled:
					case PacketProperty.Ack:
						HeaderSizes[i] = NetConstants.ChanneledHeaderSize;
						break;
					case PacketProperty.Ping:
						HeaderSizes[i] = NetConstants.HeaderSize + 2;
						break;
					case PacketProperty.ConnectRequest:
						HeaderSizes[i] = NetConnectRequestPacket.HeaderSize;
						break;
					case PacketProperty.ConnectAccept:
						HeaderSizes[i] = NetConnectAcceptPacket.Size;
						break;
					case PacketProperty.Disconnect:
						HeaderSizes[i] = NetConstants.HeaderSize + 8;
						break;
					case PacketProperty.Pong:
						HeaderSizes[i] = NetConstants.HeaderSize + 10;
						break;
					default:
						HeaderSizes[i] = NetConstants.HeaderSize;
						break;
				}
			}
		}

		//Header
		public PacketProperty Property
		{
			get => (PacketProperty) (RawData[0] & 0x1F);
			set => RawData[0] = (byte) ((RawData[0] & 0xE0) | (byte) value);
		}

		public byte ConnectionNumber
		{
			get => (byte) ((RawData[0] & 0x60) >> 5);
			set => RawData[0] = (byte) ((RawData[0] & 0x9F) | (value << 5));
		}

		public ushort Sequence
		{
			get => BitConverter.ToUInt16(RawData, 1);
			set => FastBitConverter.GetBytes(RawData, 1, value);
		}

		public bool IsFragmented =>
			(RawData[0] & 0x80) != 0;

		public void MarkFragmented()
		{
			RawData[0] |= 0x80; //set first bit
		}

		public byte ChannelId
		{
			get => RawData[3];
			set => RawData[3] = value;
		}

		public ushort FragmentId
		{
			get => BitConverter.ToUInt16(RawData, 4);
			set => FastBitConverter.GetBytes(RawData, 4, value);
		}

		public ushort FragmentPart
		{
			get => BitConverter.ToUInt16(RawData, 6);
			set => FastBitConverter.GetBytes(RawData, 6, value);
		}

		public ushort FragmentsTotal
		{
			get => BitConverter.ToUInt16(RawData, 8);
			set => FastBitConverter.GetBytes(RawData, 8, value);
		}

		//Data
		public byte[] RawData;
		public int Size;

		//Delivery
		public object UserData;

		//Pool node
		public NetPacket Next;

		public NetPacket(int size)
		{
			RawData = new byte[size];
			Size = size;
		}

		public NetPacket(PacketProperty property, int size)
		{
			size += GetHeaderSize(property);
			RawData = new byte[size];
			Property = property;
			Size = size;
		}

		public static int GetHeaderSize(PacketProperty property)
		{
			return HeaderSizes[(int) property];
		}

		public int GetHeaderSize()
		{
			return HeaderSizes[RawData[0] & 0x1F];
		}

		public bool Verify()
		{
			byte property = (byte) (RawData[0] & 0x1F);
			if (property >= PropertiesCount)
				return false;
			int headerSize = HeaderSizes[property];
			bool fragmented = (RawData[0] & 0x80) != 0;
			return Size >= headerSize && (!fragmented || Size >= headerSize + NetConstants.FragmentHeaderSize);
		}
	}
}
