using System.Collections;
using System.ComponentModel;
using System.Net;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(NetworkManager))]
	[RequireComponent(typeof(NetworkManagerHUD))]
	[RequireComponent(typeof(LiteNetLib4MirrorTransport))]
	[RequireComponent(typeof(LiteNetLib4MirrorDiscovery))]
	[EditorBrowsable(EditorBrowsableState.Never)]
	// ReSharper disable once InconsistentNaming
	public class NetworkDiscoveryHUD : MonoBehaviour
	{
		[SerializeField] public float discoveryInterval = 1f;
		private NetworkManagerHUD _managerHud;
		private bool _noDiscovering = true;

		private void Awake()
		{
			_managerHud = GetComponent<NetworkManagerHUD>();
		}

#if UNITY_EDITOR
		private void OnGUI()
		{
			if (!_managerHud.enabled)
			{
				_noDiscovering = true;
				return;
			}

			GUILayout.BeginArea(new Rect(10 + _managerHud.offsetX + 215 + 10, 40 + _managerHud.offsetY, 215, 9999));
			if (!NetworkClient.isConnected && !NetworkServer.active)
			{
				if (_noDiscovering)
				{
					if (GUILayout.Button("Start Discovery"))
					{
						StartCoroutine(StartDiscovery());
					}
				}
				else
				{
					GUILayout.Label("Discovering..");
					GUILayout.Label($"LocalPort: {LiteNetLib4MirrorTransport.Singleton.port}");
					if (GUILayout.Button("Stop Discovery"))
					{
						_noDiscovering = true;
					}
				}
			}
			else
			{
				_noDiscovering = true;
			}

			GUILayout.EndArea();
		}
#endif

		private IEnumerator StartDiscovery()
		{
			_noDiscovering = false;

			LiteNetLib4MirrorDiscovery.InitializeFinder();
			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.AddListener(OnClientDiscoveryResponse);
			while (!_noDiscovering)
			{
				LiteNetLib4MirrorDiscovery.SendDiscoveryRequest("NetworkManagerHUD");
				yield return new WaitForSeconds(discoveryInterval);
			}

			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.RemoveListener(OnClientDiscoveryResponse);
			LiteNetLib4MirrorDiscovery.StopDiscovery();
		}

		private void OnClientDiscoveryResponse(IPEndPoint endpoint, string text)
		{
			string ip = endpoint.Address.ToString();

			NetworkManager.singleton.networkAddress = ip;
			NetworkManager.singleton.maxConnections = 2;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = ip;
			LiteNetLib4MirrorTransport.Singleton.port = (ushort)endpoint.Port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = 2;
			NetworkManager.singleton.StartClient();
			_noDiscovering = true;
		}
	}
}
