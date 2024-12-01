#if DEBUG
#define STATS_ENABLED
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
	/// <summary>
	/// Peer connection state
	/// </summary>
	[Flags]
	public enum ConnectionState : byte
	{
		Outgoing = 1 << 1,
		Connected = 1 << 2,
		ShutdownRequested = 1 << 3,
		Disconnected = 1 << 4,
		EndPointChange = 1 << 5,
		Any = Outgoing | Connected | ShutdownRequested | EndPointChange
	}

	internal enum ConnectRequestResult
	{
		None,
		P2PLose, //when peer connecting
		Reconnection, //when peer was connected
		NewConnection //when peer was disconnected
	}

	internal enum DisconnectResult
	{
		None,
		Reject,
		Disconnect
	}

	internal enum ShutdownResult
	{
		None,
		Success,
		WasConnected
	}

	/// <summary>
	/// Network peer. Main purpose is sending messages to specific peer.
	/// </summary>
	public class NetPeer
	{
		//Ping and RTT
		private int _rtt;
		private int _avgRtt;
		private int _rttCount;
		private double _resendDelay = 27.0;
		private int _pingSendTimer;
		private int _rttResetTimer;
		private readonly Stopwatch _pingTimer = new Stopwatch();
		private int _timeSinceLastPacket;
		private long _remoteDelta;

		//Common
		private readonly object _shutdownLock = new object();

		internal volatile NetPeer NextPeer;
		internal NetPeer PrevPeer;

		internal byte ConnectionNum
		{
			get => _connectNum;
			private set
			{
				_connectNum = value;
				_mergeData.ConnectionNumber = value;
				_pingPacket.ConnectionNumber = value;
				_pongPacket.ConnectionNumber = value;
			}
		}

		//Channels
		private readonly Queue<NetPacket> _unreliableChannel;
		private readonly ConcurrentQueue<BaseChannel> _channelSendQueue;
		private readonly BaseChannel[] _channels;

		//MTU
		private int _mtu;
		private int _mtuIdx;
		private bool _finishMtu;
		private int _mtuCheckTimer;
		private int _mtuCheckAttempts;
		private const int MtuCheckDelay = 1000;
		private const int MaxMtuCheckAttempts = 4;
		private readonly object _mtuMutex = new object();

		//Fragment
		private class IncomingFragments
		{
			public NetPacket[] Fragments;
			public int ReceivedCount;
			public int TotalSize;
			public byte ChannelId;
		}

		private int _fragmentId;
		private readonly Dictionary<ushort, IncomingFragments> _holdedFragments;
		private readonly Dictionary<ushort, ushort> _deliveredFragments;

		//Merging
		private readonly NetPacket _mergeData;
		private int _mergePos;
		private int _mergeCount;

		//Connection
		private IPEndPoint _remoteEndPoint;
		private int _connectAttempts;
		private int _connectTimer;
		private long _connectTime;
		private byte _connectNum;
		private ConnectionState _connectionState;
		private NetPacket _shutdownPacket;
		private const int ShutdownDelay = 300;
		private int _shutdownTimer;
		private readonly NetPacket _pingPacket;
		private readonly NetPacket _pongPacket;
		private readonly NetPacket _connectRequestPacket;
		private readonly NetPacket _connectAcceptPacket;

		/// <summary>
		/// Peer ip address and port
		/// </summary>
		public IPEndPoint EndPoint =>
			_remoteEndPoint;

		/// <summary>
		/// Peer parent NetManager
		/// </summary>
		public readonly NetManager NetManager;

		/// <summary>
		/// Current connection state
		/// </summary>
		public ConnectionState ConnectionState =>
			_connectionState;

		/// <summary>
		/// Connection time for internal purposes
		/// </summary>
		internal long ConnectTime =>
			_connectTime;

		/// <summary>
		/// Peer id can be used as key in your dictionary of peers
		/// </summary>
		public readonly int Id;

		/// <summary>
		/// Id assigned from server
		/// </summary>
		public int RemoteId { get; private set; }

		/// <summary>
		/// Current one-way ping (RTT/2) in milliseconds
		/// </summary>
		public int Ping =>
			_avgRtt / 2;

		/// <summary>
		/// Round trip time in milliseconds
		/// </summary>
		public int RoundTripTime =>
			_avgRtt;

		/// <summary>
		/// Current MTU - Maximum Transfer Unit ( maximum udp packet size without fragmentation )
		/// </summary>
		public int Mtu =>
			_mtu;

		/// <summary>
		/// Delta with remote time in ticks (not accurate)
		/// positive - remote time > our time
		/// </summary>
		public long RemoteTimeDelta =>
			_remoteDelta;

		/// <summary>
		/// Remote UTC time (not accurate)
		/// </summary>
		public DateTime RemoteUtcTime =>
			new DateTime(DateTime.UtcNow.Ticks + _remoteDelta);

		/// <summary>
		/// Time since last packet received (including internal library packets)
		/// </summary>
		public int TimeSinceLastPacket =>
			_timeSinceLastPacket;

		internal double ResendDelay =>
			_resendDelay;

		/// <summary>
		/// Application defined object containing data about the connection
		/// </summary>
		public object Tag;

		/// <summary>
		/// Statistics of peer connection
		/// </summary>
		public readonly NetStatistics Statistics;

		//incoming connection constructor
		internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id)
		{
			Id = id;
			Statistics = new NetStatistics();
			NetManager = netManager;
			ResetMtu();
			_remoteEndPoint = remoteEndPoint;
			_connectionState = ConnectionState.Connected;
			_mergeData = new NetPacket(PacketProperty.Merged, NetConstants.MaxPacketSize);
			_pongPacket = new NetPacket(PacketProperty.Pong, 0);
			_pingPacket = new NetPacket(PacketProperty.Ping, 0)
			{
				Sequence = 1
			};

			_unreliableChannel = new Queue<NetPacket>();
			_holdedFragments = new Dictionary<ushort, IncomingFragments>();
			_deliveredFragments = new Dictionary<ushort, ushort>();

			_channels = new BaseChannel[netManager.ChannelsCount * NetConstants.ChannelTypeCount];
			_channelSendQueue = new ConcurrentQueue<BaseChannel>();
		}

		internal void InitiateEndPointChange()
		{
			ResetMtu();
			_connectionState = ConnectionState.EndPointChange;
		}

		internal void FinishEndPointChange(IPEndPoint newEndPoint)
		{
			if (_connectionState != ConnectionState.EndPointChange)
				return;
			_connectionState = ConnectionState.Connected;
			_remoteEndPoint = newEndPoint;
		}

		internal void ResetMtu()
		{
			_finishMtu = false;
			if (NetManager.MtuOverride > 0)
				OverrideMtu(NetManager.MtuOverride);
			else if (NetManager.UseSafeMtu)
				SetMtu(0);
			else
				SetMtu(1);
		}

		private void SetMtu(int mtuIdx)
		{
			_mtuIdx = mtuIdx;
			_mtu = NetConstants.PossibleMtu[mtuIdx] - NetManager.ExtraPacketSizeForLayer;
		}

		private void OverrideMtu(int mtuValue)
		{
			_mtu = mtuValue;
			_finishMtu = true;
		}

		/// <summary>
		/// Returns packets count in queue for reliable channel
		/// </summary>
		/// <param name="channelNumber">number of channel 0-63</param>
		/// <param name="ordered">type of channel ReliableOrdered or ReliableUnordered</param>
		/// <returns>packets count in channel queue</returns>
		public int GetPacketsCountInReliableQueue(byte channelNumber, bool ordered)
		{
			int idx = channelNumber * NetConstants.ChannelTypeCount +
			          (byte) (ordered ? DeliveryMethod.ReliableOrdered : DeliveryMethod.ReliableUnordered);
			var channel = _channels[idx];
			return channel != null ? ((ReliableChannel) channel).PacketsInQueue : 0;
		}

		/// <summary>
		/// Create temporary packet (maximum size MTU - headerSize) to send later without additional copies
		/// </summary>
		/// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <returns>PooledPacket that you can use to write data starting from UserDataOffset</returns>
		public PooledPacket CreatePacketFromPool(DeliveryMethod deliveryMethod, byte channelNumber)
		{
			//multithreaded variable
			int mtu = _mtu;
			var packet = NetManager.PoolGetPacket(mtu);
			if (deliveryMethod == DeliveryMethod.Unreliable)
			{
				packet.Property = PacketProperty.Unreliable;
				return new PooledPacket(packet, mtu, 0);
			}
			else
			{
				packet.Property = PacketProperty.Channeled;
				return new PooledPacket(packet, mtu, (byte) (channelNumber * NetConstants.ChannelTypeCount + (byte) deliveryMethod));
			}
		}

		/// <summary>
		/// Sends pooled packet without data copy
		/// </summary>
		/// <param name="packet">packet to send</param>
		/// <param name="userDataSize">size of user data you want to send</param>
		public void SendPooledPacket(PooledPacket packet, int userDataSize)
		{
			if (_connectionState != ConnectionState.Connected)
				return;
			packet._packet.Size = packet.UserDataOffset + userDataSize;
			if (packet._packet.Property == PacketProperty.Channeled)
			{
				CreateChannel(packet._channelNumber).AddToQueue(packet._packet);
			}
			else
			{
				lock (_unreliableChannel)
					_unreliableChannel.Enqueue(packet._packet);
			}
		}

		private BaseChannel CreateChannel(byte idx)
		{
			BaseChannel newChannel = _channels[idx];
			if (newChannel != null)
				return newChannel;
			switch ((DeliveryMethod) (idx % NetConstants.ChannelTypeCount))
			{
				case DeliveryMethod.ReliableUnordered:
					newChannel = new ReliableChannel(this, false, idx);
					break;
				case DeliveryMethod.Sequenced:
					newChannel = new SequencedChannel(this, false, idx);
					break;
				case DeliveryMethod.ReliableOrdered:
					newChannel = new ReliableChannel(this, true, idx);
					break;
				case DeliveryMethod.ReliableSequenced:
					newChannel = new SequencedChannel(this, true, idx);
					break;
			}
			BaseChannel prevChannel = Interlocked.CompareExchange(ref _channels[idx], newChannel, null);
			if (prevChannel != null)
				return prevChannel;

			return newChannel;
		}

		//"Connect to" constructor
		internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id, byte connectNum, NetDataWriter connectData)
			: this(netManager, remoteEndPoint, id)
		{
			_connectTime = DateTime.UtcNow.Ticks;
			_connectionState = ConnectionState.Outgoing;
			ConnectionNum = connectNum;

			//Make initial packet
			_connectRequestPacket = NetConnectRequestPacket.Make(connectData, remoteEndPoint.Serialize(), _connectTime, id);
			_connectRequestPacket.ConnectionNumber = connectNum;

			//Send request
			NetManager.SendRaw(_connectRequestPacket, _remoteEndPoint);

			NetDebug.Write(NetLogLevel.Trace, $"[CC] ConnectId: {_connectTime}, ConnectNum: {connectNum}");
		}

		//"Accept" incoming constructor
		internal NetPeer(NetManager netManager, ConnectionRequest request, int id)
			: this(netManager, request.RemoteEndPoint, id)
		{
			_connectTime = request.InternalPacket.ConnectionTime;
			ConnectionNum = request.InternalPacket.ConnectionNumber;
			RemoteId = request.InternalPacket.PeerId;

			//Make initial packet
			_connectAcceptPacket = NetConnectAcceptPacket.Make(_connectTime, ConnectionNum, id);

			//Make Connected
			_connectionState = ConnectionState.Connected;

			//Send
			NetManager.SendRaw(_connectAcceptPacket, _remoteEndPoint);

			NetDebug.Write(NetLogLevel.Trace, $"[CC] ConnectId: {_connectTime}");
		}

		//Reject
		internal void Reject(NetConnectRequestPacket requestData, byte[] data, int start, int length)
		{
			_connectTime = requestData.ConnectionTime;
			_connectNum = requestData.ConnectionNumber;
			Shutdown(data, start, length, false);
		}

		internal bool ProcessConnectAccept(NetConnectAcceptPacket packet)
		{
			if (_connectionState != ConnectionState.Outgoing)
				return false;

			//check connection id
			if (packet.ConnectionTime != _connectTime)
			{
				NetDebug.Write(NetLogLevel.Trace, $"[NC] Invalid connectId: {packet.ConnectionTime} != our({_connectTime})");
				return false;
			}
			//check connect num
			ConnectionNum = packet.ConnectionNumber;
			RemoteId = packet.PeerId;

			NetDebug.Write(NetLogLevel.Trace, "[NC] Received connection accept");
			Interlocked.Exchange(ref _timeSinceLastPacket, 0);
			_connectionState = ConnectionState.Connected;
			return true;
		}

		/// <summary>
		/// Gets maximum size of packet that will be not fragmented.
		/// </summary>
		/// <param name="options">Type of packet that you want send</param>
		/// <returns>size in bytes</returns>
		public int GetMaxSinglePacketSize(DeliveryMethod options)
		{
			return _mtu - NetPacket.GetHeaderSize(options == DeliveryMethod.Unreliable ? PacketProperty.Unreliable : PacketProperty.Channeled);
		}

		/// <summary>
		/// Send data to peer with delivery event called
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
		/// <param name="userData">User data that will be received in DeliveryEvent</param>
		/// <exception cref="ArgumentException">
		///     If you trying to send unreliable packet type<para/>
		/// </exception>
		public void SendWithDeliveryEvent(byte[] data, byte channelNumber, DeliveryMethod deliveryMethod, object userData)
		{
			if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
				throw new ArgumentException("Delivery event will work only for ReliableOrdered/Unordered packets");
			SendInternal(data, 0, data.Length, channelNumber, deliveryMethod, userData);
		}

		/// <summary>
		/// Send data to peer with delivery event called
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
		/// <param name="userData">User data that will be received in DeliveryEvent</param>
		/// <exception cref="ArgumentException">
		///     If you trying to send unreliable packet type<para/>
		/// </exception>
		public void SendWithDeliveryEvent(byte[] data, int start, int length, byte channelNumber, DeliveryMethod deliveryMethod, object userData)
		{
			if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
				throw new ArgumentException("Delivery event will work only for ReliableOrdered/Unordered packets");
			SendInternal(data, start, length, channelNumber, deliveryMethod, userData);
		}

		/// <summary>
		/// Send data to peer with delivery event called
		/// </summary>
		/// <param name="dataWriter">Data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
		/// <param name="userData">User data that will be received in DeliveryEvent</param>
		/// <exception cref="ArgumentException">
		///     If you trying to send unreliable packet type<para/>
		/// </exception>
		public void SendWithDeliveryEvent(NetDataWriter dataWriter, byte channelNumber, DeliveryMethod deliveryMethod, object userData)
		{
			if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
				throw new ArgumentException("Delivery event will work only for ReliableOrdered/Unordered packets");
			SendInternal(dataWriter.Data, 0, dataWriter.Length, channelNumber, deliveryMethod, userData);
		}

		/// <summary>
		/// Send data to peer (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(byte[] data, DeliveryMethod deliveryMethod)
		{
			SendInternal(data, 0, data.Length, 0, deliveryMethod, null);
		}

		/// <summary>
		/// Send data to peer (channel - 0)
		/// </summary>
		/// <param name="dataWriter">DataWriter with data</param>
		/// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(NetDataWriter dataWriter, DeliveryMethod deliveryMethod)
		{
			SendInternal(dataWriter.Data, 0, dataWriter.Length, 0, deliveryMethod, null);
		}

		/// <summary>
		/// Send data to peer (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(byte[] data, int start, int length, DeliveryMethod options)
		{
			SendInternal(data, start, length, 0, options, null);
		}

		/// <summary>
		/// Send data to peer
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(byte[] data, byte channelNumber, DeliveryMethod deliveryMethod)
		{
			SendInternal(data, 0, data.Length, channelNumber, deliveryMethod, null);
		}

		/// <summary>
		/// Send data to peer
		/// </summary>
		/// <param name="dataWriter">DataWriter with data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(NetDataWriter dataWriter, byte channelNumber, DeliveryMethod deliveryMethod)
		{
			SendInternal(dataWriter.Data, 0, dataWriter.Length, channelNumber, deliveryMethod, null);
		}

		/// <summary>
		/// Send data to peer
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
		/// <exception cref="TooBigPacketException">
		///     If size exceeds maximum limit:<para/>
		///     MTU - headerSize bytes for Unreliable<para/>
		///     Fragment count exceeded ushort.MaxValue<para/>
		/// </exception>
		public void Send(byte[] data, int start, int length, byte channelNumber, DeliveryMethod deliveryMethod)
		{
			SendInternal(data, start, length, channelNumber, deliveryMethod, null);
		}

		private void SendInternal(
			byte[] data,
			int start,
			int length,
			byte channelNumber,
			DeliveryMethod deliveryMethod,
			object userData)
		{
			if (_connectionState != ConnectionState.Connected || channelNumber >= _channels.Length)
				return;

			//Select channel
			PacketProperty property;
			BaseChannel channel = null;

			if (deliveryMethod == DeliveryMethod.Unreliable)
			{
				property = PacketProperty.Unreliable;
			}
			else
			{
				property = PacketProperty.Channeled;
				channel = CreateChannel((byte) (channelNumber * NetConstants.ChannelTypeCount + (byte) deliveryMethod));
			}

			//Prepare
			NetDebug.Write("[RS]Packet: " + property);

			//Check fragmentation
			int headerSize = NetPacket.GetHeaderSize(property);
			//Save mtu for multithread
			int mtu = _mtu;
			if (length + headerSize > mtu)
			{
				//if cannot be fragmented
				if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
					throw new TooBigPacketException("Unreliable or ReliableSequenced packet size exceeded maximum of " + (mtu - headerSize) +
					                                " bytes, Check allowed size by GetMaxSinglePacketSize()");

				int packetFullSize = mtu - headerSize;
				int packetDataSize = packetFullSize - NetConstants.FragmentHeaderSize;
				int totalPackets = length / packetDataSize + (length % packetDataSize == 0 ? 0 : 1);

				NetDebug.Write($@"FragmentSend:
 MTU: {mtu}
 headerSize: {headerSize}
 packetFullSize: {packetFullSize}
 packetDataSize: {packetDataSize}
 totalPackets: {totalPackets}");

				if (totalPackets > ushort.MaxValue)
					throw new TooBigPacketException("Data was split in " + totalPackets + " fragments, which exceeds " + ushort.MaxValue);

				ushort currentFragmentId = (ushort) Interlocked.Increment(ref _fragmentId);

				for (ushort partIdx = 0; partIdx < totalPackets; partIdx++)
				{
					int sendLength = length > packetDataSize ? packetDataSize : length;

					NetPacket p = NetManager.PoolGetPacket(headerSize + sendLength + NetConstants.FragmentHeaderSize);
					p.Property = property;
					p.UserData = userData;
					p.FragmentId = currentFragmentId;
					p.FragmentPart = partIdx;
					p.FragmentsTotal = (ushort) totalPackets;
					p.MarkFragmented();

					Buffer.BlockCopy(data, start + partIdx * packetDataSize, p.RawData, NetConstants.FragmentedHeaderTotalSize, sendLength);
					channel.AddToQueue(p);

					length -= sendLength;
				}
				return;
			}

			//Else just send
			NetPacket packet = NetManager.PoolGetPacket(headerSize + length);
			packet.Property = property;
			Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
			packet.UserData = userData;

			if (channel == null) //unreliable
			{
				lock (_unreliableChannel)
					_unreliableChannel.Enqueue(packet);
			}
			else
			{
				channel.AddToQueue(packet);
			}
		}

#if LITENETLIB_SPANS || NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1 || NETCOREAPP3_1 || NET5_0 || NETSTANDARD2_1
        /// <summary>
        /// Send data to peer with delivery event called
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="deliveryMethod">Delivery method (reliable, unreliable, etc.)</param>
        /// <param name="userData">User data that will be received in DeliveryEvent</param>
        /// <exception cref="ArgumentException">
        ///     If you trying to send unreliable packet type<para/>
        /// </exception>
        public void SendWithDeliveryEvent(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod deliveryMethod, object userData)
        {
            if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
                throw new ArgumentException("Delivery event will work only for ReliableOrdered/Unordered packets");
            SendInternal(data, channelNumber, deliveryMethod, userData);
        }

        /// <summary>
        /// Send data to peer (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(ReadOnlySpan<byte> data, DeliveryMethod deliveryMethod)
        {
            SendInternal(data, 0, deliveryMethod, null);
        }

        /// <summary>
        /// Send data to peer
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="deliveryMethod">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(ReadOnlySpan<byte> data, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            SendInternal(data, channelNumber, deliveryMethod, null);
        }

        private void SendInternal(
            ReadOnlySpan<byte> data,
            byte channelNumber,
            DeliveryMethod deliveryMethod,
            object userData)
        {
            if (_connectionState != ConnectionState.Connected || channelNumber >= _channels.Length)
                return;

            //Select channel
            PacketProperty property;
            BaseChannel channel = null;

            if (deliveryMethod == DeliveryMethod.Unreliable)
            {
                property = PacketProperty.Unreliable;
            }
            else
            {
                property = PacketProperty.Channeled;
                channel = CreateChannel((byte)(channelNumber * NetConstants.ChannelTypeCount + (byte)deliveryMethod));
            }

            //Prepare
            NetDebug.Write("[RS]Packet: " + property);

            //Check fragmentation
            int headerSize = NetPacket.GetHeaderSize(property);
            //Save mtu for multithread
            int mtu = _mtu;
            int length = data.Length;
            if (length + headerSize > mtu)
            {
                //if cannot be fragmented
                if (deliveryMethod != DeliveryMethod.ReliableOrdered && deliveryMethod != DeliveryMethod.ReliableUnordered)
                    throw new TooBigPacketException("Unreliable or ReliableSequenced packet size exceeded maximum of " + (mtu - headerSize) + " bytes, Check allowed size by GetMaxSinglePacketSize()");

                int packetFullSize = mtu - headerSize;
                int packetDataSize = packetFullSize - NetConstants.FragmentHeaderSize;
                int totalPackets = length / packetDataSize + (length % packetDataSize == 0 ? 0 : 1);

                if (totalPackets > ushort.MaxValue)
                    throw new TooBigPacketException("Data was split in " + totalPackets + " fragments, which exceeds " + ushort.MaxValue);

                ushort currentFragmentId = (ushort)Interlocked.Increment(ref _fragmentId);

                for (ushort partIdx = 0; partIdx < totalPackets; partIdx++)
                {
                    int sendLength = length > packetDataSize ? packetDataSize : length;

                    NetPacket p = NetManager.PoolGetPacket(headerSize + sendLength + NetConstants.FragmentHeaderSize);
                    p.Property = property;
                    p.UserData = userData;
                    p.FragmentId = currentFragmentId;
                    p.FragmentPart = partIdx;
                    p.FragmentsTotal = (ushort)totalPackets;
                    p.MarkFragmented();

                    data.Slice(partIdx * packetDataSize, sendLength).CopyTo(new Span<byte>(p.RawData, NetConstants.FragmentedHeaderTotalSize, sendLength));
                    channel.AddToQueue(p);

                    length -= sendLength;
                }
                return;
            }

            //Else just send
            NetPacket packet = NetManager.PoolGetPacket(headerSize + length);
            packet.Property = property;
            data.CopyTo(new Span<byte>(packet.RawData, headerSize, length));
            packet.UserData = userData;

            if (channel == null) //unreliable
            {
                lock(_unreliableChannel)
                    _unreliableChannel.Enqueue(packet);
            }
            else
            {
                channel.AddToQueue(packet);
            }
        }
#endif

		public void Disconnect(byte[] data)
		{
			NetManager.DisconnectPeer(this, data);
		}

		public void Disconnect(NetDataWriter writer)
		{
			NetManager.DisconnectPeer(this, writer);
		}

		public void Disconnect(byte[] data, int start, int count)
		{
			NetManager.DisconnectPeer(this, data, start, count);
		}

		public void Disconnect()
		{
			NetManager.DisconnectPeer(this);
		}

		internal DisconnectResult ProcessDisconnect(NetPacket packet)
		{
			if ((_connectionState == ConnectionState.Connected || _connectionState == ConnectionState.Outgoing) &&
			    packet.Size >= 9 &&
			    BitConverter.ToInt64(packet.RawData, 1) == _connectTime &&
			    packet.ConnectionNumber == _connectNum)
			{
				return _connectionState == ConnectionState.Connected
					? DisconnectResult.Disconnect
					: DisconnectResult.Reject;
			}
			return DisconnectResult.None;
		}

		internal void AddToReliableChannelSendQueue(BaseChannel channel)
		{
			_channelSendQueue.Enqueue(channel);
		}

		internal ShutdownResult Shutdown(byte[] data, int start, int length, bool force)
		{
			lock (_shutdownLock)
			{
				//trying to shutdown already disconnected
				if (_connectionState == ConnectionState.Disconnected ||
				    _connectionState == ConnectionState.ShutdownRequested)
				{
					return ShutdownResult.None;
				}

				var result = _connectionState == ConnectionState.Connected
					? ShutdownResult.WasConnected
					: ShutdownResult.Success;

				//don't send anything
				if (force)
				{
					_connectionState = ConnectionState.Disconnected;
					return result;
				}

				//reset time for reconnect protection
				Interlocked.Exchange(ref _timeSinceLastPacket, 0);

				//send shutdown packet
				_shutdownPacket = new NetPacket(PacketProperty.Disconnect, length)
				{
					ConnectionNumber = _connectNum
				};
				FastBitConverter.GetBytes(_shutdownPacket.RawData, 1, _connectTime);
				if (_shutdownPacket.Size >= _mtu)
				{
					//Drop additional data
					NetDebug.WriteError("[Peer] Disconnect additional data size more than MTU - 8!");
				}
				else if (data != null && length > 0)
				{
					Buffer.BlockCopy(data, start, _shutdownPacket.RawData, 9, length);
				}
				_connectionState = ConnectionState.ShutdownRequested;
				NetDebug.Write("[Peer] Send disconnect");
				NetManager.SendRaw(_shutdownPacket, _remoteEndPoint);
				return result;
			}
		}

		private void UpdateRoundTripTime(int roundTripTime)
		{
			_rtt += roundTripTime;
			_rttCount++;
			_avgRtt = _rtt / _rttCount;
			_resendDelay = 25.0 + _avgRtt * 2.1; // 25 ms + double rtt
		}

		internal void AddReliablePacket(DeliveryMethod method, NetPacket p)
		{
			if (p.IsFragmented)
			{
				NetDebug.Write($"Fragment. Id: {p.FragmentId}, Part: {p.FragmentPart}, Total: {p.FragmentsTotal}");
				//Get needed array from dictionary
				ushort packetFragId = p.FragmentId;
				byte packetChannelId = p.ChannelId;
				if (!_holdedFragments.TryGetValue(packetFragId, out var incomingFragments))
				{
					incomingFragments = new IncomingFragments
					{
						Fragments = new NetPacket[p.FragmentsTotal],
						ChannelId = p.ChannelId
					};
					_holdedFragments.Add(packetFragId, incomingFragments);
				}

				//Cache
				var fragments = incomingFragments.Fragments;

				//Error check
				if (p.FragmentPart >= fragments.Length ||
				    fragments[p.FragmentPart] != null ||
				    p.ChannelId != incomingFragments.ChannelId)
				{
					NetManager.PoolRecycle(p);
					NetDebug.WriteError("Invalid fragment packet");
					return;
				}
				//Fill array
				fragments[p.FragmentPart] = p;

				//Increase received fragments count
				incomingFragments.ReceivedCount++;

				//Increase total size
				incomingFragments.TotalSize += p.Size - NetConstants.FragmentedHeaderTotalSize;

				//Check for finish
				if (incomingFragments.ReceivedCount != fragments.Length)
					return;

				//just simple packet
				NetPacket resultingPacket = NetManager.PoolGetPacket(incomingFragments.TotalSize);

				int pos = 0;
				for (int i = 0; i < incomingFragments.ReceivedCount; i++)
				{
					var fragment = fragments[i];
					int writtenSize = fragment.Size - NetConstants.FragmentedHeaderTotalSize;

					if (pos + writtenSize > resultingPacket.RawData.Length)
					{
						_holdedFragments.Remove(packetFragId);
						NetDebug.WriteError(
							$"Fragment error pos: {pos + writtenSize} >= resultPacketSize: {resultingPacket.RawData.Length} , totalSize: {incomingFragments.TotalSize}");
						return;
					}
					if (fragment.Size > fragment.RawData.Length)
					{
						_holdedFragments.Remove(packetFragId);
						NetDebug.WriteError($"Fragment error size: {fragment.Size} > fragment.RawData.Length: {fragment.RawData.Length}");
						return;
					}

					//Create resulting big packet
					Buffer.BlockCopy(fragment.RawData,
						NetConstants.FragmentedHeaderTotalSize,
						resultingPacket.RawData,
						pos,
						writtenSize);
					pos += writtenSize;

					//Free memory
					NetManager.PoolRecycle(fragment);
					fragments[i] = null;
				}

				//Clear memory
				_holdedFragments.Remove(packetFragId);

				//Send to process
				NetManager.CreateReceiveEvent(resultingPacket, method, (byte) (packetChannelId / NetConstants.ChannelTypeCount), 0, this);
			}
			else //Just simple packet
			{
				NetManager.CreateReceiveEvent(p, method, (byte) (p.ChannelId / NetConstants.ChannelTypeCount), NetConstants.ChanneledHeaderSize, this);
			}
		}

		private void ProcessMtuPacket(NetPacket packet)
		{
			//header + int
			if (packet.Size < NetConstants.PossibleMtu[0])
				return;

			//first stage check (mtu check and mtu ok)
			int receivedMtu = BitConverter.ToInt32(packet.RawData, 1);
			int endMtuCheck = BitConverter.ToInt32(packet.RawData, packet.Size - 4);
			if (receivedMtu != packet.Size || receivedMtu != endMtuCheck || receivedMtu > NetConstants.MaxPacketSize)
			{
				NetDebug.WriteError($"[MTU] Broken packet. RMTU {receivedMtu}, EMTU {endMtuCheck}, PSIZE {packet.Size}");
				return;
			}

			if (packet.Property == PacketProperty.MtuCheck)
			{
				_mtuCheckAttempts = 0;
				NetDebug.Write("[MTU] check. send back: " + receivedMtu);
				packet.Property = PacketProperty.MtuOk;
				NetManager.SendRawAndRecycle(packet, _remoteEndPoint);
			}
			else if (receivedMtu > _mtu && !_finishMtu) //MtuOk
			{
				//invalid packet
				if (receivedMtu != NetConstants.PossibleMtu[_mtuIdx + 1] - NetManager.ExtraPacketSizeForLayer)
					return;

				lock (_mtuMutex)
				{
					SetMtu(_mtuIdx + 1);
				}
				//if maxed - finish.
				if (_mtuIdx == NetConstants.PossibleMtu.Length - 1)
					_finishMtu = true;
				NetManager.PoolRecycle(packet);
				NetDebug.Write("[MTU] ok. Increase to: " + _mtu);
			}
		}

		private void UpdateMtuLogic(int deltaTime)
		{
			if (_finishMtu)
				return;

			_mtuCheckTimer += deltaTime;
			if (_mtuCheckTimer < MtuCheckDelay)
				return;

			_mtuCheckTimer = 0;
			_mtuCheckAttempts++;
			if (_mtuCheckAttempts >= MaxMtuCheckAttempts)
			{
				_finishMtu = true;
				return;
			}

			lock (_mtuMutex)
			{
				if (_mtuIdx >= NetConstants.PossibleMtu.Length - 1)
					return;

				//Send increased packet
				int newMtu = NetConstants.PossibleMtu[_mtuIdx + 1] - NetManager.ExtraPacketSizeForLayer;
				var p = NetManager.PoolGetPacket(newMtu);
				p.Property = PacketProperty.MtuCheck;
				FastBitConverter.GetBytes(p.RawData, 1, newMtu); //place into start
				FastBitConverter.GetBytes(p.RawData, p.Size - 4, newMtu); //and end of packet

				//Must check result for MTU fix
				if (NetManager.SendRawAndRecycle(p, _remoteEndPoint) <= 0)
					_finishMtu = true;
			}
		}

		internal ConnectRequestResult ProcessConnectRequest(NetConnectRequestPacket connRequest)
		{
			//current or new request
			switch (_connectionState)
			{
				//P2P case
				case ConnectionState.Outgoing:
					//fast check
					if (connRequest.ConnectionTime < _connectTime)
					{
						return ConnectRequestResult.P2PLose;
					}
					//slow rare case check
					if (connRequest.ConnectionTime == _connectTime)
					{
						var remoteBytes = _remoteEndPoint.Serialize();
						var localBytes = connRequest.TargetAddress;
						for (int i = remoteBytes.Size - 1; i >= 0; i--)
						{
							byte rb = remoteBytes[i];
							if (rb == localBytes[i])
								continue;
							if (rb < localBytes[i])
								return ConnectRequestResult.P2PLose;
						}
					}
					break;

				case ConnectionState.Connected:
					//Old connect request
					if (connRequest.ConnectionTime == _connectTime)
					{
						//just reply accept
						NetManager.SendRaw(_connectAcceptPacket, _remoteEndPoint);
					}
					//New connect request
					else if (connRequest.ConnectionTime > _connectTime)
					{
						return ConnectRequestResult.Reconnection;
					}
					break;

				case ConnectionState.Disconnected:
				case ConnectionState.ShutdownRequested:
					if (connRequest.ConnectionTime >= _connectTime)
						return ConnectRequestResult.NewConnection;
					break;
			}
			return ConnectRequestResult.None;
		}

		//Process incoming packet
		internal void ProcessPacket(NetPacket packet)
		{
			//not initialized
			if (_connectionState == ConnectionState.Outgoing || _connectionState == ConnectionState.Disconnected)
			{
				NetManager.PoolRecycle(packet);
				return;
			}
			if (packet.Property == PacketProperty.ShutdownOk)
			{
				if (_connectionState == ConnectionState.ShutdownRequested)
					_connectionState = ConnectionState.Disconnected;
				NetManager.PoolRecycle(packet);
				return;
			}
			if (packet.ConnectionNumber != _connectNum)
			{
				NetDebug.Write(NetLogLevel.Trace, "[RR]Old packet");
				NetManager.PoolRecycle(packet);
				return;
			}
			Interlocked.Exchange(ref _timeSinceLastPacket, 0);

			NetDebug.Write($"[RR]PacketProperty: {packet.Property}");
			switch (packet.Property)
			{
				case PacketProperty.Merged:
					int pos = NetConstants.HeaderSize;
					while (pos < packet.Size)
					{
						ushort size = BitConverter.ToUInt16(packet.RawData, pos);
						pos += 2;
						if (packet.RawData.Length - pos < size)
							break;

						NetPacket mergedPacket = NetManager.PoolGetPacket(size);
						Buffer.BlockCopy(packet.RawData, pos, mergedPacket.RawData, 0, size);
						mergedPacket.Size = size;

						if (!mergedPacket.Verify())
							break;

						pos += size;
						ProcessPacket(mergedPacket);
					}
					NetManager.PoolRecycle(packet);
					break;

				//If we get ping, send pong
				case PacketProperty.Ping:
					if (NetUtils.RelativeSequenceNumber(packet.Sequence, _pongPacket.Sequence) > 0)
					{
						NetDebug.Write("[PP]Ping receive, send pong");
						FastBitConverter.GetBytes(_pongPacket.RawData, 3, DateTime.UtcNow.Ticks);
						_pongPacket.Sequence = packet.Sequence;
						NetManager.SendRaw(_pongPacket, _remoteEndPoint);
					}
					NetManager.PoolRecycle(packet);
					break;

				//If we get pong, calculate ping time and rtt
				case PacketProperty.Pong:
					if (packet.Sequence == _pingPacket.Sequence)
					{
						_pingTimer.Stop();
						int elapsedMs = (int) _pingTimer.ElapsedMilliseconds;
						_remoteDelta = BitConverter.ToInt64(packet.RawData, 3) + (elapsedMs * TimeSpan.TicksPerMillisecond) / 2 - DateTime.UtcNow.Ticks;
						UpdateRoundTripTime(elapsedMs);
						NetManager.ConnectionLatencyUpdated(this, elapsedMs / 2);
						NetDebug.Write($"[PP]Ping: {packet.Sequence} - {elapsedMs} - {_remoteDelta}");
					}
					NetManager.PoolRecycle(packet);
					break;

				case PacketProperty.Ack:
				case PacketProperty.Channeled:
					if (packet.ChannelId > _channels.Length)
					{
						NetManager.PoolRecycle(packet);
						break;
					}
					var channel = _channels[packet.ChannelId] ?? (packet.Property == PacketProperty.Ack ? null : CreateChannel(packet.ChannelId));
					if (channel != null)
					{
						if (!channel.ProcessPacket(packet))
							NetManager.PoolRecycle(packet);
					}
					break;

				//Simple packet without acks
				case PacketProperty.Unreliable:
					NetManager.CreateReceiveEvent(packet, DeliveryMethod.Unreliable, 0, NetConstants.HeaderSize, this);
					return;

				case PacketProperty.MtuCheck:
				case PacketProperty.MtuOk:
					ProcessMtuPacket(packet);
					break;

				default:
					NetDebug.WriteError("Error! Unexpected packet type: " + packet.Property);
					break;
			}
		}

		private void SendMerged()
		{
			if (_mergeCount == 0)
				return;
			int bytesSent;
			if (_mergeCount > 1)
			{
				NetDebug.Write("[P]Send merged: " + _mergePos + ", count: " + _mergeCount);
				bytesSent = NetManager.SendRaw(_mergeData.RawData, 0, NetConstants.HeaderSize + _mergePos, _remoteEndPoint);
			}
			else
			{
				//Send without length information and merging
				bytesSent = NetManager.SendRaw(_mergeData.RawData, NetConstants.HeaderSize + 2, _mergePos - 2, _remoteEndPoint);
			}

			if (NetManager.EnableStatistics)
			{
				Statistics.IncrementPacketsSent();
				Statistics.AddBytesSent(bytesSent);
			}

			_mergePos = 0;
			_mergeCount = 0;
		}

		internal void SendUserData(NetPacket packet)
		{
			packet.ConnectionNumber = _connectNum;
			int mergedPacketSize = NetConstants.HeaderSize + packet.Size + 2;
			const int sizeTreshold = 20;
			if (mergedPacketSize + sizeTreshold >= _mtu)
			{
				NetDebug.Write(NetLogLevel.Trace, "[P]SendingPacket: " + packet.Property);
				int bytesSent = NetManager.SendRaw(packet, _remoteEndPoint);

				if (NetManager.EnableStatistics)
				{
					Statistics.IncrementPacketsSent();
					Statistics.AddBytesSent(bytesSent);
				}

				return;
			}
			if (_mergePos + mergedPacketSize > _mtu)
				SendMerged();

			FastBitConverter.GetBytes(_mergeData.RawData, _mergePos + NetConstants.HeaderSize, (ushort) packet.Size);
			Buffer.BlockCopy(packet.RawData, 0, _mergeData.RawData, _mergePos + NetConstants.HeaderSize + 2, packet.Size);
			_mergePos += packet.Size + 2;
			_mergeCount++;
			//DebugWriteForce("Merged: " + _mergePos + "/" + (_mtu - 2) + ", count: " + _mergeCount);
		}

		internal void Update(int deltaTime)
		{
			Interlocked.Add(ref _timeSinceLastPacket, deltaTime);
			switch (_connectionState)
			{
				case ConnectionState.Connected:
					if (_timeSinceLastPacket > NetManager.DisconnectTimeout)
					{
						NetDebug.Write($"[UPDATE] Disconnect by timeout: {_timeSinceLastPacket} > {NetManager.DisconnectTimeout}");
						NetManager.DisconnectPeerForce(this, DisconnectReason.Timeout, 0, null);
						return;
					}
					break;

				case ConnectionState.ShutdownRequested:
					if (_timeSinceLastPacket > NetManager.DisconnectTimeout)
					{
						_connectionState = ConnectionState.Disconnected;
					}
					else
					{
						_shutdownTimer += deltaTime;
						if (_shutdownTimer >= ShutdownDelay)
						{
							_shutdownTimer = 0;
							NetManager.SendRaw(_shutdownPacket, _remoteEndPoint);
						}
					}
					return;

				case ConnectionState.Outgoing:
					_connectTimer += deltaTime;
					if (_connectTimer > NetManager.ReconnectDelay)
					{
						_connectTimer = 0;
						_connectAttempts++;
						if (_connectAttempts > NetManager.MaxConnectAttempts)
						{
							NetManager.DisconnectPeerForce(this, DisconnectReason.ConnectionFailed, 0, null);
							return;
						}

						//else send connect again
						NetManager.SendRaw(_connectRequestPacket, _remoteEndPoint);
					}
					return;

				case ConnectionState.Disconnected: return;
			}

			//Send ping
			_pingSendTimer += deltaTime;
			if (_pingSendTimer >= NetManager.PingInterval)
			{
				NetDebug.Write("[PP] Send ping...");
				//reset timer
				_pingSendTimer = 0;
				//send ping
				_pingPacket.Sequence++;
				//ping timeout
				if (_pingTimer.IsRunning)
					UpdateRoundTripTime((int) _pingTimer.ElapsedMilliseconds);
				_pingTimer.Restart();
				NetManager.SendRaw(_pingPacket, _remoteEndPoint);
			}

			//RTT - round trip time
			_rttResetTimer += deltaTime;
			if (_rttResetTimer >= NetManager.PingInterval * 3)
			{
				_rttResetTimer = 0;
				_rtt = _avgRtt;
				_rttCount = 1;
			}

			UpdateMtuLogic(deltaTime);

			//Pending send
			int count = _channelSendQueue.Count;
			while (count-- > 0)
			{
				if (!_channelSendQueue.TryDequeue(out var channel))
					break;
				if (channel.SendAndCheckQueue())
				{
					// still has something to send, re-add it to the send queue
					_channelSendQueue.Enqueue(channel);
				}
			}

			lock (_unreliableChannel)
			{
				int unreliableCount = _unreliableChannel.Count;
				for (int i = 0; i < unreliableCount; i++)
				{
					var packet = _unreliableChannel.Dequeue();
					SendUserData(packet);
					NetManager.PoolRecycle(packet);
				}
			}

			SendMerged();
		}

		//For reliable channel
		internal void RecycleAndDeliver(NetPacket packet)
		{
			if (packet.UserData != null)
			{
				if (packet.IsFragmented)
				{
					_deliveredFragments.TryGetValue(packet.FragmentId, out ushort fragCount);
					fragCount++;
					if (fragCount == packet.FragmentsTotal)
					{
						NetManager.MessageDelivered(this, packet.UserData);
						_deliveredFragments.Remove(packet.FragmentId);
					}
					else
					{
						_deliveredFragments[packet.FragmentId] = fragCount;
					}
				}
				else
				{
					NetManager.MessageDelivered(this, packet.UserData);
				}
				packet.UserData = null;
			}
			NetManager.PoolRecycle(packet);
		}
	}
}
