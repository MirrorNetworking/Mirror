using System;
using System.Collections.Generic;

namespace LiteNetLib.Utils
{
	public class NetPacketProcessor
	{
		private static class HashCache<T>
		{
			public static readonly ulong Id;

			//FNV-1 64 bit hash
			static HashCache()
			{
				ulong hash = 14695981039346656037UL; //offset
				string typeName = typeof(T).ToString();
				for (var i = 0; i < typeName.Length; i++)
				{
					hash ^= typeName[i];
					hash *= 1099511628211UL; //prime
				}
				Id = hash;
			}
		}

		protected delegate void SubscribeDelegate(NetDataReader reader, object userData);

		private readonly NetSerializer _netSerializer;
		private readonly Dictionary<ulong, SubscribeDelegate> _callbacks = new Dictionary<ulong, SubscribeDelegate>();

		public NetPacketProcessor()
		{
			_netSerializer = new NetSerializer();
		}

		public NetPacketProcessor(int maxStringLength)
		{
			_netSerializer = new NetSerializer(maxStringLength);
		}

		protected virtual ulong GetHash<T>()
		{
			return HashCache<T>.Id;
		}

		protected virtual SubscribeDelegate GetCallbackFromData(NetDataReader reader)
		{
			ulong hash = reader.GetULong();
			if (!_callbacks.TryGetValue(hash, out var action))
			{
				throw new ParseException("Undefined packet in NetDataReader");
			}
			return action;
		}

		protected virtual void WriteHash<T>(NetDataWriter writer)
		{
			writer.Put(GetHash<T>());
		}

		/// <summary>
		/// Register nested property type
		/// </summary>
		/// <typeparam name="T">INetSerializable structure</typeparam>
		public void RegisterNestedType<T>() where T : struct, INetSerializable
		{
			_netSerializer.RegisterNestedType<T>();
		}

		/// <summary>
		/// Register nested property type
		/// </summary>
		/// <param name="writeDelegate"></param>
		/// <param name="readDelegate"></param>
		public void RegisterNestedType<T>(Action<NetDataWriter, T> writeDelegate, Func<NetDataReader, T> readDelegate)
		{
			_netSerializer.RegisterNestedType<T>(writeDelegate, readDelegate);
		}

		/// <summary>
		/// Register nested property type
		/// </summary>
		/// <typeparam name="T">INetSerializable class</typeparam>
		public void RegisterNestedType<T>(Func<T> constructor) where T : class, INetSerializable
		{
			_netSerializer.RegisterNestedType(constructor);
		}

		/// <summary>
		/// Reads all available data from NetDataReader and calls OnReceive delegates
		/// </summary>
		/// <param name="reader">NetDataReader with packets data</param>
		public void ReadAllPackets(NetDataReader reader)
		{
			while (reader.AvailableBytes > 0)
				ReadPacket(reader);
		}

		/// <summary>
		/// Reads all available data from NetDataReader and calls OnReceive delegates
		/// </summary>
		/// <param name="reader">NetDataReader with packets data</param>
		/// <param name="userData">Argument that passed to OnReceivedEvent</param>
		/// <exception cref="ParseException">Malformed packet</exception>
		public void ReadAllPackets(NetDataReader reader, object userData)
		{
			while (reader.AvailableBytes > 0)
				ReadPacket(reader, userData);
		}

		/// <summary>
		/// Reads one packet from NetDataReader and calls OnReceive delegate
		/// </summary>
		/// <param name="reader">NetDataReader with packet</param>
		/// <exception cref="ParseException">Malformed packet</exception>
		public void ReadPacket(NetDataReader reader)
		{
			ReadPacket(reader, null);
		}

		public void Write<T>(NetDataWriter writer, T packet) where T : class, new()
		{
			WriteHash<T>(writer);
			_netSerializer.Serialize(writer, packet);
		}

		public void WriteNetSerializable<T>(NetDataWriter writer, ref T packet) where T : INetSerializable
		{
			WriteHash<T>(writer);
			packet.Serialize(writer);
		}

