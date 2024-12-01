using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	public static class LiteNetLib4MirrorClient
	{
		/// <summary>
		/// Use LiteNetLib4MirrorNetworkManager.DisconnectConnection to send the reason
		/// </summary>
		public static string LastDisconnectReason { get; private set; }

		public static int GetPing()
		{
			return LiteNetLib4MirrorCore.Host.FirstPeer.Ping;
		}

		internal static bool IsConnected()
		{
			return LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.ClientConnected || LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.ClientConnecting;
		}

		internal static void ConnectClient(NetDataWriter data)
		{
			try
			{
				if (LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Discovery)
				{
					LiteNetLib4MirrorCore.StopTransport();
				}
				EventBasedNetListener listener = new EventBasedNetListener();
				LiteNetLib4MirrorCore.Host = new NetManager(listener);
				listener.NetworkReceiveEvent += OnNetworkReceive;
				listener.NetworkErrorEvent += OnNetworkError;
				listener.PeerConnectedEvent += OnPeerConnected;
				listener.PeerDisconnectedEvent += OnPeerDisconnected;

				LiteNetLib4MirrorCore.SetOptions(false);

				LiteNetLib4MirrorCore.Host.Start();
				LiteNetLib4MirrorCore.Host.Connect(LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.clientAddress, LiteNetLib4MirrorTransport.Singleton.port), data);

				LiteNetLib4MirrorTransport.Polling = true;
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.ClientConnecting;
			}
			catch (Exception ex)
			{
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
				Debug.LogException(ex);
			}
		}

		private static void OnPeerConnected(NetPeer peer)
		{
			LastDisconnectReason = null;
			LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.ClientConnected;
			LiteNetLib4MirrorTransport.Singleton.OnClientConnected.Invoke();
		}

		private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			switch (disconnectinfo.Reason)
			{
				case DisconnectReason.ConnectionRejected:
					LiteNetLib4MirrorTransport.Singleton.OnConncetionRefused(disconnectinfo);
					LastDisconnectReason = null;
					break;
				case DisconnectReason.DisconnectPeerCalled when disconnectinfo.AdditionalData.TryGetString(out string reason) && !string.IsNullOrWhiteSpace(reason):
					LastDisconnectReason = LiteNetLib4MirrorUtils.FromBase64(reason);
					break;
				default:
					LastDisconnectReason = null;
					break;
			}
			LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
			LiteNetLib4MirrorCore.LastDisconnectError = disconnectinfo.SocketErrorCode;
			LiteNetLib4MirrorCore.LastDisconnectReason = disconnectinfo.Reason;
			LiteNetLib4MirrorTransport.Singleton.OnClientDisconnected.Invoke();
			LiteNetLib4MirrorCore.StopTransport();
		}

		private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
		{
			LiteNetLib4MirrorTransport.Singleton.OnClientDataReceived.Invoke(reader.GetRemainingBytesSegment(), -1);
			reader.Recycle();
		}

		private static void OnNetworkError(IPEndPoint endpoint, SocketError socketerror)
		{
			LiteNetLib4MirrorCore.LastError = socketerror;
			LiteNetLib4MirrorTransport.Singleton.OnClientError.Invoke(TransportError.Unexpected, $"Socket exception: {(int)socketerror}");
			LiteNetLib4MirrorTransport.Singleton.onClientSocketError.Invoke(socketerror);
		}

		internal static bool Send(DeliveryMethod method, byte[] data, int start, int length, byte channelNumber)
		{
			try
			{
				LiteNetLib4MirrorCore.Host.FirstPeer.Send(data, start, length, channelNumber, method);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
