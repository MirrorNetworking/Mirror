using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib.Layers;
using LiteNetLib.Utils;
//using Steam;

namespace LiteNetLib
{
	public sealed class NetPacketReader : NetDataReader
	{
		private NetPacket _packet;
		private readonly NetManager _manager;
		private readonly NetEvent _evt;

		internal NetPacketReader(NetManager manager, NetEvent evt)
		{
			_manager = manager;
			_evt = evt;
		}

		internal void SetSource(NetPacket packet, int headerSize)
		{
			if (packet == null)
				return;
			_packet = packet;
			SetSource(packet.RawData, headerSize, packet.Size);
		}

		internal void RecycleInternal()
		{
			Clear();
			if (_packet != null)
				_manager.PoolRecycle(_packet);
			_packet = null;
			_manager.RecycleEvent(_evt);
		}

		public void Recycle()
		{
			if (_manager.AutoRecycle)
				return;
			RecycleInternal();
		}
	}

	internal sealed class NetEvent
	{
		public NetEvent Next;

		public enum EType
		{
			Connect,
			Disconnect,
			Receive,
			ReceiveUnconnected,
			Error,
			ConnectionLatencyUpdated,
			Broadcast,
			ConnectionRequest,
			MessageDelivered,
			PeerAddressChanged
		}

		public EType Type;

		public NetPeer Peer;
		public IPEndPoint RemoteEndPoint;
		public object UserData;
		public int Latency;
		public SocketError ErrorCode;
		public DisconnectReason DisconnectReason;
		public ConnectionRequest ConnectionRequest;
		public DeliveryMethod DeliveryMethod;
		public byte ChannelNumber;
		public readonly NetPacketReader DataReader;

		public NetEvent(NetManager manager)
		{
			DataReader = new NetPacketReader(manager, this);
		}
	}

	/// <summary>
	/// Main class for all network operations. Can be used as client and/or server.
	/// </summary>
	public partial class NetManager : IEnumerable<NetPeer>
	{
		private class IPEndPointComparer : IEqualityComparer<IPEndPoint>
		{
			public bool Equals(IPEndPoint x, IPEndPoint y)
			{
				return x.Address.Equals(y.Address) && x.Port == y.Port;
			}

			public int GetHashCode(IPEndPoint obj)
			{
				return obj.GetHashCode();
			}
		}

		public struct NetPeerEnumerator : IEnumerator<NetPeer>
		{
			private readonly NetPeer _initialPeer;
			private NetPeer _p;

			public NetPeerEnumerator(NetPeer p)
			{
				_initialPeer = p;
				_p = null;
			}

			public void Dispose() { }

			public bool MoveNext()
			{
				_p = _p == null ? _initialPeer : _p.NextPeer;
				return _p != null;
			}

			public void Reset()
			{
				throw new NotSupportedException();
			}

			public NetPeer Current =>
				_p;

			object IEnumerator.Current =>
				_p;
		}

#if DEBUG
		private struct IncomingData
		{
			public NetPacket Data;
			public IPEndPoint EndPoint;
			public DateTime TimeWhenGet;
		}

		private readonly List<IncomingData> _pingSimulationList = new List<IncomingData>();
		private readonly Random _randomGenerator = new Random();
		private const int MinLatencyThreshold = 5;
#endif

		private Thread _logicThread;
		private bool _manualMode;
		private readonly AutoResetEvent _updateTriggerEvent = new AutoResetEvent(true);

		private NetEvent _pendingEventHead;
		private NetEvent _pendingEventTail;

		private NetEvent _netEventPoolHead;
		private readonly INetEventListener _netEventListener;
		private readonly IDeliveryEventListener _deliveryEventListener;
		private readonly INtpEventListener _ntpEventListener;
		private readonly IPeerAddressChangedListener _peerAddressChangedListener;

		private readonly Dictionary<IPEndPoint, NetPeer> _peersDict = new Dictionary<IPEndPoint, NetPeer>(new IPEndPointComparer());
		private readonly Dictionary<IPEndPoint, ConnectionRequest> _requestsDict = new Dictionary<IPEndPoint, ConnectionRequest>(new IPEndPointComparer());
		private readonly Dictionary<IPEndPoint, NtpRequest> _ntpRequests = new Dictionary<IPEndPoint, NtpRequest>(new IPEndPointComparer());
		private readonly ReaderWriterLockSlim _peersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
		private volatile NetPeer _headPeer;
		private int _connectedPeersCount;
		private readonly List<NetPeer> _connectedPeerListCache = new List<NetPeer>();
		private NetPeer[] _peersArray = new NetPeer[32];
		private readonly PacketLayerBase _extraPacketLayer;
		private int _lastPeerId;
		private ConcurrentQueue<int> _peerIds = new ConcurrentQueue<int>();
		private byte _channelsCount = 1;
		private readonly object _eventLock = new object();

		//config section
		/// <summary>
		/// Enable messages receiving without connection. (with SendUnconnectedMessage method)
		/// </summary>
		public bool UnconnectedMessagesEnabled = false;

		/// <summary>
		/// Enable nat punch messages
		/// </summary>
		public bool NatPunchEnabled = false;

		/// <summary>
		/// Library logic update and send period in milliseconds
		/// Lowest values in Windows doesn't change much because of Thread.Sleep precision
		/// To more frequent sends (or sends tied to your game logic) use <see cref="TriggerUpdate"/>
		/// </summary>
		public int UpdateTime = 15;

		/// <summary>
		/// Interval for latency detection and checking connection (in milliseconds)
		/// </summary>
		public int PingInterval = 1000;

		/// <summary>
		/// If NetManager doesn't receive any packet from remote peer during this time (in milliseconds) then connection will be closed
		/// (including library internal keepalive packets)
		/// </summary>
		public int DisconnectTimeout = 5000;

		/// <summary>
		/// Simulate packet loss by dropping random amount of packets. (Works only in DEBUG mode)
		/// </summary>
		public bool SimulatePacketLoss = false;

		/// <summary>
		/// Simulate latency by holding packets for random time. (Works only in DEBUG mode)
		/// </summary>
		public bool SimulateLatency = false;

		/// <summary>
		/// Chance of packet loss when simulation enabled. value in percents (1 - 100).
		/// </summary>
		public int SimulationPacketLossChance = 10;

		/// <summary>
		/// Minimum simulated latency (in milliseconds)
		/// </summary>
		public int SimulationMinLatency = 30;

		/// <summary>
		/// Maximum simulated latency (in milliseconds)
		/// </summary>
		public int SimulationMaxLatency = 100;

		/// <summary>
		/// Events automatically will be called without PollEvents method from another thread
		/// </summary>
		public bool UnsyncedEvents = false;

		/// <summary>
		/// If true - receive event will be called from "receive" thread immediately otherwise on PollEvents call
		/// </summary>
		public bool UnsyncedReceiveEvent = false;