		/// <summary>
		/// Reads one packet from NetDataReader and calls OnReceive delegate
		/// </summary>
		/// <param name="reader">NetDataReader with packet</param>
		/// <param name="userData">Argument that passed to OnReceivedEvent</param>
		/// <exception cref="ParseException">Malformed packet</exception>
		public void ReadPacket(NetDataReader reader, object userData)
		{
			GetCallbackFromData(reader)(reader, userData);
		}

		/// <summary>
		/// Register and subscribe to packet receive event
		/// </summary>
		/// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
		/// <param name="packetConstructor">Method that constructs packet instead of slow Activator.CreateInstance</param>
		/// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
		public void Subscribe<T>(Action<T> onReceive, Func<T> packetConstructor) where T : class, new()
		{
			_netSerializer.Register<T>();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				var reference = packetConstructor();
				_netSerializer.Deserialize(reader, reference);
				onReceive(reference);
			};
		}

		/// <summary>
		/// Register and subscribe to packet receive event (with userData)
		/// </summary>
		/// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
		/// <param name="packetConstructor">Method that constructs packet instead of slow Activator.CreateInstance</param>
		/// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
		public void Subscribe<T, TUserData>(Action<T, TUserData> onReceive, Func<T> packetConstructor) where T : class, new()
		{
			_netSerializer.Register<T>();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				var reference = packetConstructor();
				_netSerializer.Deserialize(reader, reference);
				onReceive(reference, (TUserData) userData);
			};
		}

		/// <summary>
		/// Register and subscribe to packet receive event
		/// This method will overwrite last received packet class on receive (less garbage)
		/// </summary>
		/// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
		/// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
		public void SubscribeReusable<T>(Action<T> onReceive) where T : class, new()
		{
			_netSerializer.Register<T>();
			var reference = new T();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				_netSerializer.Deserialize(reader, reference);
				onReceive(reference);
			};
		}

		/// <summary>
		/// Register and subscribe to packet receive event
		/// This method will overwrite last received packet class on receive (less garbage)
		/// </summary>
		/// <param name="onReceive">event that will be called when packet deserialized with ReadPacket method</param>
		/// <exception cref="InvalidTypeException"><typeparamref name="T"/>'s fields are not supported, or it has no fields</exception>
		public void SubscribeReusable<T, TUserData>(Action<T, TUserData> onReceive) where T : class, new()
		{
			_netSerializer.Register<T>();
			var reference = new T();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				_netSerializer.Deserialize(reader, reference);
				onReceive(reference, (TUserData) userData);
			};
		}

		public void SubscribeNetSerializable<T, TUserData>(
			Action<T, TUserData> onReceive,
			Func<T> packetConstructor) where T : INetSerializable
		{
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				var pkt = packetConstructor();
				pkt.Deserialize(reader);
				onReceive(pkt, (TUserData) userData);
			};
		}

		public void SubscribeNetSerializable<T>(
			Action<T> onReceive,
			Func<T> packetConstructor) where T : INetSerializable
		{
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				var pkt = packetConstructor();
				pkt.Deserialize(reader);
				onReceive(pkt);
			};
		}

		public void SubscribeNetSerializable<T, TUserData>(
			Action<T, TUserData> onReceive) where T : INetSerializable, new()
		{
			var reference = new T();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				reference.Deserialize(reader);
				onReceive(reference, (TUserData) userData);
			};
		}

		public void SubscribeNetSerializable<T>(
			Action<T> onReceive) where T : INetSerializable, new()
		{
			var reference = new T();
			_callbacks[GetHash<T>()] = (reader, userData) =>
			{
				reference.Deserialize(reader);
				onReceive(reference);
			};
		}

		/// <summary>
		/// Remove any subscriptions by type
		/// </summary>
		/// <typeparam name="T">Packet type</typeparam>
		/// <returns>true if remove is success</returns>
		public bool RemoveSubscription<T>()
		{
			return _callbacks.Remove(GetHash<T>());
		}
	}
}
