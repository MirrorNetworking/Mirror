using System.Net;
using System.Net.Sockets;

namespace LiteNetLib.Utils
{
	internal sealed class NtpRequest
	{
		private const int ResendTimer = 1000;
		private const int KillTimer = 10000;
		public const int DefaultPort = 123;
		private readonly IPEndPoint _ntpEndPoint;
		private int _resendTime = ResendTimer;
		private int _killTime = 0;

		public NtpRequest(IPEndPoint endPoint)
		{
			_ntpEndPoint = endPoint;
		}

		public bool NeedToKill =>
			_killTime >= KillTimer;

		public bool Send(Socket socket, int time)
		{
			_resendTime += time;
			_killTime += time;
			if (_resendTime < ResendTimer)
			{
				return false;
			}
			var packet = new NtpPacket();
			try
			{
				int sendCount = socket.SendTo(packet.Bytes, 0, packet.Bytes.Length, SocketFlags.None, _ntpEndPoint);
				return sendCount == packet.Bytes.Length;
			}
			catch
			{
				return false;
			}
		}
	}
}
