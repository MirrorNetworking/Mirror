using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    public class MultiplexTransport : Transport
    {

        public Transport[] transports;

        AutoResetUniTaskCompletionSource completionSource;

        Queue<IConnection> acceptedConnections;
        Queue<Transport> acceptedTransport;

        public override IEnumerable<string> Scheme =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.Scheme);

        private Transport GetTransport()
        {
            foreach (Transport transport in transports)
            {
                if (transport.Supported)
                    return transport;
            }
            throw new PlatformNotSupportedException("None of the transports is supported in this platform");
        }

        public override bool Supported => GetTransport() != null;

        public override async UniTask<IConnection> AcceptAsync()
        {
            if (acceptedTransport == null)
            {
                acceptedTransport = new Queue<Transport>();
                acceptedConnections = new Queue<IConnection>();

                foreach (Transport transport in transports)
                {
                    acceptedTransport.Enqueue(transport);
                }
            }

            while (true)
            {
                if (acceptedConnections.Count > 0)
                    return acceptedConnections.Dequeue();

                // all transports already closed
                if (acceptedTransport.Count == 0)
                    return null;

                completionSource = AutoResetUniTaskCompletionSource.Create();

                // no pending connections, accept from any transport again
                while (acceptedTransport.Count > 0 && acceptedConnections.Count == 0)
                {
                    Transport transport = acceptedTransport.Dequeue();
                    AcceptConnection(transport).Forget();
                }

                await completionSource.Task;

                completionSource = null;

            }

        }

        private async UniTaskVoid AcceptConnection(Transport transport)
        {
            try
            {
                IConnection connection = await transport.AcceptAsync();

                if (connection != null)
                {
                    acceptedConnections.Enqueue(connection);
                    acceptedTransport.Enqueue(transport);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                completionSource?.TrySetResult();
            }
        }

        public override UniTask<IConnection> ConnectAsync(Uri uri)
        {
            foreach (Transport transport in transports)
            {
                if (transport.Supported && transport.Scheme.Contains(uri.Scheme))
                    return transport.ConnectAsync(uri);
            }
            throw new ArgumentException($"No transport was able to connect to {uri}");
        }

        public override void Disconnect()
        {
            foreach (Transport transport in transports)
                transport.Disconnect();
        }

        public override async UniTask ListenAsync()
        {
            IEnumerable<UniTask> tasks = from t in transports select t.ListenAsync();
            await UniTask.WhenAll(tasks);
            acceptedTransport = null;
            acceptedConnections = null;
        }

        public override IEnumerable<Uri> ServerUri() =>
            transports
                .Where(transport => transport.Supported)
                .SelectMany(transport => transport.ServerUri());
    }
}