		/// <summary>
		/// If true - delivery event will be called from "receive" thread immediately otherwise on PollEvents call
		/// </summary>
		public bool UnsyncedDeliveryEvent = false;

		/// <summary>
		/// Allows receive broadcast packets
		/// </summary>
		public bool BroadcastReceiveEnabled = false;

		/// <summary>
		/// Delay between initial connection attempts (in milliseconds)
		/// </summary>
		public int ReconnectDelay = 500;

		/// <summary>
		/// Maximum connection attempts before client stops and call disconnect event.
		/// </summary>
		public int MaxConnectAttempts = 10;

		/// <summary>
		/// Enables socket option "ReuseAddress" for specific purposes
		/// </summary>
		public bool ReuseAddress = false;

		/// <summary>
		/// Statistics of all connections
		/// </summary>
		public readonly NetStatistics Statistics = new NetStatistics();

		/// <summary>
		/// Toggles the collection of network statistics for the instance and all known peers
		/// </summary>
		public bool EnableStatistics = false;

		/// <summary>
		/// NatPunchModule for NAT hole punching operations
		/// </summary>
		public readonly NatPunchModule NatPunchModule;

		/// <summary>
		/// Returns true if socket listening and update thread is running
		/// </summary>
		public bool IsRunning { get; private set; }

		/// <summary>
		/// Local EndPoint (host and port)
		/// </summary>
		public int LocalPort { get; private set; }

		/// <summary>
		/// Automatically recycle NetPacketReader after OnReceive event
		/// </summary>
		public bool AutoRecycle;

		/// <summary>
		/// IPv6 support
		/// </summary>
		public bool IPv6Enabled = true;

		/// <summary>
		/// Override MTU for all new peers registered in this NetManager, will ignores MTU Discovery!
		/// </summary>
		public int MtuOverride = 0;

		/// <summary>
		/// Sets initial MTU to lowest possible value according to RFC1191 (576 bytes)
		/// </summary>
		public bool UseSafeMtu = false;

		/// <summary>
		/// First peer. Useful for Client mode
		/// </summary>
		public NetPeer FirstPeer =>
			_headPeer;

		/// <summary>
		/// Experimental feature mostly for servers. Only for Windows/Linux
		/// use direct socket calls for send/receive to drastically increase speed and reduce GC pressure
		/// </summary>
		public bool UseNativeSockets = false;

		/// <summary>
		/// Disconnect peers if HostUnreachable or NetworkUnreachable spawned (old behaviour 0.9.x was true)
		/// </summary>
		public bool DisconnectOnUnreachable = false;

		/// <summary>
		/// Allows peer change it's ip (lte to wifi, wifi to lte, etc). Use only on server
		/// </summary>
		public bool AllowPeerAddressChange = false;

		/// <summary>
		/// QoS channel count per message type (value must be between 1 and 64 channels)
		/// </summary>
		public byte ChannelsCount
		{
			get => _channelsCount;
			set
			{
				if (value < 1 || value > 64)
					throw new ArgumentException("Channels count must be between 1 and 64");
				_channelsCount = value;
			}
		}

		/// <summary>
		/// Returns connected peers list (with internal cached list)
		/// </summary>
		public List<NetPeer> ConnectedPeerList
		{
			get
			{
				GetPeersNonAlloc(_connectedPeerListCache, ConnectionState.Connected);
				return _connectedPeerListCache;
			}
		}

		/// <summary>
		/// Gets peer by peer id
		/// </summary>
		/// <param name="id">id of peer</param>
		/// <returns>Peer if peer with id exist, otherwise null</returns>
		public NetPeer GetPeerById(int id)
		{
			if (id >= 0 && id < _peersArray.Length)
			{
				return _peersArray[id];
			}

			return null;
		}

		/// <summary>
		/// Gets peer by peer id
		/// </summary>
		/// <param name="id">id of peer</param>
		/// <param name="peer">resulting peer</param>
		/// <returns>True if peer with id exist, otherwise false</returns>
		public bool TryGetPeerById(int id, out NetPeer peer)
		{
			peer = GetPeerById(id);

			return peer != null;
		}

		/// <summary>
		/// Returns connected peers count
		/// </summary>
		public int ConnectedPeersCount =>
			Interlocked.CompareExchange(ref _connectedPeersCount, 0, 0);

		public int ExtraPacketSizeForLayer =>
			_extraPacketLayer?.ExtraPacketSizeForLayer ?? 0;

		private bool TryGetPeer(IPEndPoint endPoint, out NetPeer peer)
		{
			_peersLock.EnterReadLock();
			bool result = _peersDict.TryGetValue(endPoint, out peer);
			_peersLock.ExitReadLock();
			return result;
		}

		private void AddPeer(NetPeer peer)
		{
			_peersLock.EnterWriteLock();
			if (_headPeer != null)
			{
				peer.NextPeer = _headPeer;
				_headPeer.PrevPeer = peer;
			}
			_headPeer = peer;
			_peersDict.Add(peer.EndPoint, peer);
			if (peer.Id >= _peersArray.Length)
			{
				int newSize = _peersArray.Length * 2;
				while (peer.Id >= newSize)
					newSize *= 2;
				Array.Resize(ref _peersArray, newSize);
			}
			_peersArray[peer.Id] = peer;
			RegisterEndPoint(peer.EndPoint);
			_peersLock.ExitWriteLock();
		}

		private void RemovePeer(NetPeer peer)
		{
			_peersLock.EnterWriteLock();
			RemovePeerInternal(peer);
			_peersLock.ExitWriteLock();
		}

		private void RemovePeerInternal(NetPeer peer)
		{
			if (!_peersDict.Remove(peer.EndPoint))
				return;
			if (peer == _headPeer)
				_headPeer = peer.NextPeer;

			if (peer.PrevPeer != null)
				peer.PrevPeer.NextPeer = peer.NextPeer;
			if (peer.NextPeer != null)
				peer.NextPeer.PrevPeer = peer.PrevPeer;
			peer.PrevPeer = null;

			_peersArray[peer.Id] = null;
			_peerIds.Enqueue(peer.Id);
			UnregisterEndPoint(peer.EndPoint);
		}

		/// <summary>
		/// NetManager constructor
		/// </summary>
		/// <param name="listener">Network events listener (also can implement IDeliveryEventListener)</param>
		/// <param name="extraPacketLayer">Extra processing of packages, like CRC checksum or encryption. All connected NetManagers must have same layer.</param>
		public NetManager(INetEventListener listener, PacketLayerBase extraPacketLayer = null)
		{
			_netEventListener = listener;
			_deliveryEventListener = listener as IDeliveryEventListener;
			_ntpEventListener = listener as INtpEventListener;
			_peerAddressChangedListener = listener as IPeerAddressChangedListener;
			NatPunchModule = new NatPunchModule(this);
			_extraPacketLayer = extraPacketLayer;
		}

