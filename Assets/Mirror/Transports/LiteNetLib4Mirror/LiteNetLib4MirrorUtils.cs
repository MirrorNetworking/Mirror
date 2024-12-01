using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using LiteNetLib4Mirror.Open.Nat;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.LiteNetLib4Mirror
{
	[Serializable] public class UnityEventError : UnityEvent<SocketError> { }
	[Serializable] public class UnityEventIntError : UnityEvent<int, SocketError> { }
	[Serializable] public class UnityEventIpEndpointString : UnityEvent<IPEndPoint, string> { }
	public static class LiteNetLib4MirrorUtils
	{
		internal static ushort LastForwardedPort;
		internal static readonly string ApplicationName;
		public static bool UpnpFailed { get; private set; }
		public static IPAddress ExternalIp { get; private set; }

		static LiteNetLib4MirrorUtils()
		{
			ApplicationName = Application.productName;
		}

		public static string ToBase64(string text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
		}

		public static string FromBase64(string text)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(text));
		}

		public static NetDataWriter ReusePut(NetDataWriter writer, string text, ref string lastText)
		{
			if (text != lastText)
			{
				lastText = text;
				writer.Reset();
				writer.Put(ToBase64(text));
			}

			return writer;
		}

		public static NetDataWriter ReusePutDiscovery(NetDataWriter writer, string text, ref string lastText)
		{
			if (ApplicationName + text != lastText)
			{
				lastText = ApplicationName + text;
				writer.Reset();
				writer.Put(ApplicationName);
				writer.Put(ToBase64(text));
			}

			return writer;
		}

		public static IPAddress Parse(string address)
		{
			switch (address)
			{
				case "0.0.0.0":
					return IPAddress.Any;
				case "0:0:0:0:0:0:0:0":
				case "::":
					return IPAddress.IPv6Any;
				case "localhost":
				case "127.0.0.1":
					return IPAddress.Loopback;
				case "0:0:0:0:0:0:0:1":
				case "::1":
					return IPAddress.IPv6Loopback;
			}

			if (IPAddress.TryParse(address, out IPAddress ipAddress))
			{
				return ipAddress;
			}

			IPAddress[] addresses = Dns.GetHostAddresses(address);
#if DISABLE_IPV6
			return FirstAddressOfType(addresses, AddressFamily.InterNetwork) ?? addresses[0];
#else
			if (LiteNetLib4MirrorTransport.Singleton.ipv6Enabled)
			{
				return (FirstAddressOfType(addresses, AddressFamily.InterNetworkV6) ?? FirstAddressOfType(addresses, AddressFamily.InterNetwork)) ?? addresses[0];
			}

			return FirstAddressOfType(addresses, AddressFamily.InterNetwork) ?? addresses[0];
#endif
		}

		public static IPEndPoint Parse(string address, ushort port)
		{
			return new IPEndPoint(Parse(address), port);
		}

		private static IPAddress FirstAddressOfType(IPAddress[] addresses, AddressFamily type)
		{
			for (int i = 0; i < addresses.Length; i++)
			{
				IPAddress address = addresses[i];
				if (address.AddressFamily == type)
				{
					return address;
				}
			}
			return null;
		}

		/// <summary>
		/// Utility function for getting first free port in range (as a bonus, should work if unity doesn't shit itself)
		/// </summary>
		/// <param name="ports">Available ports</param>
		/// <returns>First free port in range</returns>
		public static ushort GetFirstFreePort(params ushort[] ports)
		{
			if (ports == null || ports.Length == 0) throw new Exception("No ports provided");
			ushort freeport = ports.Except(Array.ConvertAll(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners(), p => (ushort)p.Port)).FirstOrDefault();
			if (freeport == 0) throw new Exception("No free port!");
			return freeport;
		}

#pragma warning disable 4014
		public static void ForwardPort(NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp, int milisecondsDelay = 10000)
		{
			ForwardPortInternalAsync(LiteNetLib4MirrorTransport.Singleton.port, milisecondsDelay, networkProtocolType);
		}

		public static void ForwardPort(ushort port, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp, int milisecondsDelay = 10000)
		{
			ForwardPortInternalAsync(port, milisecondsDelay, networkProtocolType);
		}
#pragma warning restore 4014

		private static async Task ForwardPortInternalAsync(ushort port, int milisecondsDelay, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp)
		{
			try
			{
				if (LastForwardedPort == port || UpnpFailed) return;
				if (LastForwardedPort != 0) NatDiscoverer.ReleaseAll();
				NatDiscoverer discoverer = new NatDiscoverer();
				NatDevice device;
				using (CancellationTokenSource cts = new CancellationTokenSource(milisecondsDelay))
				{
					device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts).ConfigureAwait(false);
				}

				ExternalIp = await device.GetExternalIPAsync();
				await device.CreatePortMapAsync(new Mapping(networkProtocolType, IPAddress.None, port, port, 0, ApplicationName)).ConfigureAwait(false);
				LastForwardedPort = port;
				Debug.Log($"Port {port.ToString()} forwarded successfully!");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"UPnP failed: {ex.Message}");
				UpnpFailed = true;
			}
		}
	}
}
