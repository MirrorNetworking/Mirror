using System.Net.Sockets;
using LiteNetLib;

namespace Mirror.LiteNetLib4Mirror
{
	public static class LiteNetLib4MirrorCore
	{
		public const string TransportVersion = "1.2.8";
		public static SocketError LastError { get; internal set; }
		public static SocketError LastDisconnectError { get; internal set; }
		public static DisconnectReason LastDisconnectReason { get; internal set; }
		public static NetManager Host { get; internal set; }
		public static States State { get; internal set; } = States.NonInitialized;

		public enum States : byte
		{
			NonInitialized,
			Idle,
			Discovery,
			ClientConnecting,
			ClientConnected,
			Server
		}

		internal static string GetState()
		{
			switch (State)
			{
				case States.NonInitialized:
					return "LiteNetLib4Mirror isn't initialized";
				case States.Idle:
					return "LiteNetLib4Mirror Transport idle";
				case States.ClientConnecting:
					return $"LiteNetLib4Mirror Client Connecting to {LiteNetLib4MirrorTransport.Singleton.clientAddress}:{LiteNetLib4MirrorTransport.Singleton.port}";
				case States.ClientConnected:
					return $"LiteNetLib4Mirror Client Connected to {LiteNetLib4MirrorTransport.Singleton.clientAddress}:{LiteNetLib4MirrorTransport.Singleton.port}";
				case States.Server:
#if DISABLE_IPV6
					return $"LiteNetLib4Mirror Server active at IPv4:{LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress} Port:{LiteNetLib4MirrorTransport.Singleton.port}";
#else
					return $"LiteNetLib4Mirror Server active at IPv4:{LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress} IPv6:{LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress} Port:{LiteNetLib4MirrorTransport.Singleton.port}";
#endif
				default:
					return "Invalid state!";
			}
		}

		internal static void SetOptions(bool server)
		{
#if !DISABLE_IPV6
			Host.IPv6Enabled = LiteNetLib4MirrorTransport.Singleton.ipv6Enabled;
#else
			Host.IPv6Enabled = false;
#endif
			Host.UpdateTime = LiteNetLib4MirrorTransport.Singleton.updateTime;
			Host.PingInterval = LiteNetLib4MirrorTransport.Singleton.pingInterval;
			Host.DisconnectTimeout = LiteNetLib4MirrorTransport.Singleton.disconnectTimeout;
			Host.ReconnectDelay = LiteNetLib4MirrorTransport.Singleton.reconnectDelay;
			Host.MaxConnectAttempts = LiteNetLib4MirrorTransport.Singleton.maxConnectAttempts;
			Host.UseNativeSockets = LiteNetLib4MirrorTransport.Singleton.useNativeSockets;

			Host.SimulatePacketLoss = LiteNetLib4MirrorTransport.Singleton.simulatePacketLoss;
			Host.SimulationPacketLossChance = LiteNetLib4MirrorTransport.Singleton.simulationPacketLossChance;
			Host.SimulateLatency = LiteNetLib4MirrorTransport.Singleton.simulateLatency;
			Host.SimulationMinLatency = LiteNetLib4MirrorTransport.Singleton.simulationMinLatency;
			Host.SimulationMaxLatency = LiteNetLib4MirrorTransport.Singleton.simulationMaxLatency;

			Host.BroadcastReceiveEnabled = server && LiteNetLib4MirrorDiscovery.Singleton != null;

			Host.ChannelsCount = (byte)LiteNetLib4MirrorTransport.Singleton.channels.Length;
		}

		internal static void StopTransport()
		{
			if (Host != null)
			{
				LiteNetLib4MirrorServer.Peers = null;
				Host.Stop();
				Host = null;
				LiteNetLib4MirrorTransport.Polling = false;
				State = States.Idle;
			}
		}

		internal static int GetMaxPacketSize(DeliveryMethod channel)
		{
			int mtu = Host != null && Host.FirstPeer != null ? Host.FirstPeer.Mtu : NetConstants.MaxPacketSize;
			switch (channel)
			{
				case DeliveryMethod.ReliableOrdered:
				case DeliveryMethod.ReliableUnordered:
					return ushort.MaxValue * (mtu - NetConstants.FragmentHeaderSize);
				case DeliveryMethod.ReliableSequenced:
				case DeliveryMethod.Sequenced:
					return mtu - NetConstants.ChanneledHeaderSize;
				case DeliveryMethod.Unreliable:
					return mtu - NetConstants.HeaderSize;
				default:
					return mtu - NetConstants.HeaderSize;
			}
		}
	}
}