		internal void ConnectionLatencyUpdated(NetPeer fromPeer, int latency)
		{
			CreateEvent(NetEvent.EType.ConnectionLatencyUpdated, fromPeer, latency: latency);
		}

		internal void MessageDelivered(NetPeer fromPeer, object userData)
		{
			if (_deliveryEventListener != null)
				CreateEvent(NetEvent.EType.MessageDelivered, fromPeer, userData: userData);
		}

		internal void DisconnectPeerForce(NetPeer peer,
			DisconnectReason reason,
			SocketError socketErrorCode,
			NetPacket eventData)
		{
			DisconnectPeer(peer, reason, socketErrorCode, true, null, 0, 0, eventData);
		}

		private void DisconnectPeer(
			NetPeer peer,
			DisconnectReason reason,
			SocketError socketErrorCode,
			bool force,
			byte[] data,
			int start,
			int count,
			NetPacket eventData)
		{
			var shutdownResult = peer.Shutdown(data, start, count, force);
			if (shutdownResult == ShutdownResult.None)
				return;
			if (shutdownResult == ShutdownResult.WasConnected)
				Interlocked.Decrement(ref _connectedPeersCount);
			CreateEvent(NetEvent.EType.Disconnect,
				peer,
				errorCode: socketErrorCode,
				disconnectReason: reason,
				readerSource: eventData);
		}

		private void CreateEvent(
			NetEvent.EType type,
			NetPeer peer = null,
			IPEndPoint remoteEndPoint = null,
			SocketError errorCode = 0,
			int latency = 0,
			DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed,
			ConnectionRequest connectionRequest = null,
			DeliveryMethod deliveryMethod = DeliveryMethod.Unreliable,
			byte channelNumber = 0,
			NetPacket readerSource = null,
			object userData = null)
		{
			NetEvent evt;
			bool unsyncEvent = UnsyncedEvents;

			if (type == NetEvent.EType.Connect)
				Interlocked.Increment(ref _connectedPeersCount);
			else if (type == NetEvent.EType.MessageDelivered)
				unsyncEvent = UnsyncedDeliveryEvent;

			lock (_eventLock)
			{
				evt = _netEventPoolHead;
				if (evt == null)
					evt = new NetEvent(this);
				else
					_netEventPoolHead = evt.Next;
			}

			evt.Next = null;
			evt.Type = type;
			evt.DataReader.SetSource(readerSource, readerSource?.GetHeaderSize() ?? 0);
			evt.Peer = peer;
			evt.RemoteEndPoint = remoteEndPoint;
			evt.Latency = latency;
			evt.ErrorCode = errorCode;
			evt.DisconnectReason = disconnectReason;
			evt.ConnectionRequest = connectionRequest;
			evt.DeliveryMethod = deliveryMethod;
			evt.ChannelNumber = channelNumber;
			evt.UserData = userData;

			if (unsyncEvent || _manualMode)
			{
				ProcessEvent(evt);
			}
			else
			{
				lock (_eventLock)
				{
					if (_pendingEventTail == null)
						_pendingEventHead = evt;
					else
						_pendingEventTail.Next = evt;
					_pendingEventTail = evt;
				}
			}
		}

