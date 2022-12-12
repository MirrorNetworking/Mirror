using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
	public class NetworkManagerDelegates : NetworkManager
	{
		public delegate void OnClientConnectDelegate();
		public delegate void OnClientDisconnectDelegate();

		public static OnClientConnectDelegate onClientConnectDelegate;
		public static OnClientDisconnectDelegate onClientDisconnectDelegate;

		public override void OnClientConnect()
		{
			base.OnClientConnect();

			onClientConnectDelegate?.Invoke();
		}

		public override void OnClientDisconnect()
		{
			base.OnClientDisconnect();

			onClientDisconnectDelegate?.Invoke();
		}
	}
}
