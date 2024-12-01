using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
	public partial class NetManager
	{
		private const int ReceivePollingTime = 500000; //0.5 second

		private Socket _udpSocketv4;
		private Socket _udpSocketv6;
		private Thread _receiveThread;
		private IPEndPoint _bufferEndPointv4;
		private IPEndPoint _bufferEndPointv6;
#if UNITY_2018_3_OR_NEWER
		private PausedSocketFix _pausedSocketFix;
#endif

#if !LITENETLIB_UNSAFE
		[ThreadStatic]
		private static byte[] _sendToBuffer;
#endif
		[ThreadStatic]
		private static byte[] _endPointBuffer;

		private readonly Dictionary<NativeAddr, IPEndPoint> _nativeAddrMap = new Dictionary<NativeAddr, IPEndPoint>();

		private const int SioUdpConnreset = -1744830452; //SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12
		private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("ff02::1");
		public static readonly bool IPv6Support;

		/// <summary>
		/// Maximum packets count that will be processed in Manual PollEvents
		/// </summary>
		public int MaxPacketsReceivePerUpdate = 0;

		// special case in iOS (and possibly android that should be resolved in unity)
		internal bool NotConnected;

		public short Ttl
		{
			get
			{
#if UNITY_SWITCH
                return 0;
#else
				return _udpSocketv4.Ttl;
#endif
			}
			internal set
			{
#if !UNITY_SWITCH
				_udpSocketv4.Ttl = value;
#endif
			}
		}

		static NetManager()
		{
#if DISABLE_IPV6
            IPv6Support = false;
#elif !UNITY_2019_1_OR_NEWER && !UNITY_2018_4_OR_NEWER && (!UNITY_EDITOR && ENABLE_IL2CPP)
            string version = UnityEngine.Application.unityVersion;
            IPv6Support = Socket.OSSupportsIPv6 && int.Parse(version.Remove(version.IndexOf('f')).Split('.')[2]) >= 6;
#else
			IPv6Support = Socket.OSSupportsIPv6;
#endif
		}

		private void RegisterEndPoint(IPEndPoint ep)
		{
			if (UseNativeSockets && ep is NativeEndPoint nep)
			{
				_nativeAddrMap.Add(new NativeAddr(nep.NativeAddress, nep.NativeAddress.Length), nep);
			}
		}

		private void UnregisterEndPoint(IPEndPoint ep)
		{
			if (UseNativeSockets && ep is NativeEndPoint nep)
			{
				var nativeAddr = new NativeAddr(nep.NativeAddress, nep.NativeAddress.Length);
				_nativeAddrMap.Remove(nativeAddr);
			}
		}

		private bool ProcessError(SocketException ex)
		{
			switch (ex.SocketErrorCode)
			{
				case SocketError.NotConnected:
					NotConnected = true;
					return true;
				case SocketError.Interrupted:
				case SocketError.NotSocket:
				case SocketError.OperationAborted:
					return true;
				case SocketError.ConnectionReset:
				case SocketError.MessageSize:
				case SocketError.TimedOut:
				case SocketError.NetworkReset:
					//NetDebug.Write($"[R]Ignored error: {(int)ex.SocketErrorCode} - {ex}");
					break;
				default:
					NetDebug.WriteError($"[R]Error code: {(int) ex.SocketErrorCode} - {ex}");
					CreateEvent(NetEvent.EType.Error, errorCode: ex.SocketErrorCode);
					break;
			}
			return false;
		}

		private void ManualReceive(Socket socket, EndPoint bufferEndPoint)
		{
			//Reading data
			try
			{
				int packetsReceived = 0;
				while (socket.Available > 0)
				{
					ReceiveFrom(socket, ref bufferEndPoint);
					packetsReceived++;
					if (packetsReceived == MaxPacketsReceivePerUpdate)
						break;
				}
			}
			catch (SocketException ex)
			{
				ProcessError(ex);
			}
			catch (ObjectDisposedException) { }
			catch (Exception e)
			{
				//protects socket receive thread
				NetDebug.WriteError("[NM] SocketReceiveThread error: " + e);
			}
		}

		private bool NativeReceiveFrom(ref NetPacket packet, IntPtr s, byte[] addrBuffer, int addrSize)
		{
			//Reading data
			packet.Size = NativeSocket.RecvFrom(s, packet.RawData, NetConstants.MaxPacketSize, addrBuffer, ref addrSize);
			if (packet.Size == 0)
				return false; //socket closed
			if (packet.Size == -1)
			{
				var errorCode = NativeSocket.GetSocketError();
				//Linux timeout EAGAIN
				return errorCode == SocketError.WouldBlock || errorCode == SocketError.TimedOut || ProcessError(new SocketException((int) errorCode)) == false;
			}

			var nativeAddr = new NativeAddr(addrBuffer, addrSize);
			if (!_nativeAddrMap.TryGetValue(nativeAddr, out var endPoint))
				endPoint = new NativeEndPoint(addrBuffer);

			//All ok!
			//NetDebug.WriteForce($"[R]Received data from {endPoint}, result: {packet.Size}");
			OnMessageReceived(packet, endPoint);
			packet = PoolGetPacket(NetConstants.MaxPacketSize);
			return true;
		}

		private void NativeReceiveLogic()
		{
			IntPtr socketHandle4 = _udpSocketv4.Handle;
			IntPtr socketHandle6 = _udpSocketv6?.Handle ?? IntPtr.Zero;
			byte[] addrBuffer4 = new byte[NativeSocket.IPv4AddrSize];
			byte[] addrBuffer6 = new byte[NativeSocket.IPv6AddrSize];
			int addrSize4 = addrBuffer4.Length;
			int addrSize6 = addrBuffer6.Length;
			var selectReadList = new List<Socket>(2);
			var socketv4 = _udpSocketv4;
			var socketV6 = _udpSocketv6;
			var packet = PoolGetPacket(NetConstants.MaxPacketSize);

			while (IsRunning)
			{
				if (socketV6 == null)
				{
					NativeReceiveFrom(ref packet, socketHandle4, addrBuffer4, addrSize4);
				}
				bool messageReceived = false;
				if (socketv4.Available != 0 || selectReadList.Contains(socketv4))
				{
					NativeReceiveFrom(ref packet, socketHandle4, addrBuffer4, addrSize4);
					messageReceived = true;
				}
				if (socketV6.Available != 0 || selectReadList.Contains(socketV6))
				{
					NativeReceiveFrom(ref packet, socketHandle6, addrBuffer6, addrSize6);
					messageReceived = true;
				}

				selectReadList.Clear();

				if (messageReceived)
					continue;

				selectReadList.Add(socketv4);
				selectReadList.Add(socketV6);
				try
				{
					Socket.Select(selectReadList, null, null, ReceivePollingTime);
				}
				catch (SocketException ex)
				{
					if (ProcessError(ex))
						return;
				}
				catch (ObjectDisposedException)
				{
					//socket closed
					return;
				}
				catch (ThreadAbortException)
				{
					//thread closed
					return;
				}
				catch (Exception e)
				{
					//protects socket receive thread
					NetDebug.WriteError("[NM] SocketReceiveThread error: " + e);
				}
			}
		}

		private void ReceiveFrom(Socket s, ref EndPoint bufferEndPoint)
		{
			var packet = PoolGetPacket(NetConstants.MaxPacketSize);
			packet.Size = s.ReceiveFrom(packet.RawData, 0, NetConstants.MaxPacketSize, SocketFlags.None, ref bufferEndPoint);
			OnMessageReceived(packet, (IPEndPoint) bufferEndPoint);
		}

		private void ReceiveLogic()
		{
			EndPoint bufferEndPoint4 = new IPEndPoint(IPAddress.Any, 0);
			EndPoint bufferEndPoint6 = new IPEndPoint(IPAddress.IPv6Any, 0);
			var selectReadList = new List<Socket>(2);
			var socketv4 = _udpSocketv4;
			var socketV6 = _udpSocketv6;

			while (IsRunning)
			{
				//Reading data
				try
				{
					if (socketV6 == null)
					{
						if (socketv4.Available == 0 && !socketv4.Poll(ReceivePollingTime, SelectMode.SelectRead))
							continue;
						ReceiveFrom(socketv4, ref bufferEndPoint4);
					}
					else
					{
						bool messageReceived = false;
						if (socketv4.Available != 0 || selectReadList.Contains(socketv4))
						{
							ReceiveFrom(socketv4, ref bufferEndPoint4);
							messageReceived = true;
						}
						if (socketV6.Available != 0 || selectReadList.Contains(socketV6))
						{
							ReceiveFrom(socketV6, ref bufferEndPoint6);
							messageReceived = true;
						}

						selectReadList.Clear();

						if (messageReceived)
							continue;

						selectReadList.Add(socketv4);
						selectReadList.Add(socketV6);
						Socket.Select(selectReadList, null, null, ReceivePollingTime);
					}
					//NetDebug.Write(NetLogLevel.Trace, $"[R]Received data from {bufferEndPoint}, result: {packet.Size}");
				}
				catch (SocketException ex)
				{
					if (ProcessError(ex))
						return;
				}
				catch (ObjectDisposedException)
				{
					//socket closed
					return;
				}
				catch (ThreadAbortException)
				{
					//thread closed
					return;
				}
				catch (Exception e)
				{
					//protects socket receive thread
					NetDebug.WriteError("[NM] SocketReceiveThread error: " + e);
				}
			}
		}

		/// <summary>
		/// Start logic thread and listening on selected port
		/// </summary>
		/// <param name="addressIPv4">bind to specific ipv4 address</param>
		/// <param name="addressIPv6">bind to specific ipv6 address</param>
		/// <param name="port">port to listen</param>
		/// <param name="manualMode">mode of library</param>
		public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool manualMode)
		{
			if (IsRunning && NotConnected == false)
				return false;

			NotConnected = false;
			_manualMode = manualMode;
			UseNativeSockets = UseNativeSockets && NativeSocket.IsSupported;
			_udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			if (!BindSocket(_udpSocketv4, new IPEndPoint(addressIPv4, port)))
				return false;

			LocalPort = ((IPEndPoint) _udpSocketv4.LocalEndPoint).Port;

#if UNITY_2018_3_OR_NEWER
			if (_pausedSocketFix == null)
				_pausedSocketFix = new PausedSocketFix(this, addressIPv4, addressIPv6, port, manualMode);
#endif

			IsRunning = true;
			if (_manualMode)
			{
				_bufferEndPointv4 = new IPEndPoint(IPAddress.Any, 0);
			}

			//Check IPv6 support
			if (IPv6Support && IPv6Enabled)
			{
				_udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
				//Use one port for two sockets
				if (BindSocket(_udpSocketv6, new IPEndPoint(addressIPv6, LocalPort)))
				{
					if (_manualMode)
					{
						_bufferEndPointv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
					}
				}
				else
				{
					_udpSocketv6 = null;
				}
			}

			if (!manualMode)
			{
				ThreadStart ts = ReceiveLogic;
				if (UseNativeSockets)
					ts = NativeReceiveLogic;
				_receiveThread = new Thread(ts)
				{
					Name = $"ReceiveThread({LocalPort})",
					IsBackground = true
				};
				_receiveThread.Start();
				if (_logicThread == null)
				{
					_logicThread = new Thread(UpdateLogic)
					{
						Name = "LogicThread",
						IsBackground = true
					};
					_logicThread.Start();
				}
			}

			return true;
		}

		private bool BindSocket(Socket socket, IPEndPoint ep)
		{
			//Setup socket
			socket.ReceiveTimeout = 500;
			socket.SendTimeout = 500;
			socket.ReceiveBufferSize = NetConstants.SocketBufferSize;
			socket.SendBufferSize = NetConstants.SocketBufferSize;
			socket.Blocking = true;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try
				{
					socket.IOControl(SioUdpConnreset, new byte[]
					{
						0
					}, null);
				}
				catch
				{
					//ignored
				}
			}

			try
			{
				socket.ExclusiveAddressUse = !ReuseAddress;
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, ReuseAddress);
			}
			catch
			{
				//Unity with IL2CPP throws an exception here, it doesn't matter in most cases so just ignore it
			}
			if (ep.AddressFamily == AddressFamily.InterNetwork)
			{
				Ttl = NetConstants.SocketTTL;

				try { socket.EnableBroadcast = true; }
				catch (SocketException e)
				{
					NetDebug.WriteError($"[B]Broadcast error: {e.SocketErrorCode}");
				}

				if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					try { socket.DontFragment = true; }
					catch (SocketException e)
					{
						NetDebug.WriteError($"[B]DontFragment error: {e.SocketErrorCode}");
					}
				}
			}
			//Bind
			try
			{
				socket.Bind(ep);
				NetDebug.Write(NetLogLevel.Trace, $"[B]Successfully binded to port: {((IPEndPoint) socket.LocalEndPoint).Port}, AF: {socket.AddressFamily}");

				//join multicast
				if (ep.AddressFamily == AddressFamily.InterNetworkV6)
				{
					try
					{
#if !UNITY_2018_3_OR_NEWER
                        socket.SetSocketOption(
                            SocketOptionLevel.IPv6,
                            SocketOptionName.AddMembership,
                            new IPv6MulticastOption(MulticastAddressV6));
#endif
					}
					catch (Exception)
					{
						// Unity3d throws exception - ignored
					}
				}
			}
			catch (SocketException bindException)
			{
				switch (bindException.SocketErrorCode)
				{
					//IPv6 bind fix
					case SocketError.AddressAlreadyInUse:
						if (socket.AddressFamily == AddressFamily.InterNetworkV6)
						{
							try
							{
								//Set IPv6Only
								socket.DualMode = false;
								socket.Bind(ep);
							}
							catch (SocketException ex)
							{
								//because its fixed in 2018_3
								NetDebug.WriteError($"[B]Bind exception: {ex}, errorCode: {ex.SocketErrorCode}");
								return false;
							}
							return true;
						}
						break;
					//hack for iOS (Unity3D)
					case SocketError.AddressFamilyNotSupported: return true;
				}
				NetDebug.WriteError($"[B]Bind exception: {bindException}, errorCode: {bindException.SocketErrorCode}");
				return false;
			}
			return true;
		}

		internal int SendRawAndRecycle(NetPacket packet, IPEndPoint remoteEndPoint)
		{
			int result = SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
			PoolRecycle(packet);
			return result;
		}

		internal int SendRaw(NetPacket packet, IPEndPoint remoteEndPoint)
		{
			return SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
		}

		internal int SendRaw(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
		{
			if (!IsRunning)
				return 0;

			NetPacket expandedPacket = null;
			if (_extraPacketLayer != null)
			{
				expandedPacket = PoolGetPacket(length + _extraPacketLayer.ExtraPacketSizeForLayer);
				Buffer.BlockCopy(message, start, expandedPacket.RawData, 0, length);
				start = 0;
				_extraPacketLayer.ProcessOutBoundPacket(ref remoteEndPoint, ref expandedPacket.RawData, ref start, ref length);
				message = expandedPacket.RawData;
			}

			var socket = _udpSocketv4;
			if (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 && IPv6Support)
			{
				socket = _udpSocketv6;
				if (socket == null)
					return 0;
			}

			int result;
			try
			{
				if (UseNativeSockets)
				{
					byte[] socketAddress;

					if (remoteEndPoint is NativeEndPoint nep)
					{
						socketAddress = nep.NativeAddress;
					}
					else //Convert endpoint to raw
					{
						if (_endPointBuffer == null)
							_endPointBuffer = new byte[NativeSocket.IPv6AddrSize];
						socketAddress = _endPointBuffer;

						bool ipv4 = remoteEndPoint.AddressFamily == AddressFamily.InterNetwork;
						short addressFamily = NativeSocket.GetNativeAddressFamily(remoteEndPoint);

						socketAddress[0] = (byte) (addressFamily);
						socketAddress[1] = (byte) (addressFamily >> 8);
						socketAddress[2] = (byte) (remoteEndPoint.Port >> 8);
						socketAddress[3] = (byte) (remoteEndPoint.Port);

						if (ipv4)
						{
#pragma warning disable 618
							long addr = remoteEndPoint.Address.Address;
#pragma warning restore 618
							socketAddress[4] = (byte) (addr);
							socketAddress[5] = (byte) (addr >> 8);
							socketAddress[6] = (byte) (addr >> 16);
							socketAddress[7] = (byte) (addr >> 24);
						}
						else
						{
#if NETCOREAPP || NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
                            remoteEndPoint.Address.TryWriteBytes(new Span<byte>(socketAddress, 8, 16), out _);
#else
							byte[] addrBytes = remoteEndPoint.Address.GetAddressBytes();
							Buffer.BlockCopy(addrBytes, 0, socketAddress, 8, 16);
#endif
						}
					}

#if LITENETLIB_UNSAFE
                    unsafe
                    {
                        fixed (byte* dataWithOffset = &message[start])
                        {
                            result =
 NativeSocket.SendTo(socket.Handle, dataWithOffset, length, socketAddress, socketAddress.Length);
                        }
                    }
#else
					if (start > 0)
					{
						if (_sendToBuffer == null)
							_sendToBuffer = new byte[NetConstants.MaxPacketSize];
						Buffer.BlockCopy(message, start, _sendToBuffer, 0, length);
						message = _sendToBuffer;
					}

					result = NativeSocket.SendTo(socket.Handle, message, length, socketAddress, socketAddress.Length);
#endif
					if (result == -1)
						throw NativeSocket.GetSocketException();
				}
				else
				{
					result = socket.SendTo(message, start, length, SocketFlags.None, remoteEndPoint);
				}
				//NetDebug.WriteForce("[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
			}
			catch (SocketException ex)
			{
				switch (ex.SocketErrorCode)
				{
					case SocketError.NoBufferSpaceAvailable:
					case SocketError.Interrupted:
						return 0;
					case SocketError.MessageSize:
						NetDebug.Write(NetLogLevel.Trace, $"[SRD] 10040, datalen: {length}");
						return 0;

					case SocketError.HostUnreachable:
					case SocketError.NetworkUnreachable:
						if (DisconnectOnUnreachable && TryGetPeer(remoteEndPoint, out var fromPeer))
						{
							DisconnectPeerForce(fromPeer,
								ex.SocketErrorCode == SocketError.HostUnreachable
									? DisconnectReason.HostUnreachable
									: DisconnectReason.NetworkUnreachable,
								ex.SocketErrorCode,
								null);
						}

						CreateEvent(NetEvent.EType.Error, remoteEndPoint: remoteEndPoint, errorCode: ex.SocketErrorCode);
						return -1;

					default:
						NetDebug.WriteError($"[S] {ex}");
						return -1;
				}
			}
			catch (Exception ex)
			{
				NetDebug.WriteError($"[S] {ex}");
				return 0;
			}
			finally
			{
				if (expandedPacket != null)
				{
					PoolRecycle(expandedPacket);
				}
			}

			if (result <= 0)
				return 0;

			if (EnableStatistics)
			{
				Statistics.IncrementPacketsSent();
				Statistics.AddBytesSent(length);
			}

			return result;
		}

		public bool SendBroadcast(NetDataWriter writer, int port)
		{
			return SendBroadcast(writer.Data, 0, writer.Length, port);
		}

		public bool SendBroadcast(byte[] data, int port)
		{
			return SendBroadcast(data, 0, data.Length, port);
		}

		public bool SendBroadcast(byte[] data, int start, int length, int port)
		{
			if (!IsRunning)
				return false;

			NetPacket packet;
			if (_extraPacketLayer != null)
			{
				var headerSize = NetPacket.GetHeaderSize(PacketProperty.Broadcast);
				packet = PoolGetPacket(headerSize + length + _extraPacketLayer.ExtraPacketSizeForLayer);
				packet.Property = PacketProperty.Broadcast;
				Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
				var checksumComputeStart = 0;
				int preCrcLength = length + headerSize;
				IPEndPoint emptyEp = null;
				_extraPacketLayer.ProcessOutBoundPacket(ref emptyEp, ref packet.RawData, ref checksumComputeStart, ref preCrcLength);
			}
			else
			{
				packet = PoolGetWithData(PacketProperty.Broadcast, data, start, length);
			}

			bool broadcastSuccess = false;
			bool multicastSuccess = false;
			try
			{
				broadcastSuccess = _udpSocketv4.SendTo(packet.RawData,
					0,
					packet.Size,
					SocketFlags.None,
					new IPEndPoint(IPAddress.Broadcast, port)) > 0;

				if (_udpSocketv6 != null)
				{
					multicastSuccess = _udpSocketv6.SendTo(packet.RawData,
						0,
						packet.Size,
						SocketFlags.None,
						new IPEndPoint(MulticastAddressV6, port)) > 0;
				}
			}
			catch (Exception ex)
			{
				NetDebug.WriteError($"[S][MCAST] {ex}");
				return broadcastSuccess;
			}
			finally
			{
				PoolRecycle(packet);
			}

			return broadcastSuccess || multicastSuccess;
		}

		private void CloseSocket()
		{
			IsRunning = false;
			_udpSocketv4?.Close();
			_udpSocketv6?.Close();
			_udpSocketv4 = null;
			_udpSocketv6 = null;
			if (_receiveThread != null && _receiveThread != Thread.CurrentThread)
				_receiveThread.Join();
			_receiveThread = null;
		}
	}
}