		private void ProcessEvent(NetEvent evt)
		{
			NetDebug.Write("[NM] Processing event: " + evt.Type);
			bool emptyData = evt.DataReader.IsNull;
			switch (evt.Type)
			{
				case NetEvent.EType.Connect:
					_netEventListener.OnPeerConnected(evt.Peer);
					break;
				case NetEvent.EType.Disconnect:
					var info = new DisconnectInfo
					{
						Reason = evt.DisconnectReason,
						AdditionalData = evt.DataReader,
						SocketErrorCode = evt.ErrorCode
					};
					_netEventListener.OnPeerDisconnected(evt.Peer, info);
					break;
				case NetEvent.EType.Receive:
					_netEventListener.OnNetworkReceive(evt.Peer, evt.DataReader, evt.ChannelNumber, evt.DeliveryMethod);
					break;
				case NetEvent.EType.ReceiveUnconnected:
					_netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.BasicMessage);
					break;
				case NetEvent.EType.Broadcast:
					_netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.Broadcast);
					break;
				case NetEvent.EType.Error:
					_netEventListener.OnNetworkError(evt.RemoteEndPoint, evt.ErrorCode);
					break;
				case NetEvent.EType.ConnectionLatencyUpdated:
					_netEventListener.OnNetworkLatencyUpdate(evt.Peer, evt.Latency);
					break;
				case NetEvent.EType.ConnectionRequest:
					_netEventListener.OnConnectionRequest(evt.ConnectionRequest);
					break;
				case NetEvent.EType.MessageDelivered:
					_deliveryEventListener.OnMessageDelivered(evt.Peer, evt.UserData);
					break;
				case NetEvent.EType.PeerAddressChanged:
					_peersLock.EnterUpgradeableReadLock();
					IPEndPoint previousAddress = null;
					if (_peersDict.ContainsKey(evt.Peer.EndPoint))
					{
						_peersLock.EnterWriteLock();
						_peersDict.Remove(evt.Peer.EndPoint);
						previousAddress = evt.Peer.EndPoint;
						evt.Peer.FinishEndPointChange(evt.RemoteEndPoint);
						_peersDict.Add(evt.Peer.EndPoint, evt.Peer);
						_peersLock.ExitWriteLock();
					}
					_peersLock.ExitUpgradeableReadLock();
					if (previousAddress != null)
						_peerAddressChangedListener.OnPeerAddressChanged(evt.Peer, previousAddress);
					break;
			}
			//Recycle if not message
			if (emptyData)
				RecycleEvent(evt);
			else if (AutoRecycle)
				evt.DataReader.RecycleInternal();
		}

		internal void RecycleEvent(NetEvent evt)
		{
			evt.Peer = null;
			evt.ErrorCode = 0;
			evt.RemoteEndPoint = null;
			evt.ConnectionRequest = null;
			lock (_eventLock)
			{
				evt.Next = _netEventPoolHead;
				_netEventPoolHead = evt;
			}
		}

		//Update function
		private void UpdateLogic()
		{
			var peersToRemove = new List<NetPeer>();
			var stopwatch = new Stopwatch();
			stopwatch.Start();

			while (IsRunning)
			{
				try
				{
					ProcessDelayedPackets();
					int elapsed = (int) stopwatch.ElapsedMilliseconds;
					elapsed = elapsed <= 0 ? 1 : elapsed;
					stopwatch.Restart();

					for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
					{
						if (netPeer.ConnectionState == ConnectionState.Disconnected &&
						    netPeer.TimeSinceLastPacket > DisconnectTimeout)
						{
							peersToRemove.Add(netPeer);
						}
						else
						{
							netPeer.Update(elapsed);
						}
					}

					if (peersToRemove.Count > 0)
					{
						_peersLock.EnterWriteLock();
						for (int i = 0; i < peersToRemove.Count; i++)
							RemovePeerInternal(peersToRemove[i]);
						_peersLock.ExitWriteLock();
						peersToRemove.Clear();
					}

					ProcessNtpRequests(elapsed);

					int sleepTime = UpdateTime - (int) stopwatch.ElapsedMilliseconds;
					if (sleepTime > 0)
						_updateTriggerEvent.WaitOne(sleepTime);
				}
				catch (ThreadAbortException)
				{
					return;
				}
				catch (Exception e)
				{
					NetDebug.WriteError("[NM] LogicThread error: " + e);
				}
			}
			stopwatch.Stop();
		}

		[Conditional("DEBUG")]
		private void ProcessDelayedPackets()
		{
#if DEBUG
			if (!SimulateLatency)
				return;

			var time = DateTime.UtcNow;
			lock (_pingSimulationList)
			{
				for (int i = 0; i < _pingSimulationList.Count; i++)
				{
					var incomingData = _pingSimulationList[i];
					if (incomingData.TimeWhenGet <= time)
					{
						DebugMessageReceived(incomingData.Data, incomingData.EndPoint);
						_pingSimulationList.RemoveAt(i);
						i--;
					}
				}
			}
#endif
		}

		private void ProcessNtpRequests(int elapsedMilliseconds)
		{
			List<IPEndPoint> requestsToRemove = null;
			foreach (var ntpRequest in _ntpRequests)
			{
				ntpRequest.Value.Send(_udpSocketv4, elapsedMilliseconds);
				if (ntpRequest.Value.NeedToKill)
				{
					if (requestsToRemove == null)
						requestsToRemove = new List<IPEndPoint>();
					requestsToRemove.Add(ntpRequest.Key);
				}
			}

			if (requestsToRemove != null)
			{
				foreach (var ipEndPoint in requestsToRemove)
				{
					_ntpRequests.Remove(ipEndPoint);
				}
			}
		}

		/// <summary>
		/// Update and send logic. Use this only when NetManager started in manual mode
		/// </summary>
		/// <param name="elapsedMilliseconds">elapsed milliseconds since last update call</param>
		public void ManualUpdate(int elapsedMilliseconds)
		{
			if (!_manualMode)
				return;

			for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
			{
				if (netPeer.ConnectionState == ConnectionState.Disconnected && netPeer.TimeSinceLastPacket > DisconnectTimeout)
				{
					RemovePeerInternal(netPeer);
				}
				else
				{
					netPeer.Update(elapsedMilliseconds);
				}
			}
			ProcessNtpRequests(elapsedMilliseconds);
		}

		internal NetPeer OnConnectionSolved(ConnectionRequest request, byte[] rejectData, int start, int length)
		{
			NetPeer netPeer = null;

			if (request.Result == ConnectionRequestResult.RejectForce)
			{
				NetDebug.Write(NetLogLevel.Trace, "[NM] Peer connect reject force.");
				if (rejectData != null && length > 0)
				{
					var shutdownPacket = PoolGetWithProperty(PacketProperty.Disconnect, length);
					shutdownPacket.ConnectionNumber = request.InternalPacket.ConnectionNumber;
					FastBitConverter.GetBytes(shutdownPacket.RawData, 1, request.InternalPacket.ConnectionTime);
					if (shutdownPacket.Size >= NetConstants.PossibleMtu[0])
						NetDebug.WriteError("[Peer] Disconnect additional data size more than MTU!");
					else
						Buffer.BlockCopy(rejectData, start, shutdownPacket.RawData, 9, length);
					SendRawAndRecycle(shutdownPacket, request.RemoteEndPoint);
				}
			}
			else
			{
				_peersLock.EnterUpgradeableReadLock();
				if (_peersDict.TryGetValue(request.RemoteEndPoint, out netPeer))
				{
					//already have peer
					_peersLock.ExitUpgradeableReadLock();
				}
				else if (request.Result == ConnectionRequestResult.Reject)
				{
					netPeer = new NetPeer(this, request.RemoteEndPoint, GetNextPeerId());
					netPeer.Reject(request.InternalPacket, rejectData, start, length);
					AddPeer(netPeer);
					_peersLock.ExitUpgradeableReadLock();
					NetDebug.Write(NetLogLevel.Trace, "[NM] Peer connect reject.");
				}
				else //Accept
				{
					netPeer = new NetPeer(this, request, GetNextPeerId());
					AddPeer(netPeer);
					_peersLock.ExitUpgradeableReadLock();
					CreateEvent(NetEvent.EType.Connect, netPeer);
					NetDebug.Write(NetLogLevel.Trace, $"[NM] Received peer connection Id: {netPeer.ConnectTime}, EP: {netPeer.EndPoint}");
				}
			}

			lock (_requestsDict)
				_requestsDict.Remove(request.RemoteEndPoint);

			return netPeer;
		}

		private int GetNextPeerId()
		{
			return _peerIds.TryDequeue(out int id) ? id : _lastPeerId++;
		}

		private void ProcessConnectRequest(
			IPEndPoint remoteEndPoint,
			NetPeer netPeer,
			NetConnectRequestPacket connRequest)
		{
			//if we have peer
			if (netPeer != null)
			{
				var processResult = netPeer.ProcessConnectRequest(connRequest);
				NetDebug.Write($"ConnectRequest LastId: {netPeer.ConnectTime}, NewId: {connRequest.ConnectionTime}, EP: {remoteEndPoint}, Result: {processResult}");

				switch (processResult)
				{
					case ConnectRequestResult.Reconnection:
						DisconnectPeerForce(netPeer, DisconnectReason.Reconnect, 0, null);
						RemovePeer(netPeer);
						//go to new connection
						break;
					case ConnectRequestResult.NewConnection:
						RemovePeer(netPeer);
						//go to new connection
						break;
					case ConnectRequestResult.P2PLose:
						DisconnectPeerForce(netPeer, DisconnectReason.PeerToPeerConnection, 0, null);
						RemovePeer(netPeer);
						//go to new connection
						break;
					default:
						//no operations needed
						return;
				}
				//ConnectRequestResult.NewConnection
				//Set next connection number
				if (processResult != ConnectRequestResult.P2PLose)
					connRequest.ConnectionNumber = (byte) ((netPeer.ConnectionNum + 1) % NetConstants.MaxConnectionNumber);
				//To reconnect peer
			}
			else
			{
				NetDebug.Write($"ConnectRequest Id: {connRequest.ConnectionTime}, EP: {remoteEndPoint}");
			}

			ConnectionRequest req;
			lock (_requestsDict)
			{
				if (_requestsDict.TryGetValue(remoteEndPoint, out req))
				{
					req.UpdateRequest(connRequest);
					return;
				}
				req = new ConnectionRequest(remoteEndPoint, connRequest, this);
				_requestsDict.Add(remoteEndPoint, req);
			}
			NetDebug.Write($"[NM] Creating request event: {connRequest.ConnectionTime}");
			CreateEvent(NetEvent.EType.ConnectionRequest, connectionRequest: req);
		}

		private void OnMessageReceived(NetPacket packet, IPEndPoint remoteEndPoint)
		{
#if DEBUG
			if (SimulatePacketLoss && _randomGenerator.NextDouble() * 100 < SimulationPacketLossChance)
			{
				//drop packet
				return;
			}
			if (SimulateLatency)
			{
				int latency = _randomGenerator.Next(SimulationMinLatency, SimulationMaxLatency);
				if (latency > MinLatencyThreshold)
				{
					lock (_pingSimulationList)
					{
						_pingSimulationList.Add(new IncomingData
						{
							Data = packet,
							EndPoint = remoteEndPoint,
							TimeWhenGet = DateTime.UtcNow.AddMilliseconds(latency)
						});
					}
					//hold packet
					return;
				}
			}

			//ProcessEvents
			DebugMessageReceived(packet, remoteEndPoint);
		}

		private void DebugMessageReceived(NetPacket packet, IPEndPoint remoteEndPoint)
		{
#endif
			var originalPacketSize = packet.Size;
			if (EnableStatistics)
			{
				Statistics.IncrementPacketsReceived();
				Statistics.AddBytesReceived(originalPacketSize);
			}

			if (_ntpRequests.Count > 0)
			{
				if (_ntpRequests.TryGetValue(remoteEndPoint, out var request))
				{
					if (packet.Size < 48)
					{
						NetDebug.Write(NetLogLevel.Trace, $"NTP response too short: {packet.Size}");
						return;
					}

					byte[] copiedData = new byte[packet.Size];
					Buffer.BlockCopy(packet.RawData, 0, copiedData, 0, packet.Size);
					NtpPacket ntpPacket = NtpPacket.FromServerResponse(copiedData, DateTime.UtcNow);
					try
					{
						ntpPacket.ValidateReply();
					}
					catch (InvalidOperationException ex)
					{
						NetDebug.Write(NetLogLevel.Trace, $"NTP response error: {ex.Message}");
						ntpPacket = null;
					}

					if (ntpPacket != null)
					{
						_ntpRequests.Remove(remoteEndPoint);
						_ntpEventListener?.OnNtpResponse(ntpPacket);
					}
					return;
				}
			}

			if (_extraPacketLayer != null)
			{
				int start = 0;
				_extraPacketLayer.ProcessInboundPacket(ref remoteEndPoint, ref packet.RawData, ref start, ref packet.Size);
				if (packet.Size == 0)
					return;
			}

			if (!packet.Verify())
			{
				// Requests A2S_Info data from server.
				if (packet.RawData.Length >= 5 && packet.RawData[4] == 0x54)
				{
					//_udpSocketv4.SendTo(SteamServerInfo.Serialize(), SocketFlags.None, remoteEndPoint);
					PoolRecycle(packet);
					return;
				}

				NetDebug.WriteError("[NM] DataReceived: bad!");
				PoolRecycle(packet);
				return;
			}

			switch (packet.Property)
			{
				//special case connect request
				case PacketProperty.ConnectRequest:
					if (NetConnectRequestPacket.GetProtocolId(packet) != NetConstants.ProtocolId)
					{
						SendRawAndRecycle(PoolGetWithProperty(PacketProperty.InvalidProtocol), remoteEndPoint);
						return;
					}
					break;
				//unconnected messages
				case PacketProperty.Broadcast:
					if (!BroadcastReceiveEnabled)
						return;
					CreateEvent(NetEvent.EType.Broadcast, remoteEndPoint: remoteEndPoint, readerSource: packet);
					return;
				case PacketProperty.UnconnectedMessage:
					if (!UnconnectedMessagesEnabled)
						return;
					CreateEvent(NetEvent.EType.ReceiveUnconnected, remoteEndPoint: remoteEndPoint, readerSource: packet);
					return;
				case PacketProperty.NatMessage:
					if (NatPunchEnabled)
						NatPunchModule.ProcessMessage(remoteEndPoint, packet);
					return;
			}

			//Check normal packets
			_peersLock.EnterReadLock();
			bool peerFound = _peersDict.TryGetValue(remoteEndPoint, out var netPeer);
			_peersLock.ExitReadLock();

			if (peerFound && EnableStatistics)
			{
				netPeer.Statistics.IncrementPacketsReceived();
				netPeer.Statistics.AddBytesReceived(originalPacketSize);
			}

			switch (packet.Property)
			{
				case PacketProperty.ConnectRequest:
					var connRequest = NetConnectRequestPacket.FromData(packet);
					if (connRequest != null)
						ProcessConnectRequest(remoteEndPoint, netPeer, connRequest);
					break;

				case PacketProperty.PeerNotFound:
					if (peerFound) //local
					{
						if (netPeer.ConnectionState != ConnectionState.Connected)
							return;
						if (packet.Size == 1)
						{
							//first reply
							//send NetworkChanged packet
							netPeer.ResetMtu();
							SendRaw(NetConnectAcceptPacket.MakeNetworkChanged(netPeer), remoteEndPoint);
							NetDebug.Write($"PeerNotFound sending connection info: {remoteEndPoint}");
						}
						else if (packet.Size == 2 && packet.RawData[1] == 1)
						{
							//second reply
							DisconnectPeerForce(netPeer, DisconnectReason.PeerNotFound, 0, null);
						}
					}
					else if (packet.Size > 1) //remote
					{
						//check if this is old peer
						bool isOldPeer = false;

						if (AllowPeerAddressChange)
						{
							NetDebug.Write($"[NM] Looks like address change: {packet.Size}");
							var remoteData = NetConnectAcceptPacket.FromData(packet);
							if (remoteData != null &&
							    remoteData.PeerNetworkChanged &&
							    remoteData.PeerId < _peersArray.Length)
							{
								_peersLock.EnterUpgradeableReadLock();
								var peer = _peersArray[remoteData.PeerId];
								if (peer != null &&
								    peer.ConnectTime == remoteData.ConnectionTime &&
								    peer.ConnectionNum == remoteData.ConnectionNumber)
								{
									if (peer.ConnectionState == ConnectionState.Connected)
									{
										peer.InitiateEndPointChange();
										if (_peerAddressChangedListener != null)
										{
											CreateEvent(NetEvent.EType.PeerAddressChanged, peer, remoteEndPoint);
										}
										NetDebug.Write("[NM] PeerNotFound change address of remote peer");
									}
									isOldPeer = true;
								}
								_peersLock.ExitUpgradeableReadLock();
							}
						}

						PoolRecycle(packet);

						//else peer really not found
						if (!isOldPeer)
						{
							var secondResponse = PoolGetWithProperty(PacketProperty.PeerNotFound, 1);
							secondResponse.RawData[1] = 1;
							SendRawAndRecycle(secondResponse, remoteEndPoint);
						}
					}
					break;
				case PacketProperty.InvalidProtocol:
					if (peerFound && netPeer.ConnectionState == ConnectionState.Outgoing)
						DisconnectPeerForce(netPeer, DisconnectReason.InvalidProtocol, 0, null);
					break;

				case PacketProperty.Disconnect:
					if (peerFound)
					{
						var disconnectResult = netPeer.ProcessDisconnect(packet);
						if (disconnectResult == DisconnectResult.None)
						{
							PoolRecycle(packet);
							return;
						}
						DisconnectPeerForce(netPeer,
							disconnectResult == DisconnectResult.Disconnect
								? DisconnectReason.RemoteConnectionClose
								: DisconnectReason.ConnectionRejected,
							0, packet);
					}
					else
					{
						PoolRecycle(packet);
					}
					//Send shutdown
					SendRawAndRecycle(PoolGetWithProperty(PacketProperty.ShutdownOk), remoteEndPoint);
					break;
				case PacketProperty.ConnectAccept:
					if (!peerFound)
						return;
					var connAccept = NetConnectAcceptPacket.FromData(packet);
					if (connAccept != null && netPeer.ProcessConnectAccept(connAccept))
						CreateEvent(NetEvent.EType.Connect, netPeer);
					break;
				default:
					if (peerFound)
						netPeer.ProcessPacket(packet);
					else
						SendRawAndRecycle(PoolGetWithProperty(PacketProperty.PeerNotFound), remoteEndPoint);
					break;
			}
		}

		internal void CreateReceiveEvent(NetPacket packet, DeliveryMethod method, byte channelNumber, int headerSize, NetPeer fromPeer)
		{
			NetEvent evt;

			if (UnsyncedEvents || UnsyncedReceiveEvent || _manualMode)
			{
				lock (_eventLock)
				{
					evt = _netEventPoolHead;
					if (evt == null)
						evt = new NetEvent(this);
					else
						_netEventPoolHead = evt.Next;
				}
				evt.Next = null;
				evt.Type = NetEvent.EType.Receive;
				evt.DataReader.SetSource(packet, headerSize);
				evt.Peer = fromPeer;
				evt.DeliveryMethod = method;
				evt.ChannelNumber = channelNumber;
				ProcessEvent(evt);
			}
			else
			{
				lock (_eventLock)
				{
					evt = _netEventPoolHead;
					if (evt == null)
						evt = new NetEvent(this);
					else
						_netEventPoolHead = evt.Next;

					evt.Next = null;
					evt.Type = NetEvent.EType.Receive;
					evt.DataReader.SetSource(packet, headerSize);
					evt.Peer = fromPeer;
					evt.DeliveryMethod = method;
					evt.ChannelNumber = channelNumber;

					if (_pendingEventTail == null)
						_pendingEventHead = evt;
					else
						_pendingEventTail.Next = evt;
					_pendingEventTail = evt;
				}
			}
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="writer">DataWriter with data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(NetDataWriter writer, DeliveryMethod options)
		{
			SendToAll(writer.Data, 0, writer.Length, options);
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(byte[] data, DeliveryMethod options)
		{
			SendToAll(data, 0, data.Length, options);
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(byte[] data, int start, int length, DeliveryMethod options)
		{
			SendToAll(data, start, length, 0, options);
		}

		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="writer">DataWriter with data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options)
		{
			SendToAll(writer.Data, 0, writer.Length, channelNumber, options);
		}

		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options)
		{
			SendToAll(data, 0, data.Length, channelNumber, options);
		}

		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options)
		{
			try
			{
				_peersLock.EnterReadLock();
				for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
					netPeer.Send(data, start, length, channelNumber, options);
			}
			finally
			{
				_peersLock.ExitReadLock();
			}
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="writer">DataWriter with data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(NetDataWriter writer, DeliveryMethod options, NetPeer excludePeer)
		{
			SendToAll(writer.Data, 0, writer.Length, 0, options, excludePeer);
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(byte[] data, DeliveryMethod options, NetPeer excludePeer)
		{
			SendToAll(data, 0, data.Length, 0, options, excludePeer);
		}

		/// <summary>
		/// Send data to all connected peers (channel - 0)
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(byte[] data, int start, int length, DeliveryMethod options, NetPeer excludePeer)
		{
			SendToAll(data, start, length, 0, options, excludePeer);
		}

		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="writer">DataWriter with data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
		{
			SendToAll(writer.Data, 0, writer.Length, channelNumber, options, excludePeer);
		}

		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
		{
			SendToAll(data, 0, data.Length, channelNumber, options, excludePeer);
		}


		/// <summary>
		/// Send data to all connected peers
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="start">Start of data</param>
		/// <param name="length">Length of data</param>
		/// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
		/// <param name="options">Send options (reliable, unreliable, etc.)</param>
		/// <param name="excludePeer">Excluded peer</param>
		public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
		{
			try
			{
				_peersLock.EnterReadLock();
				for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
				{
					if (netPeer != excludePeer)
						netPeer.Send(data, start, length, channelNumber, options);
				}
			}
			finally
			{
				_peersLock.ExitReadLock();
			}
		}

		/// <summary>
		/// Start logic thread and listening on available port
		/// </summary>
		public bool Start()
		{
			return Start(0);
		}

		/// <summary>
		/// Start logic thread and listening on selected port
		/// </summary>
		/// <param name="addressIPv4">bind to specific ipv4 address</param>
		/// <param name="addressIPv6">bind to specific ipv6 address</param>
		/// <param name="port">port to listen</param>
		public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port)
		{
			return Start(addressIPv4, addressIPv6, port, false);
		}

		/// <summary>
		/// Start logic thread and listening on selected port
		/// </summary>
		/// <param name="addressIPv4">bind to specific ipv4 address</param>
		/// <param name="addressIPv6">bind to specific ipv6 address</param>
		/// <param name="port">port to listen</param>
		public bool Start(string addressIPv4, string addressIPv6, int port)
		{
			IPAddress ipv4 = NetUtils.ResolveAddress(addressIPv4);
			IPAddress ipv6 = NetUtils.ResolveAddress(addressIPv6);
			return Start(ipv4, ipv6, port);
		}

		/// <summary>
		/// Start logic thread and listening on selected port
		/// </summary>
		/// <param name="port">port to listen</param>
		public bool Start(int port)
		{
			return Start(IPAddress.Any, IPAddress.IPv6Any, port);
		}

		/// <summary>
		/// Start in manual mode and listening on selected port
		/// In this mode you should use ManualReceive (without PollEvents) for receive packets
		/// and ManualUpdate(...) for update and send packets
		/// This mode useful mostly for single-threaded servers
		/// </summary>
		/// <param name="addressIPv4">bind to specific ipv4 address</param>
		/// <param name="addressIPv6">bind to specific ipv6 address</param>
		/// <param name="port">port to listen</param>
		public bool StartInManualMode(IPAddress addressIPv4, IPAddress addressIPv6, int port)
		{
			return Start(addressIPv4, addressIPv6, port, true);
		}

		/// <summary>
		/// Start in manual mode and listening on selected port
		/// In this mode you should use ManualReceive (without PollEvents) for receive packets
		/// and ManualUpdate(...) for update and send packets
		/// This mode useful mostly for single-threaded servers
		/// </summary>
		/// <param name="addressIPv4">bind to specific ipv4 address</param>
		/// <param name="addressIPv6">bind to specific ipv6 address</param>
		/// <param name="port">port to listen</param>
		public bool StartInManualMode(string addressIPv4, string addressIPv6, int port)
		{
			IPAddress ipv4 = NetUtils.ResolveAddress(addressIPv4);
			IPAddress ipv6 = NetUtils.ResolveAddress(addressIPv6);
			return StartInManualMode(ipv4, ipv6, port);
		}

		/// <summary>
		/// Start in manual mode and listening on selected port
		/// In this mode you should use ManualReceive (without PollEvents) for receive packets
		/// and ManualUpdate(...) for update and send packets
		/// This mode useful mostly for single-threaded servers
		/// </summary>
		/// <param name="port">port to listen</param>
		public bool StartInManualMode(int port)
		{
			return StartInManualMode(IPAddress.Any, IPAddress.IPv6Any, port);
		}

		/// <summary>
		/// Send message without connection
		/// </summary>
		/// <param name="message">Raw data</param>
		/// <param name="remoteEndPoint">Packet destination</param>
		/// <returns>Operation result</returns>
		public bool SendUnconnectedMessage(byte[] message, IPEndPoint remoteEndPoint)
		{
			return SendUnconnectedMessage(message, 0, message.Length, remoteEndPoint);
		}

		/// <summary>
		/// Send message without connection. WARNING This method allocates a new IPEndPoint object and
		/// synchronously makes a DNS request. If you're calling this method every frame it will be
		/// much faster to just cache the IPEndPoint.
		/// </summary>
		/// <param name="writer">Data serializer</param>
		/// <param name="address">Packet destination IP or hostname</param>
		/// <param name="port">Packet destination port</param>
		/// <returns>Operation result</returns>
		public bool SendUnconnectedMessage(NetDataWriter writer, string address, int port)
		{
			IPEndPoint remoteEndPoint = NetUtils.MakeEndPoint(address, port);

			return SendUnconnectedMessage(writer.Data, 0, writer.Length, remoteEndPoint);
		}

		/// <summary>
		/// Send message without connection
		/// </summary>
		/// <param name="writer">Data serializer</param>
		/// <param name="remoteEndPoint">Packet destination</param>
		/// <returns>Operation result</returns>
		public bool SendUnconnectedMessage(NetDataWriter writer, IPEndPoint remoteEndPoint)
		{
			return SendUnconnectedMessage(writer.Data, 0, writer.Length, remoteEndPoint);
		}

		/// <summary>
		/// Send message without connection
		/// </summary>
		/// <param name="message">Raw data</param>
		/// <param name="start">data start</param>
		/// <param name="length">data length</param>
		/// <param name="remoteEndPoint">Packet destination</param>
		/// <returns>Operation result</returns>
		public bool SendUnconnectedMessage(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
		{
			//No need for CRC here, SendRaw does that
			NetPacket packet = PoolGetWithData(PacketProperty.UnconnectedMessage, message, start, length);
			return SendRawAndRecycle(packet, remoteEndPoint) > 0;
		}

		/// <summary>
		/// Triggers update and send logic immediately (works asynchronously)
		/// </summary>
		public void TriggerUpdate()
		{
			_updateTriggerEvent.Set();
		}

		/// <summary>
		/// Receive all pending events. Call this in game update code
		/// In Manual mode it will call also socket Receive (which can be slow)
		/// </summary>
		public void PollEvents()
		{
			if (_manualMode)
			{
				if (_udpSocketv4 != null)
					ManualReceive(_udpSocketv4, _bufferEndPointv4);
				if (_udpSocketv6 != null && _udpSocketv6 != _udpSocketv4)
					ManualReceive(_udpSocketv6, _bufferEndPointv6);
				ProcessDelayedPackets();
				return;
			}
			if (UnsyncedEvents)
				return;
			NetEvent pendingEvent;
			lock (_eventLock)
			{
				pendingEvent = _pendingEventHead;
				_pendingEventHead = null;
				_pendingEventTail = null;
			}

			while (pendingEvent != null)
			{
				var next = pendingEvent.Next;
				ProcessEvent(pendingEvent);
				pendingEvent = next;
			}
		}

		/// <summary>
		/// Connect to remote host
		/// </summary>
		/// <param name="address">Server IP or hostname</param>
		/// <param name="port">Server Port</param>
		/// <param name="key">Connection key</param>
		/// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
		/// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
		public NetPeer Connect(string address, int port, string key)
		{
			return Connect(address, port, NetDataWriter.FromString(key));
		}

		/// <summary>
		/// Connect to remote host
		/// </summary>
		/// <param name="address">Server IP or hostname</param>
		/// <param name="port">Server Port</param>
		/// <param name="connectionData">Additional data for remote peer</param>
		/// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
		/// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
		public NetPeer Connect(string address, int port, NetDataWriter connectionData)
		{
			IPEndPoint ep;
			try
			{
				ep = NetUtils.MakeEndPoint(address, port);
			}
			catch
			{
				CreateEvent(NetEvent.EType.Disconnect, disconnectReason: DisconnectReason.UnknownHost);
				return null;
			}
			return Connect(ep, connectionData);
		}

		/// <summary>
		/// Connect to remote host
		/// </summary>
		/// <param name="target">Server end point (ip and port)</param>
		/// <param name="key">Connection key</param>
		/// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
		/// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
		public NetPeer Connect(IPEndPoint target, string key)
		{
			return Connect(target, NetDataWriter.FromString(key));
		}

		/// <summary>
		/// Connect to remote host
		/// </summary>
		/// <param name="target">Server end point (ip and port)</param>
		/// <param name="connectionData">Additional data for remote peer</param>
		/// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
		/// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
		public NetPeer Connect(IPEndPoint target, NetDataWriter connectionData)
		{
			if (!IsRunning)
				throw new InvalidOperationException("Client is not running");

			lock (_requestsDict)
			{
				if (_requestsDict.ContainsKey(target))
					return null;
			}

			byte connectionNumber = 0;
			_peersLock.EnterUpgradeableReadLock();
			if (_peersDict.TryGetValue(target, out var peer))
			{
				switch (peer.ConnectionState)
				{
					//just return already connected peer
					case ConnectionState.Connected:
					case ConnectionState.Outgoing:
						_peersLock.ExitUpgradeableReadLock();
						return peer;
				}
				//else reconnect
				connectionNumber = (byte) ((peer.ConnectionNum + 1) % NetConstants.MaxConnectionNumber);
				RemovePeer(peer);
			}

			//Create reliable connection
			//And send connection request
			peer = new NetPeer(this, target, GetNextPeerId(), connectionNumber, connectionData);
			AddPeer(peer);
			_peersLock.ExitUpgradeableReadLock();

			return peer;
		}

		/// <summary>
		/// Force closes connection and stop all threads.
		/// </summary>
		public void Stop()
		{
			Stop(true);
		}

		/// <summary>
		/// Force closes connection and stop all threads.
		/// </summary>
		/// <param name="sendDisconnectMessages">Send disconnect messages</param>
		public void Stop(bool sendDisconnectMessages)
		{
			if (!IsRunning)
				return;
			NetDebug.Write("[NM] Stop");

#if UNITY_2018_3_OR_NEWER
			_pausedSocketFix.Deinitialize();
			_pausedSocketFix = null;
#endif

			//Send last disconnect
			for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
				netPeer.Shutdown(null, 0, 0, !sendDisconnectMessages);

			//Stop
			CloseSocket();
			_updateTriggerEvent.Set();
			if (!_manualMode)
			{
				_logicThread.Join();
				_logicThread = null;
			}

			//clear peers
			_peersLock.EnterWriteLock();
			_headPeer = null;
			_peersDict.Clear();
			_peersArray = new NetPeer[32];
			_peersLock.ExitWriteLock();
			_peerIds = new ConcurrentQueue<int>();
			_lastPeerId = 0;
#if DEBUG
			lock (_pingSimulationList)
				_pingSimulationList.Clear();
#endif
			_connectedPeersCount = 0;
			_pendingEventHead = null;
			_pendingEventTail = null;
		}

		/// <summary>
		/// Return peers count with connection state
		/// </summary>
		/// <param name="peerState">peer connection state (you can use as bit flags)</param>
		/// <returns>peers count</returns>
		public int GetPeersCount(ConnectionState peerState)
		{
			int count = 0;
			_peersLock.EnterReadLock();
			for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
			{
				if ((netPeer.ConnectionState & peerState) != 0)
					count++;
			}
			_peersLock.ExitReadLock();
			return count;
		}

		/// <summary>
		/// Get copy of peers (without allocations)
		/// </summary>
		/// <param name="peers">List that will contain result</param>
		/// <param name="peerState">State of peers</param>
		public void GetPeersNonAlloc(List<NetPeer> peers, ConnectionState peerState)
		{
			peers.Clear();
			_peersLock.EnterReadLock();
			for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
			{
				if ((netPeer.ConnectionState & peerState) != 0)
					peers.Add(netPeer);
			}
			_peersLock.ExitReadLock();
		}

		/// <summary>
		/// Disconnect all peers without any additional data
		/// </summary>
		public void DisconnectAll()
		{
			DisconnectAll(null, 0, 0);
		}

		/// <summary>
		/// Disconnect all peers with shutdown message
		/// </summary>
		/// <param name="data">Data to send (must be less or equal MTU)</param>
		/// <param name="start">Data start</param>
		/// <param name="count">Data count</param>
		public void DisconnectAll(byte[] data, int start, int count)
		{
			//Send disconnect packets
			_peersLock.EnterReadLock();
			for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
			{
				DisconnectPeer(netPeer,
					DisconnectReason.DisconnectPeerCalled,
					0,
					false,
					data,
					start,
					count,
					null);
			}
			_peersLock.ExitReadLock();
		}

		/// <summary>
		/// Immediately disconnect peer from server without additional data
		/// </summary>
		/// <param name="peer">peer to disconnect</param>
		public void DisconnectPeerForce(NetPeer peer)
		{
			DisconnectPeerForce(peer, DisconnectReason.DisconnectPeerCalled, 0, null);
		}

		/// <summary>
		/// Disconnect peer from server
		/// </summary>
		/// <param name="peer">peer to disconnect</param>
		public void DisconnectPeer(NetPeer peer)
		{
			DisconnectPeer(peer, null, 0, 0);
		}

		/// <summary>
		/// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
		/// </summary>
		/// <param name="peer">peer to disconnect</param>
		/// <param name="data">additional data</param>
		public void DisconnectPeer(NetPeer peer, byte[] data)
		{
			DisconnectPeer(peer, data, 0, data.Length);
		}

		/// <summary>
		/// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
		/// </summary>
		/// <param name="peer">peer to disconnect</param>
		/// <param name="writer">additional data</param>
		public void DisconnectPeer(NetPeer peer, NetDataWriter writer)
		{
			DisconnectPeer(peer, writer.Data, 0, writer.Length);
		}

		/// <summary>
		/// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
		/// </summary>
		/// <param name="peer">peer to disconnect</param>
		/// <param name="data">additional data</param>
		/// <param name="start">data start</param>
		/// <param name="count">data length</param>
		public void DisconnectPeer(NetPeer peer, byte[] data, int start, int count)
		{
			DisconnectPeer(peer,
				DisconnectReason.DisconnectPeerCalled,
				0,
				false,
				data,
				start,
				count,
				null);
		}

		/// <summary>
		/// Create the requests for NTP server
		/// </summary>
		/// <param name="endPoint">NTP Server address.</param>
		public void CreateNtpRequest(IPEndPoint endPoint)
		{
			_ntpRequests.Add(endPoint, new NtpRequest(endPoint));
		}

		/// <summary>
		/// Create the requests for NTP server
		/// </summary>
		/// <param name="ntpServerAddress">NTP Server address.</param>
		/// <param name="port">port</param>
		public void CreateNtpRequest(string ntpServerAddress, int port)
		{
			IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, port);
			_ntpRequests.Add(endPoint, new NtpRequest(endPoint));
		}

		/// <summary>
		/// Create the requests for NTP server (default port)
		/// </summary>
		/// <param name="ntpServerAddress">NTP Server address.</param>
		public void CreateNtpRequest(string ntpServerAddress)
		{
			IPEndPoint endPoint = NetUtils.MakeEndPoint(ntpServerAddress, NtpRequest.DefaultPort);
			_ntpRequests.Add(endPoint, new NtpRequest(endPoint));
		}

		public NetPeerEnumerator GetEnumerator()
		{
			return new NetPeerEnumerator(_headPeer);
		}

		IEnumerator<NetPeer> IEnumerable<NetPeer>.GetEnumerator()
		{
			return new NetPeerEnumerator(_headPeer);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new NetPeerEnumerator(_headPeer);
		}
	}
}
