#if UNITY_2018_3_OR_NEWER
using System.Net;

namespace LiteNetLib
{
	public class PausedSocketFix
	{
		private readonly NetManager _netManager;
		private readonly IPAddress _ipv4;
		private readonly IPAddress _ipv6;
		private readonly int _port;
		private readonly bool _manualMode;
		private bool _initialized;

		public PausedSocketFix(NetManager netManager, IPAddress ipv4, IPAddress ipv6, int port, bool manualMode)
		{
			_netManager = netManager;
			_ipv4 = ipv4;
			_ipv6 = ipv6;
			_port = port;
			_manualMode = manualMode;
			UnityEngine.Application.focusChanged += Application_focusChanged;
			_initialized = true;
		}

		public void Deinitialize()
		{
			if (_initialized)
				UnityEngine.Application.focusChanged -= Application_focusChanged;
			_initialized = false;
		}

		private void Application_focusChanged(bool focused)
		{
			//If coming back into focus see if a reconnect is needed.
			if (focused)
			{
				//try reconnect
				if (!_initialized)
					return;
				//Was intentionally disconnected at some point.
				if (!_netManager.IsRunning)
					return;
				//Socket is in working state.
				if (_netManager.NotConnected == false)
					return;

				//Socket isn't running but should be. Try to start again.
				if (!_netManager.Start(_ipv4, _ipv6, _port, _manualMode))
				{
					NetDebug.WriteError($"[S] Cannot restore connection. Ipv4 {_ipv4}, Ipv6 {_ipv6}, Port {_port}, ManualMode {_manualMode}");
				}
			}
		}
	}
}
#endif
