using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Mirror.Transport.Tcp
{
    public class Server : Common
    {
        public event Action<int> Connected;
        public event Action<int, byte[]> ReceivedData;
        public event Action<int> Disconnected;
        public event Action<int, Exception> ReceivedError;

        // listener
        TcpListener listener;

        // clients with <connectionId, TcpClient>
        Dictionary<int, TcpClient> clients = new Dictionary<int, TcpClient>();

        public bool NoDelay = true;

        // connectionId counter
        // (right now we only use it from one listener thread, but we might have
        //  multiple threads later in case of WebSockets etc.)
        // -> static so that another server instance doesn't start at 0 again.
        static int counter = 0;

        // public next id function in case someone needs to reserve an id
        // (e.g. if hostMode should always have 0 connection and external
        //  connections should start at 1, etc.)
        public static int NextConnectionId()
        {
            int id = Interlocked.Increment(ref counter);

            // it's very unlikely that we reach the uint limit of 2 billion.
            // even with 1 new connection per second, this would take 68 years.
            // -> but if it happens, then we should throw an exception because
            //    the caller probably should stop accepting clients.
            // -> it's hardly worth using 'bool Next(out id)' for that case
            //    because it's just so unlikely.
            if (id == int.MaxValue)
            {
                throw new Exception("connection id limit reached: " + id);
            }

            return id;
        }

        // check if the server is running
        public bool Active
        {
            get { return listener != null; }
        }

        public TcpClient GetClient(int connectionId)
        {
            // paul:  null is evil,  throw exception if not found
            return clients[connectionId];
        }

        // the listener thread's listen function
        async public void Listen(int port, int maxConnections = int.MaxValue)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {

                if (listener != null)
                {
                    ReceivedError?.Invoke(0, new Exception("Already listening"));
                    return;
                }

                // start listener
                listener = TcpListener.Create(port);

                // NoDelay disables nagle algorithm. lowers CPU% and latency
                // but increases bandwidth
                listener.Server.NoDelay = this.NoDelay;
                listener.Start();
                Debug.Log($"Tcp server started listening on port {port}");

                // keep accepting new clients
                while (true)
                {
                    // wait for a tcp client;
                    TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                    // non blocking receive loop
                    ReceiveLoop(tcpClient);
                }
            }
            catch(ObjectDisposedException)
            {
                Debug.Log("Server dispossed");
            }
            catch (Exception exception)
            {
                ReceivedError?.Invoke(0, exception);
            }
            finally
            {
                listener = null;
            }
        }

        private async void ReceiveLoop(TcpClient tcpClient)
        {
            int connectionId = NextConnectionId();
            clients.Add(connectionId, tcpClient);

            try
            { 
                // someone connected,  raise event
                Connected?.Invoke(connectionId);

                using (Stream networkStream = tcpClient.GetStream())
                {
                    while (true)
                    {
                        byte[] data = await ReadMessageAsync(networkStream);

                        if (data == null)
                            break;

                        // we received some data,  raise event
                        ReceivedData?.Invoke(connectionId, data);
                    }
                }
            }
            catch (Exception exception)
            {
                ReceivedError?.Invoke(connectionId, exception);
            }
            finally
            {
                clients.Remove(connectionId);
                Disconnected?.Invoke(connectionId);
            }
        }

        public void Stop()
        {
            // only if started
            if (!Active) return;

            Debug.Log("Server: stopping...");

            // stop listening to connections so that no one can connect while we
            // close the client connections
            listener.Stop();

            // close all client connections
            foreach (var kvp in clients)
            {
                // close the stream if not closed yet. it may have been closed
                // by a disconnect already, so use try/catch
                try { kvp.Value.Close(); } catch {}
            }

            // clear clients list
            clients.Clear();
            listener = null;
        }

        // send message to client using socket connection or throws exception
        public async void Send(int connectionId, byte[] data)
        {
            // find the connection
            TcpClient client;
            if (clients.TryGetValue(connectionId, out client))
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    await SendMessage(stream, data);
                }
                catch (ObjectDisposedException) {
                    // connection has been closed,  swallow exception
                    Disconnect(connectionId);
                }
                catch (Exception exception)
                {
                    if (clients.ContainsKey(connectionId))
                    {
                        // paul:  If someone unplugs their internet
                        // we can potentially get hundreds of errors here all at once
                        // because all the WriteAsync wake up at once and throw exceptions

                        // by hiding inside this if, I ensure that we only report the first error
                        // all other errors are swallowed.  
                        // this prevents a log storm that freezes the server for several seconds
                        ReceivedError?.Invoke(connectionId, exception);
                    }

                    Disconnect(connectionId);
                }
            }
            else
            {
                ReceivedError?.Invoke(connectionId, new SocketException((int)SocketError.NotConnected));
            }
        }

        // get connection info in case it's needed (IP etc.)
        // (we should never pass the TcpClient to the outside)
        public bool GetConnectionInfo(int connectionId, out string address)
        {
            // find the connection
            TcpClient client;
            if (clients.TryGetValue(connectionId, out client))
            {
                address = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
                return true;
            }
            address = null;
            return false;
        }

        // disconnect (kick) a client
        public bool Disconnect(int connectionId)
        {
            // find the connection
            TcpClient client;
            if (clients.TryGetValue(connectionId, out client))
            {
                clients.Remove(connectionId);
                // just close it. client thread will take care of the rest.
                client.Close();
                Debug.Log("Server.Disconnect connectionId:" + connectionId);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (Active)
            {
                return $"TCP server {listener.LocalEndpoint}";
            }
            return "";
        }
    }
}
