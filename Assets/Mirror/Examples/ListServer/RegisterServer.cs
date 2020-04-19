// add this component to the NetworkManager
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror.Examples.ListServer
{

    public class RegisterServer : NetworkBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(RegisterServer));

        public NetworkManager manager;
        [Header("List Server Connection")]
        public string listServerUrl = "tcp4://127.0.0.1:8887";
        public string gameServerTitle = "Deathmatch";
        IConnection gameServerToListenConnection;

        public AsyncTransport transport;

        // all the servers, stored as dict with unique ip key so we can
        // update them more easily
        // (use "ip:port" if port is needed)
        readonly Dictionary<string, ServerStatus> list = new Dictionary<string, ServerStatus>();

        public async Task Start()
        {
            gameServerToListenConnection = await transport.ConnectAsync(new System.Uri(listServerUrl));

            // Update once a second. no need to try to reconnect or read data
            // in each Update call
            // -> calling it more than 1/second would also cause significantly
            //    more broadcasts in the list server.
            InvokeRepeating(nameof(Tick), 0, 1);
        }

        // send server status to list server
        void SendStatus()
        {
            var writer = new BinaryWriter(new MemoryStream());

            // create message
            writer.Write((ushort)manager.server.connections.Count);
            writer.Write((ushort)manager.server.MaxConnections);
            byte[] titleBytes = Encoding.UTF8.GetBytes(gameServerTitle);
            writer.Write((ushort)titleBytes.Length);
            writer.Write(titleBytes);
            writer.Flush();

            // list server only allows up to 128 bytes per message
            if (writer.BaseStream.Position <= 128)
            {
                byte[] data = ((MemoryStream)writer.BaseStream).ToArray();
                // send it
                _ = gameServerToListenConnection.SendAsync(new System.ArraySegment<byte>(data));
            }
            else logger.LogError("[List Server] List Server will reject messages longer than 128 bytes. Please use a shorter title.");
        }

        void Tick()
        {
            // connected yet?
            if (IsServer && gameServerToListenConnection != null)
            {
                SendStatus();
            }
        }

        private void OnDestroy()
        {
            if (gameServerToListenConnection != null)
            {
                gameServerToListenConnection.Disconnect();
                gameServerToListenConnection = null;
            }
        }
    }
}
