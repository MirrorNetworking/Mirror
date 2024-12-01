//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com
//
// Copyright (C) 2007 Ben Motmans
// Copyright (C) 2014 Lucas Ontivero
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LiteNetLib4Mirror.Open.Nat
{
	internal abstract class Searcher
	{
		private readonly List<NatDevice> _devices = new List<NatDevice>();
		protected List<UdpClient> UdpClients;
		public EventHandler<DeviceEventArgs> DeviceFound;
		internal DateTime NextSearch = DateTime.UtcNow;

#if !(NET_4_6 || NET_STANDARD_2_0)
		public Task<IEnumerable<NatDevice>> Search(CancellationToken cancelationToken)
		{
			return Task.Factory.StartNew(_ =>
			{
				NatDiscoverer.TraceSource.LogInfo("Searching for: {0}", GetType().Name);
				while (!cancelationToken.IsCancellationRequested)
				{
					Discover(cancelationToken);
					Receive(cancelationToken);
				}
				CloseUdpClients();
			}, cancelationToken)
			.ContinueWith<IEnumerable<NatDevice>>((Task task) => _devices);
		}
#else
		public async Task<IEnumerable<NatDevice>> Search(CancellationToken cancelationToken)
		{
			await Task.Factory.StartNew(_ =>
				{
					NatDiscoverer.TraceSource.LogInfo("Searching for: {0}", GetType().Name);
					while (!cancelationToken.IsCancellationRequested)
					{
						Discover(cancelationToken);
						Receive(cancelationToken);
					}
					CloseUdpClients();
				}, null, cancelationToken);
			return _devices;
		}
#endif

		private void Discover(CancellationToken cancelationToken)
		{
			if(DateTime.UtcNow < NextSearch) return;

			foreach (UdpClient socket in UdpClients)
			{
				try
				{
					Discover(socket, cancelationToken);
				}
				catch (Exception e)
				{
					NatDiscoverer.TraceSource.LogError("Error searching {0} - Details:", GetType().Name);
					NatDiscoverer.TraceSource.LogError(e.ToString());
				}
			}
		}

		private void Receive(CancellationToken cancelationToken)
		{
			foreach (UdpClient client in UdpClients.Where(x=>x.Available>0))
			{
				if(cancelationToken.IsCancellationRequested) return;

				IPAddress localHost = ((IPEndPoint)client.Client.LocalEndPoint).Address;
				IPEndPoint receivedFrom = new IPEndPoint(IPAddress.None, 0);
				byte[] buffer = client.Receive(ref receivedFrom);
				NatDevice device = AnalyseReceivedResponse(localHost, buffer, receivedFrom);

				if (device != null) RaiseDeviceFound(device);
			}
		}


		protected abstract void Discover(UdpClient client, CancellationToken cancelationToken);

		public abstract NatDevice AnalyseReceivedResponse(IPAddress localAddress, byte[] response, IPEndPoint endpoint);

		public void CloseUdpClients()
		{
			foreach (UdpClient udpClient in UdpClients)
			{
				udpClient.Close();
			}
		}

		private void RaiseDeviceFound(NatDevice device)
		{
			_devices.Add(device);
			EventHandler<DeviceEventArgs> handler = DeviceFound;
			handler?.Invoke(this, new DeviceEventArgs(device));
		}
	}
}
